// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.Pattern;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Unified read API for skill metrics (song select, local profile Track, …).
    /// Covers beatmap MSD, player SSR, player dan estimates, chart skill info, and chart-dan verdicts.
    /// Writers (computers / aggregators) are separate DI services — see <see cref="EzSkillSystems"/>.
    /// All UI surfaces (HUD DualPanel / Radar, Analysis Wedge, LocalProfile Track, display tags) read through this type.
    /// </summary>
    public sealed class EzSkillProvider
    {
        /// <summary>
        /// Chart-side rows. Exposed for callers that need a table-level read the provider's facade does not shape —
        /// e.g. joining chart coverage against the drill ledger for <see cref="Skills.Dan.EzChartChainDebt"/>.
        /// </summary>
        public EzChartSkillStore ChartStore { get; }

        /// <summary>Player-side aggregates, stored next to the score ledger they are folded from.</summary>
        public EzPlayerSkillStore PlayerStore { get; }

        private readonly EzChartDanEstimator? chartDanEstimator;
        private readonly EzLocalProfileStore? localProfileStore;
        private readonly BeatmapManager? beatmapManager;

        /// <summary>Session-only CSI compute failures — keep miss in Realm so BDSP can retry next launch.</summary>
        private readonly HashSet<string> chartSkillInfoSessionMisses = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Records a CSI miss, dropping the set once it outgrows its bound.</summary>
        private void recordChartSkillInfoSessionMiss(string beatmapHash)
        {
            if (chartSkillInfoSessionMisses.Count >= session_memo_capacity)
                chartSkillInfoSessionMisses.Clear();

            chartSkillInfoSessionMisses.Add(beatmapHash);
        }

        /// <summary>Session-only values computed on read paths. Never persisted.</summary>
        private readonly EzAnalysisLiveCache liveCache;

        /// <summary>
        /// Session ChartDan cache. May hold Realm copies or DualPanel memory-only computes (never auto-upserted).
        /// </summary>
        private readonly ConcurrentDictionary<string, EzPersistedChartDan> chartDanSessionCache
            = new ConcurrentDictionary<string, EzPersistedChartDan>(StringComparer.Ordinal);

        /// <summary>
        /// Short-lived player SSR memo (see <see cref="GetPlayerSsrSnapshot"/>). Keyed by (username, keyCount).
        /// </summary>
        private readonly ConcurrentDictionary<(string Username, int KeyCount), SsrSnapshotMemo> ssrSnapshotMemo
            = new ConcurrentDictionary<(string Username, int KeyCount), SsrSnapshotMemo>();

        private readonly record struct SsrSnapshotMemo(EzPlayerRatingSnapshot Snapshot, long Timestamp);

        /// <summary>Short-lived dan label/rating memo (see <see cref="GetDanDisplay"/>); plain values only.</summary>
        private readonly ConcurrentDictionary<(string Username, int KeyCount, string Side), DanDisplayMemo> danDisplayMemo
            = new ConcurrentDictionary<(string Username, int KeyCount, string Side), DanDisplayMemo>();

        private readonly record struct DanDisplayMemo(string Label, double RawDan, long Timestamp);

        /// <summary>
        /// Session-only skillset verdict memo (see <see cref="GetDanSkillsets"/>). The durable rows are written by the
        /// manual player analysis, so a read never goes to disk for a value it can hold here.
        /// </summary>
        private readonly ConcurrentDictionary<(string Username, int KeyCount, string Side), IReadOnlyDictionary<string, EzDanSkillsetVerdict>> danSkillsetSessionCache
            = new ConcurrentDictionary<(string Username, int KeyCount, string Side), IReadOnlyDictionary<string, EzDanSkillsetVerdict>>();

        private const double display_memo_seconds = 2;

        /// <summary>Upper bound on the session memos below; a full drop costs one re-read.</summary>
        private const int session_memo_capacity = 1024;

        /// <summary>
        /// Inserts into a session memo, dropping the whole map once it outgrows its bound. These caches are keyed
        /// by data the user browses freely, so counting is the only bound that cannot be defeated by a new key.
        /// </summary>
        private static void storeBounded<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, TKey key, TValue value)
            where TKey : notnull
        {
            if (map.Count >= session_memo_capacity && !map.ContainsKey(key))
                map.Clear();

            map[key] = value;
        }

        public EzSkillProvider(
            EzChartSkillStore chartStore,
            EzPlayerSkillStore playerStore,
            EzMetricRevisionResolver? resolver = null,
            EzChartDanEstimator? chartDanEstimator = null,
            EzLocalProfileStore? localProfileStore = null,
            BeatmapManager? beatmapManager = null,
            EzAnalysisLiveCache? liveCache = null)
        {
            ChartStore = chartStore;
            PlayerStore = playerStore;
            this.chartDanEstimator = chartDanEstimator;
            this.localProfileStore = localProfileStore;
            this.beatmapManager = beatmapManager;
            this.liveCache = liveCache ?? new EzAnalysisLiveCache();
            Resolver = resolver ?? EzMetricRegistries.Current;
            PlayerSkills = new PlayerSkillMaintenance(this);
        }

        public EzMetricRevisionResolver Resolver { get; }

        public EzMetricProducerRegistry ChartRegistry => Resolver.ChartRegistry;

        public EzMetricProducerRegistry PlayerRegistry => Resolver.PlayerRegistry;

        /// <summary>
        /// Chart-side skill wiring (systems + default radar) on <paramref name="rulesetOnlineId"/>. Null when the
        /// chart catalog declares no vector for it.
        /// </summary>
        public EzSkillProfile? GetChartProfile(int rulesetOnlineId)
            => ChartRegistry.GetProfile(rulesetOnlineId);

        /// <summary>Player-side skill wiring on <paramref name="rulesetOnlineId"/>.</summary>
        public EzSkillProfile? GetPlayerProfile(int rulesetOnlineId)
            => PlayerRegistry.GetProfile(rulesetOnlineId);

        /// <summary>
        /// Generic beatmap skill read for any registered <paramref name="systemId"/>.
        /// Prefer this over mania-specific helpers when the caller already knows the system.
        /// </summary>
        public IReadOnlyDictionary<string, double> GetBeatmapSystemSkills(BeatmapInfo beatmapInfo, string systemId)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (string.IsNullOrEmpty(beatmapInfo.Hash) || string.IsNullOrEmpty(systemId))
                return new Dictionary<string, double>(StringComparer.Ordinal);

            if (systemId == EzSkillSystems.BEATMAP_PPP)
                return ChartStore.GetBeatmapSkillsAtCurrentVersions(beatmapInfo, systemId);

            return ChartStore.GetBeatmapSkills(beatmapInfo.Hash, systemId);
        }

        /// <summary>
        /// Generic player skill read. <paramref name="sliceKey"/> is keymode for mania, or
        /// <see cref="EzSkillSystems.UNSLICED_KEY"/> for osu. <paramref name="rulesetOnlineId"/> selects the
        /// ruleset slice; pass a negative value for systems whose rows are not ruleset-attributed.
        /// </summary>
        /// <remarks>
        /// Reads stored rows only: a player vector folds the whole score archive, so it is written by the score
        /// analysis and never measured on demand. Do not add a compute fallback here.
        /// </remarks>
        public IReadOnlyDictionary<string, double> GetPlayerSystemSkills(string username, int sliceKey, string systemId, int rulesetOnlineId = -1)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(systemId))
                return new Dictionary<string, double>(StringComparer.Ordinal);

            if (systemId == EzSkillSystems.PLAYER_PPP)
                return PlayerStore.GetPlayerSkillsAtCurrentVersions(username, sliceKey, systemId, rulesetOnlineId);

            return PlayerStore.GetPlayerSkills(username, sliceKey, systemId);
        }

        public (int AnalyzedPlays, bool Provisional, bool Stale) GetPlayerSystemSkillMeta(string username, int sliceKey, string systemId, int rulesetOnlineId = -1)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(systemId))
                return (0, false, false);

            var meta = PlayerStore.GetPlayerSystemSkillMeta(username, sliceKey, systemId, rulesetOnlineId: rulesetOnlineId);
            if (meta.AnalyzedPlays > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return meta;

            return PlayerStore.GetPlayerSystemSkillMeta(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, sliceKey, systemId, rulesetOnlineId: rulesetOnlineId);
        }

        /// <summary>
        /// One player slice's values and metadata from a single read. Prefer this over the two separate getters
        /// when both are needed; they each open their own transaction on the same row set.
        /// </summary>
        public (IReadOnlyDictionary<string, double> Values, (int AnalyzedPlays, bool Provisional, bool Stale) Meta) GetPlayerSystemSkillSlice(
            string username, int sliceKey, string systemId, int rulesetOnlineId = -1)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(systemId))
                return (new Dictionary<string, double>(StringComparer.Ordinal), (0, false, false));

            var slice = PlayerStore.GetPlayerSystemSkillSlice(username, sliceKey, systemId, rulesetOnlineId: rulesetOnlineId);

            if (slice.AnalyzedPlays > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return (slice.Values, (slice.AnalyzedPlays, slice.Provisional, slice.Stale));

            var legacy = PlayerStore.GetPlayerSystemSkillSlice(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, sliceKey, systemId, rulesetOnlineId: rulesetOnlineId);

            return (slice.Values, legacy.AnalyzedPlays > 0
                ? (legacy.AnalyzedPlays, legacy.Provisional, legacy.Stale)
                : (slice.AnalyzedPlays, slice.Provisional, slice.Stale));
        }

        public IReadOnlyDictionary<string, double> GetBeatmapMsd(string beatmapHash)
        {
            if (string.IsNullOrEmpty(beatmapHash))
                return new Dictionary<string, double>(StringComparer.Ordinal);

            // Settled misses are excluded by the store, so a verdict never reaches consumers as an axis set.
            return ChartStore.GetBeatmapSkills(beatmapHash, EzSkillSystems.BEATMAP_MINA);
        }

        /// <summary>
        /// Stored structure axis values for a chart, keyed by the bare axis id the axis declaration uses.
        /// </summary>
        /// <remarks>
        /// This is the baseline form of the same table the live pass emits, so a reader can draw the long-note frame
        /// before any live pass has run. Empty when the background has not reached the chart yet.
        /// </remarks>
        public IReadOnlyDictionary<string, double> GetBeatmapStructureAxes(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            var stored = GetBeatmapSystemSkills(beatmapInfo, EzSkillSystems.BEATMAP_CSI);

            if (stored.Count == 0)
                return stored;

            var bare = new Dictionary<string, double>(stored.Count, StringComparer.Ordinal);

            foreach (var (skillId, value) in stored)
                bare[EzMetricSkillId.TrySplit(skillId, out _, out string axisId) ? axisId : skillId] = value;

            return bare;
        }

        /// <summary>
        /// Whether MSD settled this chart as unrateable. Asks the stored state rather than the axis set: a
        /// verdict is not an axis, so a caller asking "has MSD settled it unrateable?" must not infer it from
        /// a row that is not there.
        /// </summary>
        private bool isSettledUnrateableMina(string beatmapHash)
            => ChartStore.GetBeatmapSystemState(beatmapHash, EzSkillSystems.BEATMAP_MINA, EzAnalysisRevision.Msd) == EzMetricState.Unrateable;

        /// <summary>
        /// Drop session-only values so the next read reflects a row written or cleared elsewhere.
        /// Pass a hash after that chart changed; omit to clear everything.
        /// </summary>
        public void InvalidateLiveMetrics(string? beatmapHash = null)
        {
            if (string.IsNullOrEmpty(beatmapHash))
            {
                liveCache.Clear();
                return;
            }

            liveCache.InvalidateSubject(beatmapHash);
        }

        /// <summary>Drop session ChartDan entry (e.g. after Realm UpsertChartDan).</summary>
        public void InvalidateChartDanSession(string beatmapHash)
        {
            if (!string.IsNullOrEmpty(beatmapHash))
                chartDanSessionCache.TryRemove(beatmapHash, out _);
        }

        /// <summary>
        /// Chart dan from persisted MSD (+ Realm xxy when present). No MSD compute / no beatmap note load.
        /// Hold ratio uses MSD <c>hold_ratio</c> only (no analysis SQLite on this path).
        /// </summary>
        public EzChartDanVerdict? TryGetCachedChartDan(BeatmapInfo beatmapInfo)
        {
            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var msd = GetBeatmapMsd(beatmapInfo.Hash);
            if (msd.Count == 0)
                return null;

            int keyCount = EzHoldMetrics.ResolveKeyCount(beatmapInfo);

            double holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            double? xxySr = beatmapInfo.SunnyStarRating >= 0 ? beatmapInfo.SunnyStarRating : null;
            return EzChartDanEstimator.FromMsd(msd, keyCount, holdRatio, xxySr);
        }

        public bool TryGetBeatmapMsdSkill(string beatmapHash, string axisId, out double value)
        {
            string skillId = EzMinaSkillAxisExtensions.TryParse(axisId, out var axis)
                ? axis.ToMsdSkillId()
                : axisId;
            return ChartStore.TryGetBeatmapSkill(beatmapHash, skillId, out value);
        }

        public IReadOnlyDictionary<string, double> GetPlayerSsr(string username, int keyCount)
            => GetPlayerSsrSnapshot(username, keyCount).Values;

        /// <summary>
        /// Player SSR snapshot with a short-lived memo. Song select re-reads this several times per switch
        /// (once per bindable-driven DualPanel/radar refresh); the TTL collapses those into one Realm read.
        /// Mirrors the other session caches here — a fresh compute becomes visible within the TTL at worst.
        /// </summary>
        public EzPlayerRatingSnapshot GetPlayerSsrSnapshot(string username, int keyCount)
        {
            var key = (username, keyCount);

            if (ssrSnapshotMemo.TryGetValue(key, out var memo))
            {
                if (Stopwatch.GetElapsedTime(memo.Timestamp).TotalSeconds < display_memo_seconds)
                    return memo.Snapshot;

                // An expired entry is dropped rather than overwritten in place: nothing else ever revisits it.
                ssrSnapshotMemo.TryRemove(key, out _);
            }

            var snapshot = loadPlayerSsrSnapshot(username, keyCount);
            storeBounded(ssrSnapshotMemo, key, new SsrSnapshotMemo(snapshot, Stopwatch.GetTimestamp()));
            return snapshot;
        }

        private EzPlayerRatingSnapshot loadPlayerSsrSnapshot(string username, int keyCount)
        {
            var snapshot = PlayerStore.GetPlayerSsrSnapshot(username, keyCount);
            if (snapshot.Values.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return snapshot;

            return PlayerStore.GetPlayerSsrSnapshot(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount);
        }

        /// <summary>Drop the SSR snapshot memo (after a recompute / write).</summary>
        public void InvalidatePlayerSsrSnapshot()
            => ssrSnapshotMemo.Clear();

        /// <summary>
        /// Maintenance of the player-side skill rows (SSR / pattern / history / Dan) - the writes and identity reads the
        /// archive owns, as opposed to the metric reads the rest of this provider serves. Every write here drops the
        /// memos that would otherwise keep serving the rows it just changed, which is why callers go through this and
        /// not straight to the store.
        /// </summary>
        public PlayerSkillMaintenance PlayerSkills { get; }

        /// <summary>See <see cref="PlayerSkills"/>.</summary>
        public sealed class PlayerSkillMaintenance
        {
            private readonly EzSkillProvider provider;

            internal PlayerSkillMaintenance(EzSkillProvider provider)
            {
                this.provider = provider;
            }

            /// <summary>
            /// Players whose stored rows are flagged as trailing the SQLite slice. The startup reconcile reads this to
            /// decide whose skills to re-derive, and it is the manual compute's default scope.
            /// </summary>
            public IReadOnlyList<string> StaleUsernames => provider.PlayerStore.GetStalePlayerSkillUsernames();

            /// <summary>
            /// Set or clear the stale flag for one player without changing any value. Setting it happens when a
            /// settled play was folded into the SQLite slice (or skipped awaiting the chart chain) and these rows have
            /// not been recomputed since; clearing it is the pass that covered that player saying so.
            /// </summary>
            /// <returns>Number of rows whose flag changed.</returns>
            public int SetStale(string username, bool stale)
            {
                if (string.IsNullOrWhiteSpace(username))
                    return 0;

                int changed = provider.PlayerStore.SetPlayerSkillStale(username, stale);

                provider.InvalidatePlayerSsrSnapshot();

                return changed;
            }

            /// <summary>
            /// Remove every persisted row for one player and drop the memos that would otherwise keep serving them.
            /// Used when the archive no longer covers a player, so no pass can re-derive what their rows say - the
            /// archive-wide <c>All</c> sentinel is dropped the same way once it summarises nobody.
            /// </summary>
            /// <returns>Total number of Realm rows removed.</returns>
            public int Delete(string username)
            {
                if (string.IsNullOrWhiteSpace(username))
                    return 0;

                int removed = provider.PlayerStore.DeletePlayerSkillData(username);

                provider.InvalidatePlayerSsrSnapshot();
                provider.InvalidateDanDisplay();

                return removed;
            }
        }

        public IReadOnlyList<EzPatternRating> GetPlayerPatternRatings(string username, int keyCount)
        {
            var ratings = PlayerStore.GetPlayerPatternRatings(username, keyCount);
            if (ratings.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return ratings;

            return PlayerStore.GetPlayerPatternRatings(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount);
        }

        /// <summary>Hub <c>skillModeEntries</c> for LocalProfile Track / Skills HUD.</summary>
        public IReadOnlyList<EzSkillModeEntry> GetSkillModeEntries(string username, int keyCount)
        {
            var snapshot = GetPlayerSsrSnapshot(username, keyCount);
            var patterns = GetPlayerPatternRatings(username, keyCount);
            return EzPatternRatings.SkillModeEntries(keyCount, snapshot.Values, patterns);
        }

        public IReadOnlyList<int> GetPlayerSsrKeyCounts(string username)
        {
            var keys = PlayerStore.GetPlayerSsrKeyCounts(username);
            if (keys.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return keys;

            return PlayerStore.GetPlayerSsrKeyCounts(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME);
        }

        public IReadOnlyList<EzPlayerSkillHistorySample> GetPlayerSkillHistory(string username, int keyCount, string skillId, int maxPoints = 64)
        {
            var history = PlayerStore.GetPlayerSkillHistory(username, keyCount, skillId, maxPoints);
            if (history.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return history;

            return PlayerStore.GetPlayerSkillHistory(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount, skillId, maxPoints);
        }

        public EzPlayerDanEstimateRow? GetDan(string username, int keyCount, string side)
        {
            string resolvedUser = resolveSkillsUsername(username);
            var estimate = PlayerStore.GetDanEstimate(resolvedUser, keyCount, side);
            if (estimate != null || string.Equals(resolvedUser, username, StringComparison.Ordinal))
                return estimate;

            return PlayerStore.GetDanEstimate(username, keyCount, side);
        }

        /// <summary>
        /// Dan label + rating for DualPanel display, with a short-lived snapshot memo. The Realm row itself must
        /// not outlive its transaction, so only the two plain values the caller renders are cached. Same TTL as
        /// the SSR memo — a switch only needs to read this once instead of once per bindable-driven refresh.
        /// </summary>
        public (string Label, double RawDan) GetDanDisplay(string username, int keyCount, string side)
        {
            var key = (username, keyCount, side);

            if (danDisplayMemo.TryGetValue(key, out var memo))
            {
                if (Stopwatch.GetElapsedTime(memo.Timestamp).TotalSeconds < display_memo_seconds)
                    return (memo.Label, memo.RawDan);

                danDisplayMemo.TryRemove(key, out _);
            }

            var estimate = GetDan(username, keyCount, side);
            var display = estimate is { } row
                ? (row.Label, row.RawDan)
                : (string.Empty, -1d);

            storeBounded(danDisplayMemo, key, new DanDisplayMemo(display.Item1, display.Item2, Stopwatch.GetTimestamp()));
            return display;
        }

        /// <summary>Drop the dan display memo (after a recompute / write).</summary>
        public void InvalidateDanDisplay()
            => danDisplayMemo.Clear();

        /// <summary>
        /// Ordered DualPanel skillset slots for <paramref name="keyCount"/>×<paramref name="side"/> (hub table; may be empty).
        /// </summary>
        public IReadOnlyList<EzDanSkillsetSlot> GetDanSkillsetSlots(int keyCount, EzDanSide side)
            => EzDanSkillsetBuckets.Slots(keyCount, side);

        public IReadOnlyList<EzDanSkillsetSlot> GetDanSkillsetSlots(int keyCount, string sideId)
            => GetDanSkillsetSlots(keyCount, EzDanSideExtensions.ParseOrRc(sideId));

        /// <summary>
        /// Skillset dan verdicts (clear-bucket averages). Prefers the persisted cache; on a miss it computes for this
        /// session only.
        /// </summary>
        /// <remarks>
        /// This is a read path (the HUD calls it while drawing), so it never persists. The durable rows are written by
        /// <see cref="RefreshDanSkillsets"/>, which runs as part of the manual player analysis.
        /// Missing keys = under quorum / no filing data yet.
        /// </remarks>
        public IReadOnlyDictionary<string, EzDanSkillsetVerdict> GetDanSkillsets(string username, int keyCount, string side)
        {
            string resolvedUser = resolveSkillsUsername(username);
            var key = (resolvedUser, keyCount, side);

            // Session memo first. It used to sit behind a Realm existence check, so every HUD refresh paid a
            // transaction even when the answer was already in memory; the memo's lifetime is bounded by the
            // explicit invalidation the writers already perform.
            if (danSkillsetSessionCache.TryGetValue(key, out var memoised))
                return memoised;

            if (PlayerStore.HasDanSkillsetCache(resolvedUser, keyCount, side))
                return memoise(danSkillsetsFromCache(resolvedUser, keyCount, side));

            if (!EzLocalProfileConstants.IsGuestUsername(username)
                && !string.Equals(resolvedUser, username, StringComparison.Ordinal)
                && PlayerStore.HasDanSkillsetCache(username, keyCount, side))
            {
                return memoise(danSkillsetsFromCache(username, keyCount, side));
            }

            var clears = GetDanClears(resolvedUser, keyCount, side, EzDanAlgorithm.VERSION);

            // No clears for current EzDanAlgorithm.VERSION → miss. Memoised as a miss so a HUD redraw does not repeat
            // the query; the manual compute invalidates the memo when it writes clears.
            if (clears.Count == 0)
                return memoise(new Dictionary<string, EzDanSkillsetVerdict>(StringComparer.Ordinal));

            return memoise(computeDanSkillsets(resolvedUser, keyCount, side, allowComputeChart: true));

            IReadOnlyDictionary<string, EzDanSkillsetVerdict> memoise(IReadOnlyDictionary<string, EzDanSkillsetVerdict> value)
            {
                storeBounded(danSkillsetSessionCache, key, value);
                return value;
            }
        }

        /// <summary>Drops the session-only skillset verdis memo.</summary>
        public void InvalidateDanSkillsets()
        {
            danSkillsetSessionCache.Clear();
            InvalidateDanDisplay();
        }

        /// <summary>
        /// Recompute and persist skillset caches for a user after dan clears are replaced.
        /// Prefetches play SSR (axis evidence = hub <c>play.values</c>) and ChartSkillInfo for clear hashes.
        /// Missing ChartSkillInfo is computed (hub load + heal), not left permanently null.
        /// Empty verdicts leave a miss (no empty sentinel write).
        /// </summary>
        public void RefreshDanSkillsets(string username, Action? tick = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            string resolvedUser = resolveSkillsUsername(username);
            PlayerStore.ClearDanSkillsetValues(resolvedUser);
            PlayerStore.ClearDanEstimates(resolvedUser);
            InvalidateDanSkillsets();

            if (!string.Equals(resolvedUser, username, StringComparison.Ordinal))
            {
                PlayerStore.ClearDanSkillsetValues(username);
                PlayerStore.ClearDanEstimates(username);
            }

            var allClears = GetDanClears(resolvedUser, algorithmVersion: EzDanAlgorithm.VERSION);
            var hashes = allClears
                         .Select(c => c.BeatmapHash)
                         .Where(static h => !string.IsNullOrEmpty(h))
                         .Distinct(StringComparer.Ordinal)
                         .ToList();

            var playSsr = EzDanPlayRatingIndex.FromAxisPlays(GetAxisPlays(resolvedUser, algorithmVersion: EzMinaSkillAlgorithm.VERSION));
            var chartByHash = ensureChartSkillInfoForHashes(hashes);

            foreach (int keyCount in tracked_skillset_key_counts)
            {
                foreach (var side in new[] { EzDanSide.Rc, EzDanSide.Ln })
                {
                    string sideId = side.ToId();
                    var clears = filterClears(allClears, keyCount, sideId);

                    if (clears.Count == 0 && EzDanSkillsetBuckets.Slots(keyCount, side).Count == 0)
                    {
                        tick?.Invoke();
                        continue;
                    }

                    var verdicts = EzDanSkillsetBuckets.ComputeFromClears(
                        keyCount,
                        side,
                        clears,
                        playSsr.Resolve,
                        chartByHash.GetValueOrDefault);

                    if (verdicts.Count > 0)
                    {
                        PlayerStore.WriteDanSkillsetVerdicts(resolvedUser, keyCount, sideId, verdicts);
                        writeSideHeadline(resolvedUser, keyCount, side, clears, verdicts);
                    }

                    tick?.Invoke();
                }
            }

            InvalidateDanDisplay();
        }

        /// <summary>
        /// Hub <c>danSideFromClears</c>: skillset anchor/mean fold, else side-wide clear window.
        /// Overwrites <see cref="GetDan"/> so DualPanel titles match filing tiles.
        /// </summary>
        private void writeSideHeadline(
            string username,
            int keyCount,
            EzDanSide side,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> verdicts)
        {
            if (clears.Count < EzDanAlgorithm.CLEAR_QUORUM)
                return;

            var clearDans = clears.Select(c => c.CreditedDan).Where(double.IsFinite).ToList();
            if (clearDans.Count < EzDanAlgorithm.CLEAR_QUORUM)
                return;

            var headline = EzDanSideHeadline.FromSkillsets(keyCount, side, verdicts, clearDans)
                           ?? EzDanSideHeadline.FromSideClears(keyCount, side, clears);

            if (headline == null)
                return;

            var (_, _, have) = EzDanClearWindow.Select(clears);

            PlayerStore.WriteDanEstimate(new EzPlayerDanEstimateRow(
                username,
                keyCount,
                side.ToId(),
                headline.Value.RawDan,
                headline.Value.Label,
                clears.Count,
                headline.Value.BeyondTable,
                string.Empty,
                -1,
                (int)Math.Round(have),
                EzDanAlgorithm.CLEAR_WINDOW,
                EzDanAlgorithm.VERSION,
                DateTimeOffset.UtcNow));
        }

        private static readonly int[] tracked_skillset_key_counts = { 4, 5, 6, 7, 8, 9 };

        private static IReadOnlyList<EzDanClearEvidenceRow> filterClears(
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            int keyCount,
            string sideId)
        {
            var list = new List<EzDanClearEvidenceRow>();

            foreach (var clear in clears)
            {
                if (clear.KeyCount != keyCount)
                    continue;

                if (!string.Equals(clear.Side, sideId, StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(clear);
            }

            return list;
        }

        private IReadOnlyDictionary<string, EzDanSkillsetVerdict> danSkillsetsFromCache(string username, int keyCount, string side)
        {
            var rows = PlayerStore.GetDanSkillsetValues(username, keyCount, side);
            var result = new Dictionary<string, EzDanSkillsetVerdict>(StringComparer.Ordinal);

            foreach (var row in rows)
                result[row.SkillsetId] = new EzDanSkillsetVerdict(row.SkillsetId, row.RawDan, row.Label, row.Clears);

            return result;
        }

        private IReadOnlyDictionary<string, EzDanSkillsetVerdict> computeDanSkillsets(
            string username,
            int keyCount,
            string side,
            bool allowComputeChart)
        {
            var sideEnum = EzDanSideExtensions.ParseOrRc(side);
            var clears = GetDanClears(username, keyCount, side, EzDanAlgorithm.VERSION);

            var hashes = clears
                         .Select(c => c.BeatmapHash)
                         .Where(static h => !string.IsNullOrEmpty(h))
                         .Distinct(StringComparer.Ordinal)
                         .ToList();

            var playSsr = EzDanPlayRatingIndex.FromAxisPlays(GetAxisPlays(username, keyCount, algorithmVersion: EzMinaSkillAlgorithm.VERSION));
            IReadOnlyDictionary<string, EzChartSkillInfo> chartByHash = allowComputeChart
                ? ensureChartSkillInfoForHashes(hashes)
                : ChartStore.GetChartSkillInfoForHashes(hashes);

            return EzDanSkillsetBuckets.ComputeFromClears(
                keyCount,
                sideEnum,
                clears,
                playSsr.Resolve,
                chartByHash.GetValueOrDefault);
        }

        /// <summary>
        /// Hub <c>loadChartSkillInfo</c> + heal missing rows (sync compute; no separate job queue).
        /// DATA-Dan-Skillset-PlaySsr: ChartSkillInfo must exist for tag keymodes before filing.
        /// </summary>
        private Dictionary<string, EzChartSkillInfo> ensureChartSkillInfoForHashes(IReadOnlyList<string> hashes)
        {
            var chartByHash = new Dictionary<string, EzChartSkillInfo>(ChartStore.GetChartSkillInfoForHashes(hashes), StringComparer.Ordinal);

            foreach (string hash in hashes)
            {
                if (chartByHash.TryGetValue(hash, out var existing) && existing is { IsUnavailable: false })
                    continue;

                if (beatmapManager == null || string.IsNullOrEmpty(hash))
                    continue;

                var info = beatmapManager.QueryBeatmap(b => b.Hash == hash);
                if (info == null)
                    continue;

                var computed = TryGetOrComputeChartSkillInfo(info);
                if (computed != null && !computed.IsUnavailable)
                    chartByHash[hash] = computed;
            }

            return chartByHash;
        }

        private string resolveSkillsUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return username;

            if (!EzLocalProfileConstants.IsGuestUsername(username))
                return username;

            var clears = localProfileStore?.GetDanClears(username, algorithmVersion: EzDanAlgorithm.VERSION);
            if (clears is { Count: > 0 })
                return username;

            return EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME;
        }

        /// <summary>
        /// Chart-side skillset labels for one DualPanel side via hub filing
        /// (<see cref="EzDanSkillsetFiling.BucketsForValues"/>). Same aggregate label on every hit bucket
        /// (hub DualPanel-style chart half; not primary-only).
        /// TODO(data): DATA-DualPanel-ChartSkillsetDans — idea: independent chart skillset dans (MSD→SrToRawDan / LeoBlack).
        /// Prefer <see cref="GetChartDanSkillsetLabelsReadOnly"/> on song-select hot paths.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetChartDanSkillsetLabels(
            BeatmapInfo beatmapInfo,
            int keyCount,
            EzDanSide side,
            IReadOnlyList<Mod>? mods = null,
            IBeatmap? playable = null)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (keyCount <= 0 || beatmapInfo.Ruleset.OnlineID != 3)
                return result;

            mods ??= Array.Empty<Mod>();
            float rate = EzModRate.Resolve(mods);

            var msd = GetBeatmapMsd(beatmapInfo.Hash);
            double holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            var chartVerdict = TryGetChartDan(beatmapInfo, mods) ?? TryGetCachedChartDan(beatmapInfo);
            if (holdRatio <= 0 && chartVerdict != null && double.IsFinite(chartVerdict.HoldRatio))
                holdRatio = Math.Clamp(chartVerdict.HoldRatio, 0, 1);

            int holdCount = playable != null
                ? EzChartDanEstimator.ComputeHoldCount(playable)
                : EzChartDanEstimator.TryHoldCountFromBeatmapInfo(beatmapInfo);

            if (side == EzDanSide.Ln
                && !EzDanAlgorithm.AllowsPersistedChartLnHalf(keyCount, holdRatio, holdCount))
            {
                return result;
            }

            // Same priority as DualPanel headline: Sunny/xxy first, then chart verdict label.
            string? aggregateLabel = null;
            double xxy = beatmapInfo.SunnyStarRating;

            // Prefer Sunny for the requested side; primary-side verdict only when sides match.
            if (xxy >= 0 && double.IsFinite(xxy)
                         && EzSunnyDanIntervals.TryLookup(keyCount, side.ToId(), xxy, out var sunny)
                         && !string.IsNullOrEmpty(sunny.DisplayLabel))
            {
                aggregateLabel = sunny.DisplayLabel;
            }
            else if (chartVerdict != null && chartVerdict.Side == side && !string.IsNullOrEmpty(chartVerdict.Label))
            {
                aggregateLabel = chartVerdict.Label;
            }
            else if (chartVerdict != null
                     && EzDanLabels.TryResolveFallbackDan(keyCount, side, chartVerdict.OverallMsd, chartVerdict.DominantAxis, xxy >= 0 ? xxy : null) is EzDanLabels.DanLookup fallback)
            {
                aggregateLabel = fallback.Label;
            }

            if (string.IsNullOrEmpty(aggregateLabel))
                return result;

            var chartInfo = TryGetOrComputeChartSkillInfo(beatmapInfo, playable, mods);
            double? length = chartInfo?.LengthSeconds ?? (beatmapInfo.Length > 0 ? beatmapInfo.Length / 1000.0 : null);

            var buckets = EzDanSkillsetFiling.BucketsForValues(keyCount, side, msd, length, rate, chartInfo);
            foreach (string id in buckets)
                result[id] = aggregateLabel;

            return result;
        }

        /// <summary>
        /// Song-select / DualPanel chart dan for UI.
        /// Order: Realm → session → optional in-memory recompute from MSD+CSI (no disk write).
        /// With <paramref name="allowMemoryCompute"/>, a Realm/session row that is missing an LN/RC half
        /// the current gate would allow is replaced by a fresh memory compute (stale ChartDan after gate changes).
        /// </summary>
        /// <param name="allowMemoryCompute">
        /// When true (DualPanel), miss or incomplete halves may FromMsd+filing into <see cref="chartDanSessionCache"/> only.
        /// When false (carousel panel), never compute — Realm/session hit or empty.
        /// </param>
        public bool TryGetChartDanForUi(BeatmapInfo beatmapInfo, out EzPersistedChartDan? chartDan, bool allowMemoryCompute = false)
        {
            chartDan = null;
            if (beatmapInfo.Ruleset.OnlineID != 3 || string.IsNullOrEmpty(beatmapInfo.Hash))
                return false;

            string hash = beatmapInfo.Hash;

            EzPersistedChartDan? existing = null;

            if (ChartStore.TryGetChartDan(hash, out var fromRealm) && fromRealm != null)
                existing = fromRealm;
            else if (chartDanSessionCache.TryGetValue(hash, out var fromSession))
                existing = fromSession;

            if (allowMemoryCompute)
            {
                var computed = tryComputeChartDanMemoryOnly(beatmapInfo);

                if (computed != null && shouldPreferMemoryChartDanForUi(existing, computed))
                {
                    storeBounded(chartDanSessionCache, hash, computed);
                    chartDan = computed;
                    return true;
                }
            }

            if (existing != null)
            {
                storeBounded(chartDanSessionCache, hash, existing);
                chartDan = existing;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Prefer a memory recompute when it fills an LN/RC half that the cached row lacks
        /// (e.g. ChartDan written under the old ratio-only LN gate).
        /// </summary>
        private static bool shouldPreferMemoryChartDanForUi(EzPersistedChartDan? existing, EzPersistedChartDan computed)
        {
            if (existing == null)
                return true;

            if (computed.HasSide(EzDanSide.Ln) && !existing.HasSide(EzDanSide.Ln))
                return true;

            if (computed.HasSide(EzDanSide.Rc) && !existing.HasSide(EzDanSide.Rc))
                return true;

            return false;
        }

        /// <summary>Current-version Realm ChartDan row (nomod). Also warms session cache. Miss when absent or algorithm mismatch.</summary>
        public bool TryGetPersistedChartDan(BeatmapInfo beatmapInfo, out EzPersistedChartDan? chartDan)
            => TryGetChartDanForUi(beatmapInfo, out chartDan, allowMemoryCompute: false);

        /// <summary>
        /// Song-select / DualPanel: labels from <see cref="TryGetChartDanForUi"/> (Realm or session memory).
        /// Never sync Mina / LeoBlack / GetPlayable; never Upsert Realm from this path.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetChartDanSkillsetLabelsReadOnly(
            BeatmapInfo beatmapInfo,
            int keyCount,
            EzDanSide side,
            bool allowMemoryCompute = false)
        {
            if (keyCount <= 0 || beatmapInfo.Ruleset.OnlineID != 3)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            if (TryGetChartDanForUi(beatmapInfo, out var row, allowMemoryCompute) && row != null)
                return row.SkillsetLabelsFor(side);

            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private EzPersistedChartDan? tryComputeChartDanMemoryOnly(BeatmapInfo beatmapInfo)
        {
            var msd = GetBeatmapMsd(beatmapInfo.Hash);
            if (!EzBeatmapMinaComputer.IsCurrentMinaCache(msd))
                return null;

            if (!EzChartDanEstimator.TryResolveStoredInputs(beatmapInfo, out int keyCount, out double holdRatio, out double? xxySr, out int holdCount))
                return null;

            EzChartSkillInfo? chartInfo = null;

            if (ChartStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var storedCsi) && storedCsi is { IsUnavailable: false })
                chartInfo = storedCsi;

            return EzPersistedChartDan.TryComputeFromStored(
                beatmapInfo.Hash,
                beatmapInfo.ID,
                msd,
                keyCount,
                holdRatio,
                xxySr,
                chartInfo,
                holdCount);
        }

        /// <summary>Backward-compatible overload: RC-side labels only.</summary>
        public IReadOnlyDictionary<string, string> GetChartDanSkillsetLabels(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            int keyCount = EzHoldMetrics.ResolveKeyCount(beatmapInfo);

            return GetChartDanSkillsetLabels(beatmapInfo, keyCount, EzDanSide.Rc, mods);
        }

        /// <summary>Realm ChartSkillInfo only (no playable / compute). Miss stays null.</summary>
        public bool TryGetStoredChartSkillInfo(BeatmapInfo beatmapInfo, out EzChartSkillInfo? info)
        {
            info = null;

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return false;

            if (!ChartStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) || stored == null || stored.IsUnavailable)
                return false;

            info = stored;
            return true;
        }

        /// <summary>
        /// Stored chart skill info, or a session-only compute when a playable (or WorkingBeatmap) is available.
        /// <para>
        /// Read paths never persist: durable rows are owned by the background backfill, which is the only writer
        /// that can stamp a settled stub. A computed row lives in memory until it is invalidated.
        /// </para>
        /// </summary>
        public EzChartSkillInfo? TryGetOrComputeChartSkillInfo(
            BeatmapInfo beatmapInfo,
            IBeatmap? playable = null,
            IReadOnlyList<Mod>? mods = null)
        {
            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            // The live analysis already ran the structure pass for these inputs; reuse its typed summary instead of
            // running the extractor and pattern analyser a second time.
            if (liveCache.Get(new EzAnalysisLiveCache.AnalysisKey(beatmapInfo.Hash, beatmapInfo.Ruleset.OnlineID, EzAnalysisLookupCache.ComputeModsSignature(mods)))
                    ?.ChartSkillInfo is { IsUnavailable: false } liveInfo)
            {
                return liveInfo;
            }

            if (ChartStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) && stored != null)
            {
                if (!stored.IsUnavailable)
                    return stored;

                if (playable == null)
                    return null;
            }

            // Session miss: retry when caller supplies playable; otherwise leave miss.
            if (playable == null && chartSkillInfoSessionMisses.Contains(beatmapInfo.Hash))
                return null;

            try
            {
                mods ??= Array.Empty<Mod>();

                // A chart whose MSD settled as unrateable is settled for the whole chain, so no CSI is derived from
                // it here. BDSP owns replacing a row an earlier pass stamped from the stub axis with the stub form,
                // which is why this path only declines to compute.
                if (isSettledUnrateableMina(beatmapInfo.Hash))
                {
                    recordChartSkillInfoSessionMiss(beatmapInfo.Hash);
                    return null;
                }

                IBeatmap? map = playable;

                if (map == null && beatmapManager != null)
                {
                    var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                    map = EzPlayableBeatmapCache.GetShared(working, beatmapInfo.Ruleset, mods);
                }

                if (map == null)
                {
                    recordChartSkillInfoSessionMiss(beatmapInfo.Hash);
                    return null;
                }

                var input = EzChartSkillInfoComputer.FromPlayable(map);
                // Motion / pattern shares are rate-invariant; always measure at 1.0x like hub.
                var info = EzChartSkillInfoComputer.Compute(input, rate: 1);
                chartSkillInfoSessionMisses.Remove(beatmapInfo.Hash);
                // Live snapshots built before this CSI existed must not be served as cache hits.
                InvalidateLiveMetrics(beatmapInfo.Hash);
                return info;
            }
            catch
            {
                recordChartSkillInfoSessionMiss(beatmapInfo.Hash);
                return null;
            }
        }

        /// <summary>
        /// BDSP-facing CSI ensure for one chart. Returns true when the hash is settled afterwards
        /// (a current-revision row, a fresh compute, or a stub because the chart cannot be loaded or its
        /// MSD settled as unrateable), false when the failure is transient and should be retried on a later run.
        /// <para>
        /// This is deliberately the only path that stamps a stub: a hot-path read never persists a
        /// failure, so a transient miss from song select cannot permanently retire a chart.
        /// </para>
        /// </summary>
        public bool TryEnsureChartSkillInfoForBackfill(BeatmapInfo beatmapInfo)
        {
            if (beatmapInfo.Ruleset.OnlineID != 3)
                return true;

            // MSD settled as unrateable: the chart is settled for the whole chain, so the stub form is what belongs
            // here. A complete row in that state was derived from the stub axis by an earlier pass and is replaced.
            if (isSettledUnrateableMina(beatmapInfo.Hash))
            {
                if (!ChartStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var settled) || settled == null || !settled.IsUnavailable)
                {
                    Logger.Log($"[EzSkills] CSI not derived for {beatmapInfo}: MSD settled as unrateable; settling as unavailable.",
                        Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    ChartStore.WriteChartSkillInfoUnavailable(beatmapInfo.Hash, beatmapInfo.ID);
                    InvalidateLiveMetrics(beatmapInfo.Hash);
                }

                return true;
            }

            // Complete row, or a stub a previous backfill already settled.
            if (ChartStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) && stored != null)
            {
                InvalidateLiveMetrics(beatmapInfo.Hash);
                return true;
            }

            IBeatmap map;

            try
            {
                if (beatmapManager == null)
                    return false;

                map = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                // Content-addressed: the file backing this hash will not become loadable on its own, so
                // settle it rather than re-converting it on every backfill. Re-importing the chart
                // produces a new hash, and 「完全重算」 clears the stub, so this is recoverable.
                Logger.Log($"[EzSkills] CSI backfill cannot load {beatmapInfo} ({e.Message}); settling as unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                ChartStore.WriteChartSkillInfoUnavailable(beatmapInfo.Hash, beatmapInfo.ID);
                InvalidateLiveMetrics(beatmapInfo.Hash);
                return true;
            }

            try
            {
                var input = EzChartSkillInfoComputer.FromPlayable(map);
                // Motion / pattern shares are rate-invariant; always measure at 1.0x like hub.
                var axes = EzChartSkillInfoComputer.ComputeAxes(input, out var info);
                ChartStore.UpsertChartSkillInfo(beatmapInfo.Hash, info, beatmapInfo.ID);
                // The typed row carries the tags; the axis table is what a reader draws, so both are written here,
                // by the one pass that measured them.
                ChartStore.WriteBeatmapSystemSkills(
                    beatmapInfo.Hash,
                    EzSkillSystems.BEATMAP_CSI,
                    axes,
                    beatmapInfo.ID,
                    algorithmVersion: EzAnalysisRevision.ChartSkillInfo,
                    rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);

                chartSkillInfoSessionMisses.Remove(beatmapInfo.Hash);
                // Stamped live snapshots built before this CSI existed must not be served as cache hits.
                InvalidateLiveMetrics(beatmapInfo.Hash);
                return true;
            }
            catch (Exception e)
            {
                // Analysis-side failure: leave it unstamped so the next backfill retries it.
                Logger.Log($"[EzSkills] CSI backfill analysis failed for {beatmapInfo} ({e.Message}); will retry.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                recordChartSkillInfoSessionMiss(beatmapInfo.Hash);
                return false;
            }
        }

        /// <summary>
        /// Chart-side dan for song select me-vs-chart. Null when estimator unset or chart cannot be rated.
        /// </summary>
        public EzChartDanVerdict? TryGetChartDan(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
            => chartDanEstimator?.TryEstimate(beatmapInfo, mods);

        /// <summary>Dan clear evidence (sqlite). Empty when local-profile store unset.</summary>
        public IReadOnlyList<EzDanClearEvidenceRow> GetDanClears(string username, int? keyCount = null, string? side = null, int? algorithmVersion = null)
        {
            var rows = localProfileStore?.GetDanClears(username, keyCount, side, algorithmVersion) ?? [];
            if (rows.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username) || localProfileStore == null)
                return rows;

            return localProfileStore.GetDanClears(
                EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME,
                keyCount,
                side,
                algorithmVersion);
        }

        /// <summary>SSR axis play evidence (sqlite). Empty when local-profile store unset.</summary>
        public IReadOnlyList<EzAxisPlayEvidenceRow> GetAxisPlays(string username, int? keyCount = null, string? skillId = null, int? algorithmVersion = null)
        {
            var rows = localProfileStore?.GetAxisPlays(username, keyCount, skillId, algorithmVersion) ?? [];
            if (rows.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username) || localProfileStore == null)
                return rows;

            return localProfileStore.GetAxisPlays(
                EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME,
                keyCount,
                skillId,
                algorithmVersion);
        }
    }
}
