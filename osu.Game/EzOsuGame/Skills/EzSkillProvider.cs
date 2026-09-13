// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Skills.Dan;
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
        private readonly EzSkillStore store;
        private readonly EzChartDanEstimator? chartDanEstimator;
        private readonly EzLocalProfileStore? localProfileStore;
        private readonly BeatmapManager? beatmapManager;

        /// <summary>Session-only CSI compute failures — keep miss in Realm so BDSP can retry next launch.</summary>
        private readonly HashSet<string> chartSkillInfoSessionMisses = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Session MSD cache (complete rows only). Not written to disk.</summary>
        private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, double>> msdSessionCache
            = new ConcurrentDictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);

        /// <summary>
        /// Session ChartDan cache. May hold Realm copies or DualPanel memory-only computes (never auto-upserted).
        /// </summary>
        private readonly ConcurrentDictionary<string, EzPersistedChartDan> chartDanSessionCache
            = new ConcurrentDictionary<string, EzPersistedChartDan>(StringComparer.Ordinal);

        private const int live_chart_skills_cache_limit = 128;

        /// <summary>
        /// Short-lived player SSR memo (see <see cref="GetPlayerSsrSnapshot"/>). Keyed by (username, keyCount).
        /// </summary>
        private readonly ConcurrentDictionary<(string Username, int KeyCount), SsrSnapshotMemo> ssrSnapshotMemo
            = new ConcurrentDictionary<(string Username, int KeyCount), SsrSnapshotMemo>();

        private readonly record struct SsrSnapshotMemo(EzPlayerSsrSnapshot Snapshot, long Timestamp);

        /// <summary>Short-lived dan label/rating memo (see <see cref="GetDanDisplay"/>); plain values only.</summary>
        private readonly ConcurrentDictionary<(string Username, int KeyCount, string Side), DanDisplayMemo> danDisplayMemo
            = new ConcurrentDictionary<(string Username, int KeyCount, string Side), DanDisplayMemo>();

        private readonly record struct DanDisplayMemo(string Label, double RawDan, long Timestamp);

        private const double display_memo_seconds = 2;

        /// <summary>
        /// Session live-chart-skills dedupe. The value is the in-flight task (not a finished result) so that
        /// surfaces which all kick on the same song switch (Skill radar + DualPanel) share one playable +
        /// Mina/xxy + pattern pass instead of each racing an identical one. Keyed by inputs that determine
        /// the result; keyCount is deliberately not part of it (it is derived from the playable, not an input).
        /// </summary>
        private readonly ConcurrentDictionary<EzLiveChartSkillsKey, Task<EzLiveChartSkillSnapshot?>> liveChartSkillsCache
            = new ConcurrentDictionary<EzLiveChartSkillsKey, Task<EzLiveChartSkillSnapshot?>>();

        private readonly record struct EzLiveChartSkillsKey(string BeatmapHash, int RulesetOnlineId, int ModsSignature);

        public EzSkillProvider(
            EzSkillStore store,
            EzSkillRegistry? registry = null,
            EzChartDanEstimator? chartDanEstimator = null,
            EzLocalProfileStore? localProfileStore = null,
            BeatmapManager? beatmapManager = null)
        {
            this.store = store;
            this.chartDanEstimator = chartDanEstimator;
            this.localProfileStore = localProfileStore;
            this.beatmapManager = beatmapManager;
            Registry = registry ?? new EzSkillRegistry();
        }

        public EzSkillRegistry Registry { get; }

        public IReadOnlyDictionary<string, double> GetBeatmapMsd(string beatmapHash)
        {
            if (string.IsNullOrEmpty(beatmapHash))
                return new Dictionary<string, double>(StringComparer.Ordinal);

            if (msdSessionCache.TryGetValue(beatmapHash, out var cached))
                return cached;

            var msd = store.GetBeatmapSkills(beatmapHash, EzSkillSystems.BEATMAP_MSD);
            if (EzBeatmapMsdComputer.IsCurrentMsdCache(msd))
                msdSessionCache[beatmapHash] = msd;

            return msd;
        }

        /// <summary>Drop session MSD entry so the next read refreshes from Realm (e.g. after WriteBeatmapMsd).</summary>
        public void InvalidateMsdSession(string beatmapHash)
        {
            if (!string.IsNullOrEmpty(beatmapHash))
                msdSessionCache.TryRemove(beatmapHash, out _);
        }

        /// <summary>Drop session ChartDan entry (e.g. after Realm UpsertChartDan).</summary>
        public void InvalidateChartDanSession(string beatmapHash)
        {
            if (!string.IsNullOrEmpty(beatmapHash))
                chartDanSessionCache.TryRemove(beatmapHash, out _);
        }

        /// <summary>
        /// Drop session live-chart-skills entries. Pass a hash after CSI heal / ChartDan upsert so a snapshot
        /// stamped before the heal is not served as a cache hit; omit to clear everything.
        /// </summary>
        public void InvalidateLiveChartSkillsCache(string? beatmapHash = null)
        {
            if (string.IsNullOrEmpty(beatmapHash))
            {
                liveChartSkillsCache.Clear();
                return;
            }

            foreach (var key in liveChartSkillsCache.Keys)
            {
                if (string.Equals(key.BeatmapHash, beatmapHash, StringComparison.Ordinal))
                    liveChartSkillsCache.TryRemove(key, out _);
            }
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

            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            if (keyCount <= 0)
                keyCount = 4;

            double holdRatio = 0;
            if (msd.TryGetValue(EzSkillSystems.MsdHoldRatioSkillId, out double cachedHold) && double.IsFinite(cachedHold))
                holdRatio = Math.Clamp(cachedHold, 0, 1);

            double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;
            return EzChartDanEstimator.FromMsd(msd, keyCount, holdRatio, xxySr);
        }

        public bool TryGetBeatmapMsdSkill(string beatmapHash, string axisId, out double value)
        {
            string skillId = EzMinaSkillAxisExtensions.TryParse(axisId, out var axis)
                ? axis.ToMsdSkillId()
                : axisId;
            return store.TryGetBeatmapSkill(beatmapHash, skillId, out value);
        }

        public IReadOnlyDictionary<string, double> GetPlayerSsr(string username, int keyCount)
            => GetPlayerSsrSnapshot(username, keyCount).Values;

        /// <summary>
        /// Player SSR snapshot with a short-lived memo. Song select re-reads this several times per switch
        /// (once per bindable-driven DualPanel/radar refresh); the TTL collapses those into one Realm read.
        /// Mirrors the other session caches here — a fresh compute becomes visible within the TTL at worst.
        /// </summary>
        public EzPlayerSsrSnapshot GetPlayerSsrSnapshot(string username, int keyCount)
        {
            var key = (username, keyCount);

            if (ssrSnapshotMemo.TryGetValue(key, out var memo)
                && Stopwatch.GetElapsedTime(memo.Timestamp).TotalSeconds < display_memo_seconds)
            {
                return memo.Snapshot;
            }

            var snapshot = loadPlayerSsrSnapshot(username, keyCount);
            ssrSnapshotMemo[key] = new SsrSnapshotMemo(snapshot, Stopwatch.GetTimestamp());
            return snapshot;
        }

        private EzPlayerSsrSnapshot loadPlayerSsrSnapshot(string username, int keyCount)
        {
            var snapshot = store.GetPlayerSsrSnapshot(username, keyCount);
            if (snapshot.Values.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return snapshot;

            return store.GetPlayerSsrSnapshot(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount);
        }

        /// <summary>Drop the SSR snapshot memo (after a recompute / write).</summary>
        public void InvalidatePlayerSsrSnapshot()
            => ssrSnapshotMemo.Clear();

        public IReadOnlyList<EzPatternRating> GetPlayerPatternRatings(string username, int keyCount)
        {
            var ratings = store.GetPlayerPatternRatings(username, keyCount);
            if (ratings.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return ratings;

            return store.GetPlayerPatternRatings(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount);
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
            var keys = store.GetPlayerSsrKeyCounts(username);
            if (keys.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return keys;

            return store.GetPlayerSsrKeyCounts(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME);
        }

        public IReadOnlyList<EzPlayerSkillHistoryPoint> GetPlayerSkillHistory(string username, int keyCount, string skillId, int maxPoints = 64)
        {
            var history = store.GetPlayerSkillHistory(username, keyCount, skillId, maxPoints);
            if (history.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return history;

            return store.GetPlayerSkillHistory(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount, skillId, maxPoints);
        }

        public EzDanEstimate? GetDan(string username, int keyCount, string side)
        {
            string resolvedUser = resolveSkillsUsername(username);
            var estimate = store.GetDanEstimate(resolvedUser, keyCount, side);
            if (estimate != null || string.Equals(resolvedUser, username, StringComparison.Ordinal))
                return estimate;

            return store.GetDanEstimate(username, keyCount, side);
        }

        /// <summary>
        /// Dan label + rating for DualPanel display, with a short-lived snapshot memo. The Realm row itself must
        /// not outlive its transaction, so only the two plain values the caller renders are cached. Same TTL as
        /// the SSR memo — a switch only needs to read this once instead of once per bindable-driven refresh.
        /// </summary>
        public (string Label, double RawDan) GetDanDisplay(string username, int keyCount, string side)
        {
            var key = (username, keyCount, side);

            if (danDisplayMemo.TryGetValue(key, out var memo)
                && Stopwatch.GetElapsedTime(memo.Timestamp).TotalSeconds < display_memo_seconds)
            {
                return (memo.Label, memo.RawDan);
            }

            var estimate = GetDan(username, keyCount, side);
            var display = estimate != null
                ? (estimate.Label, estimate.RawDan)
                : (string.Empty, -1d);

            danDisplayMemo[key] = new DanDisplayMemo(display.Item1, display.Item2, Stopwatch.GetTimestamp());
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
        /// Skillset dan verdicts (clear-bucket averages). Prefers Realm cache; miss → compute when clears exist.
        /// Only positive verdicts are persisted (no empty sentinel). Missing keys = under quorum / no filing data yet.
        /// </summary>
        public IReadOnlyDictionary<string, EzDanSkillsetVerdict> GetDanSkillsets(string username, int keyCount, string side)
        {
            string resolvedUser = resolveSkillsUsername(username);

            if (store.HasDanSkillsetCache(resolvedUser, keyCount, side))
                return danSkillsetsFromCache(resolvedUser, keyCount, side);

            if (!EzLocalProfileConstants.IsGuestUsername(username)
                && !string.Equals(resolvedUser, username, StringComparison.Ordinal)
                && store.HasDanSkillsetCache(username, keyCount, side))
            {
                return danSkillsetsFromCache(username, keyCount, side);
            }

            var clears = GetDanClears(resolvedUser, keyCount, side, EzDanAlgorithm.VERSION);

            // No clears for current EzDanAlgorithm.VERSION → keep miss (do not write empty).
            if (clears.Count == 0)
                return new Dictionary<string, EzDanSkillsetVerdict>(StringComparer.Ordinal);

            var verdicts = computeDanSkillsets(resolvedUser, keyCount, side, allowComputeChart: true);

            if (verdicts.Count > 0)
            {
                store.WriteDanSkillsetVerdicts(resolvedUser, keyCount, side, verdicts);
                writeSideHeadline(resolvedUser, keyCount, EzDanSideExtensions.ParseOrRc(side), clears, verdicts);
                InvalidateDanDisplay();
            }

            return verdicts;
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
            store.ClearDanSkillsetValues(resolvedUser);
            store.ClearDanEstimates(resolvedUser);

            if (!string.Equals(resolvedUser, username, StringComparison.Ordinal))
            {
                store.ClearDanSkillsetValues(username);
                store.ClearDanEstimates(username);
            }

            var allClears = GetDanClears(resolvedUser, algorithmVersion: EzDanAlgorithm.VERSION);
            var hashes = allClears
                         .Select(c => c.BeatmapHash)
                         .Where(static h => !string.IsNullOrEmpty(h))
                         .Distinct(StringComparer.Ordinal)
                         .ToList();

            var playSsr = EzDanPlaySsrIndex.FromAxisPlays(GetAxisPlays(resolvedUser, algorithmVersion: EzManiaSkillAlgorithm.VERSION));
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
                        store.WriteDanSkillsetVerdicts(resolvedUser, keyCount, sideId, verdicts);
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

            store.WriteDanEstimate(new EzDanEstimate
            {
                Username = username,
                KeyCount = keyCount,
                Side = side.ToId(),
                RawDan = headline.Value.RawDan,
                Label = headline.Value.Label,
                Clears = clears.Count,
                BeyondTable = headline.Value.BeyondTable,
                ClearWindowHave = (int)Math.Round(have),
                ClearWindowNeed = EzDanAlgorithm.CLEAR_WINDOW,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
                ComputedAt = DateTimeOffset.UtcNow,
            });
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
            var rows = store.GetDanSkillsetValues(username, keyCount, side);
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

            var playSsr = EzDanPlaySsrIndex.FromAxisPlays(GetAxisPlays(username, keyCount, algorithmVersion: EzManiaSkillAlgorithm.VERSION));
            IReadOnlyDictionary<string, EzChartSkillInfo> chartByHash = allowComputeChart
                ? ensureChartSkillInfoForHashes(hashes)
                : store.GetChartSkillInfoForHashes(hashes);

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
            var chartByHash = new Dictionary<string, EzChartSkillInfo>(store.GetChartSkillInfoForHashes(hashes), StringComparer.Ordinal);

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
            double holdRatio = 0;
            if (msd.TryGetValue(EzSkillSystems.MsdHoldRatioSkillId, out double cachedHold) && double.IsFinite(cachedHold))
                holdRatio = Math.Clamp(cachedHold, 0, 1);

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
            double xxy = beatmapInfo.XxyStarRating;

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

            if (store.TryGetChartDan(hash, out var fromRealm) && fromRealm != null)
                existing = fromRealm;
            else if (chartDanSessionCache.TryGetValue(hash, out var fromSession))
                existing = fromSession;

            if (allowMemoryCompute)
            {
                var computed = tryComputeChartDanMemoryOnly(beatmapInfo);

                if (computed != null && shouldPreferMemoryChartDanForUi(existing, computed))
                {
                    chartDanSessionCache[hash] = computed;
                    chartDan = computed;
                    return true;
                }
            }

            if (existing != null)
            {
                chartDanSessionCache[hash] = existing;
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

        /// <summary>
        /// Temporary chart MSD + ChartDan from playable for the selected beatmap (xxySR-style live overlay).
        /// Always computes — including empty mods. Never writes Realm.
        /// <para>
        /// Session-deduped: the heavy pass (playable + Mina/xxy + pattern) is shared per
        /// (beatmap, ruleset, mods) for the session, so the Skill radar and the DualPanel no longer each run an
        /// identical compute on the same song switch. Prefer <see cref="GetOrComputeLiveChartSkillsAsync"/>;
        /// this blocking overload exists for callers already on a background thread.
        /// </para>
        /// </summary>
        public EzLiveChartSkillSnapshot? TryComputeLiveChartSkills(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods)
            => GetOrComputeLiveChartSkillsAsync(beatmapInfo, mods, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronous, session-deduped live chart skills. Concurrent callers for the same chart + mods await a
        /// single in-flight compute; <paramref name="cancellationToken"/> only abandons this caller's wait (the
        /// shared compute is owned by whichever caller created it, so cancelling one surface cannot tear down
        /// work another is still waiting on).
        /// </summary>
        public async Task<EzLiveChartSkillSnapshot?> GetOrComputeLiveChartSkillsAsync(
            BeatmapInfo beatmapInfo,
            IReadOnlyList<Mod>? mods,
            CancellationToken cancellationToken = default)
        {
            if (chartDanEstimator == null || beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var key = new EzLiveChartSkillsKey(beatmapInfo.Hash, beatmapInfo.Ruleset.OnlineID, EzAnalysisLookupCache.ComputeModsSignature(mods));

            if (!liveChartSkillsCache.TryGetValue(key, out var task))
            {
                // The compute runs with the creator's token so a chart that is switched away from does not keep
                // burning CPU; it never writes Realm, so an abandoned run is harmless.
                var created = Task.Run(() => computeLiveChartSkills(beatmapInfo, mods, cancellationToken), CancellationToken.None);
                task = liveChartSkillsCache.GetOrAdd(key, created);

                if (ReferenceEquals(task, created))
                {
                    trimLiveChartSkillsCache();

                    // A failed / cancelled compute must not be served as a cache hit later.
                    // NotOnRanToCompletion covers both faults and cancellations (the OnlyOn* options are
                    // mutually exclusive and cannot be OR-ed together). TryRemove(KeyValuePair) also only
                    // drops the entry if it is still this exact task.
                    _ = task.ContinueWith(
                        completed =>
                        {
                            // Read the aggregate so an abandoned failure/cancellation is marked observed
                            // instead of surfacing as UnobservedTaskException when no surface awaits it.
                            _ = completed.Exception;

                            liveChartSkillsCache.TryRemove(new KeyValuePair<EzLiveChartSkillsKey, Task<EzLiveChartSkillSnapshot?>>(key, task));
                        },
                        CancellationToken.None, TaskContinuationOptions.NotOnRanToCompletion, TaskScheduler.Default);
                }
            }

            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private void trimLiveChartSkillsCache()
        {
            // Song select walks a very large number of charts; the cache only needs to cover the recent swing.
            if (liveChartSkillsCache.Count > live_chart_skills_cache_limit)
                liveChartSkillsCache.Clear();
        }

        private EzLiveChartSkillSnapshot? computeLiveChartSkills(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods, CancellationToken cancellationToken)
        {
            var estimator = chartDanEstimator;

            if (estimator == null)
                return null;

            int csKeys = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            EzChartSkillInfo? chartInfo = null;

            if (store.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) && stored is { IsUnavailable: false })
                chartInfo = stored;

            // First pass without CSI if we cannot know key match yet — compute then stamp.
            var snap = estimator.TryComputeLiveSnapshot(beatmapInfo, mods, chartInfo: null, cancellationToken);
            if (snap == null)
                return null;

            if (chartInfo != null && csKeys > 0 && snap.KeyCount == csKeys)
            {
                double? xxySr = snap.XxySr
                                ?? (EzModRate.CanUsePersistedXxy(mods) && beatmapInfo.XxyStarRating >= 0
                                    ? beatmapInfo.XxyStarRating
                                    : null);

                var stamped = EzPersistedChartDan.TryComputeFromStored(
                    beatmapInfo.Hash,
                    beatmapInfo.ID,
                    snap.Msd,
                    snap.KeyCount,
                    snap.HoldRatio,
                    xxySr,
                    chartInfo,
                    snap.HoldCount,
                    allowMsdHeuristicLabels: xxySr != null);

                if (stamped != null)
                {
                    return new EzLiveChartSkillSnapshot
                    {
                        Msd = snap.Msd,
                        ChartDan = stamped,
                        KeyCount = snap.KeyCount,
                        HoldRatio = snap.HoldRatio,
                        HoldCount = snap.HoldCount,
                        XxySr = xxySr,
                        IsLiveFromMods = true,
                        LnMetrics = snap.LnMetrics,
                        LnSubtypeScores = snap.LnSubtypeScores,
                        RcPatternScores = snap.RcPatternScores,
                    };
                }
            }

            return snap;
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
            if (!EzBeatmapMsdComputer.IsCurrentMsdCache(msd))
                return null;

            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            if (keyCount <= 0)
                keyCount = 4;

            double holdRatio = 0;
            if (msd.TryGetValue(EzSkillSystems.MsdHoldRatioSkillId, out double cachedHold) && double.IsFinite(cachedHold))
                holdRatio = Math.Clamp(cachedHold, 0, 1);

            EzChartSkillInfo? chartInfo = null;

            if (store.TryGetChartSkillInfo(beatmapInfo.Hash, out var storedCsi) && storedCsi is { IsUnavailable: false })
            {
                chartInfo = storedCsi;
                if (holdRatio <= 0 && chartInfo.LnRatio is double lnRatio && double.IsFinite(lnRatio))
                    holdRatio = Math.Clamp(lnRatio, 0, 1);
            }

            double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;

            int holdCount = EzChartDanEstimator.TryHoldCountFromBeatmapInfo(beatmapInfo);

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
            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            if (keyCount <= 0)
                keyCount = 4;

            return GetChartDanSkillsetLabels(beatmapInfo, keyCount, EzDanSide.Rc, mods);
        }

        /// <summary>Realm ChartSkillInfo only (no playable / compute). Miss stays null.</summary>
        public bool TryGetStoredChartSkillInfo(BeatmapInfo beatmapInfo, out EzChartSkillInfo? info)
        {
            info = null;

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return false;

            if (!store.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) || stored == null || stored.IsUnavailable)
                return false;

            info = stored;
            return true;
        }

        /// <summary>
        /// Stored chart skill info, or compute+upsert when a playable (or WorkingBeatmap) is available.
        /// Failures stay Realm miss (no Unavailable stub). Session set skips hot-path retries without playable.
        /// </summary>
        public EzChartSkillInfo? TryGetOrComputeChartSkillInfo(
            BeatmapInfo beatmapInfo,
            IBeatmap? playable = null,
            IReadOnlyList<Mod>? mods = null)
        {
            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            if (store.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) && stored != null && !stored.IsUnavailable)
                return stored;

            // Existing Unavailable stub (or miss): retry when caller supplies playable; otherwise leave miss.
            if (playable == null && chartSkillInfoSessionMisses.Contains(beatmapInfo.Hash))
                return null;

            try
            {
                mods ??= Array.Empty<Mod>();
                IBeatmap? map = playable;

                if (map == null && beatmapManager != null)
                {
                    var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                    map = working.GetPlayableBeatmap(beatmapInfo.Ruleset, mods);
                }

                if (map == null)
                {
                    chartSkillInfoSessionMisses.Add(beatmapInfo.Hash);
                    return null;
                }

                var input = EzChartSkillInfoComputer.FromPlayable(map);
                var msd = GetBeatmapMsd(beatmapInfo.Hash);
                // Motion / pattern shares are rate-invariant; always measure at 1.0x like hub.
                var info = EzChartSkillInfoComputer.Compute(input, msd.Count > 0 ? msd : null, rate: 1);
                store.UpsertChartSkillInfo(beatmapInfo.Hash, info, beatmapInfo.ID);
                chartSkillInfoSessionMisses.Remove(beatmapInfo.Hash);
                // Stamped live snapshots built before this CSI existed must not be served as cache hits.
                InvalidateLiveChartSkillsCache(beatmapInfo.Hash);
                return info;
            }
            catch
            {
                chartSkillInfoSessionMisses.Add(beatmapInfo.Hash);
                return null;
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
