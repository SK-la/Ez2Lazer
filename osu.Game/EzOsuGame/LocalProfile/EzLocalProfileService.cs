// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// DI façade for the single shared local profile store and aggregation.
    /// </summary>
    public class EzLocalProfileService : IDisposable
    {
        private const int yield_every = 32;

        private readonly EzLocalProfileAggregator aggregator;
        private readonly EzPlayerSsrAggregator? ssrAggregator;
        private readonly EzPlayerDanAggregator? danAggregator;
        private readonly EzSkillProvider? skillProvider;
        private readonly Lock computeLock = new Lock();
        private readonly Lock sessionCacheLock = new Lock();
        private CancellationTokenSource? computeCts;

        private readonly Dictionary<string, IReadOnlyList<EzLocalProfileDrillScoreRow>> drillCache =
            new Dictionary<string, IReadOnlyList<EzLocalProfileDrillScoreRow>>(StringComparer.Ordinal);

        private readonly Dictionary<string, EzLocalProfileSnapshot> displaySnapshotCache =
            new Dictionary<string, EzLocalProfileSnapshot>(StringComparer.Ordinal);

        private readonly Dictionary<string, EzLocalProfileInsights> insightsMemoryCache =
            new Dictionary<string, EzLocalProfileInsights>(StringComparer.Ordinal);

        private readonly Dictionary<Guid, IReadOnlyList<double>> kpsCache = new Dictionary<Guid, IReadOnlyList<double>>();

        public Bindable<EzLocalProfileSnapshot> Snapshot { get; } = new Bindable<EzLocalProfileSnapshot>(new EzLocalProfileSnapshot());

        public BindableBool IsComputing { get; } = new BindableBool();

        public EzLocalProfileService(
            Storage storage,
            RealmAccess realm,
            EzAnalysisPersistentStore analysisStore,
            BeatmapManager beatmapManager,
            EzPlayerSsrAggregator? ssrAggregator = null,
            EzPlayerDanAggregator? danAggregator = null,
            EzLocalProfileStore? sharedStore = null,
            EzSkillProvider? skillProvider = null)
        {
            Store = sharedStore ?? new EzLocalProfileStore(storage);
            aggregator = new EzLocalProfileAggregator(realm, analysisStore, beatmapManager);
            this.ssrAggregator = ssrAggregator;
            this.danAggregator = danAggregator;
            this.skillProvider = skillProvider;
            Snapshot.Value = Store.LoadSnapshot();
        }

        /// <summary>Shared store for DI (e.g. <see cref="EzSkillProvider"/> evidence reads).</summary>
        public EzLocalProfileStore Store { get; }

        public IReadOnlyList<EzLocalProfileUsernameCount> ScanUsernameCounts() => aggregator.ScanUsernameCounts();

        public IReadOnlyList<string> GetPreviouslyIncludedUsernames()
            => Store.LoadIncludedUsernames()
                    .Select(EzLocalProfileConstants.NormaliseUsername)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

        /// <summary>
        /// Display snapshot for the player filter: <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> (or null/blank)
        /// or one stored username. Guest also falls back to legacy <c>(unknown)</c> partitions.
        /// </summary>
        public EzLocalProfileSnapshot LoadDisplaySnapshot(string? usernameFilter)
        {
            string key = displaySnapshotCacheKey(usernameFilter);

            lock (sessionCacheLock)
            {
                if (displaySnapshotCache.TryGetValue(key, out var cached))
                    return cached;
            }

            EzLocalProfileSnapshot snapshot;

            if (EzLocalProfileConstants.IsAllPlayersFilter(usernameFilter))
                snapshot = Store.LoadSnapshotForUsername(usernameFilter);
            else if (EzLocalProfileConstants.IsGuestUsername(usernameFilter))
            {
                var guest = Store.LoadSnapshotForUsername(EzLocalProfileConstants.GUEST_USERNAME);
                snapshot = guest.HasData
                    ? guest
                    : Store.LoadSnapshotForUsername(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME);
            }
            else
                snapshot = Store.LoadSnapshotForUsername(usernameFilter);

            lock (sessionCacheLock)
                displaySnapshotCache[key] = snapshot;

            return snapshot;
        }

        public IReadOnlyList<EzLocalProfileDrillScoreRow> LoadDrillScores(int rulesetId, string? usernameFilter = null)
        {
            string key = drillCacheKey(rulesetId, usernameFilter);

            lock (sessionCacheLock)
            {
                if (drillCache.TryGetValue(key, out var cached))
                    return cached;
            }

            var loaded = Store.LoadDrillScores(rulesetId, usernameFilter);

            lock (sessionCacheLock)
                drillCache[key] = loaded;

            return loaded;
        }

        /// <summary>KPS series for a single score, cached per session (only the selected drill row needs it).</summary>
        public IReadOnlyList<double> LoadKpsList(Guid scoreId)
        {
            lock (sessionCacheLock)
            {
                if (kpsCache.TryGetValue(scoreId, out var cached))
                    return cached;
            }

            var loaded = Store.LoadKpsList(scoreId);

            lock (sessionCacheLock)
                kpsCache[scoreId] = loaded;

            return loaded;
        }

        public bool TryGetCachedInsights(string? usernameFilter, out EzLocalProfileInsights? insights)
        {
            string key = insightsCacheKey(usernameFilter);

            lock (sessionCacheLock)
            {
                if (insightsMemoryCache.TryGetValue(key, out insights))
                    return true;
            }

            insights = Store.TryLoadInsightsCache(usernameFilter);
            if (insights == null)
                return false;

            lock (sessionCacheLock)
                insightsMemoryCache[key] = insights;

            return true;
        }

        public void SetCachedInsights(string? usernameFilter, EzLocalProfileInsights insights)
        {
            string key = insightsCacheKey(usernameFilter);

            lock (sessionCacheLock)
                insightsMemoryCache[key] = insights;

            Store.SaveInsightsCache(usernameFilter, insights);
        }

        /// <summary>Drop session + SQLite insights caches after compute / partition writes.</summary>
        public void InvalidateSessionCaches()
        {
            lock (sessionCacheLock)
            {
                drillCache.Clear();
                displaySnapshotCache.Clear();
                insightsMemoryCache.Clear();
                kpsCache.Clear();
            }

            Store.ClearInsightsCache();
        }

        private static string displaySnapshotCacheKey(string? usernameFilter)
            => EzLocalProfileConstants.IsAllPlayersFilter(usernameFilter)
                ? EzLocalProfileConstants.ALL_PLAYERS
                : EzLocalProfileConstants.NormaliseUsername(usernameFilter);

        private static string drillCacheKey(int rulesetId, string? usernameFilter)
            => $"{rulesetId}|{displaySnapshotCacheKey(usernameFilter)}";

        private static string insightsCacheKey(string? usernameFilter)
            => displaySnapshotCacheKey(usernameFilter);

        public bool HasOnlineScoreContributions() => Store.LoadOnlineScoreContributions().Count > 0;

        /// <param name="usernamesToRecompute">Names whose score stats will be recalculated and overwrite their stored slice.</param>
        /// <param name="replaceOtherUsernames">
        /// If true, drop stored slices for names not in <paramref name="usernamesToRecompute"/>.
        /// If false, leave other names' slices untouched.
        /// </param>
        /// <param name="clearRebuild">
        /// Clear-and-rebuild: drop the incremental caches (partitions + per-play skill caches) and recompute from scratch.
        /// When false (default) the compute is a backfill — scores and plays already analysed are skipped.
        /// </param>
        /// <param name="progress">Optional progress reporter for UI notifications.</param>
        /// <param name="cancellationToken">Cancels in-flight aggregation; progress flushed so far is kept.</param>
        public Task ComputeAsync(
            IReadOnlyCollection<string> usernamesToRecompute,
            bool replaceOtherUsernames = false,
            bool clearRebuild = false,
            IProgress<EzLocalProfileComputeProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            lock (computeLock)
            {
                computeCts?.Cancel();
                computeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = computeCts.Token;

                IsComputing.Value = true;

                return Task.Run(() =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        var selected = usernamesToRecompute
                                       .Where(n => !string.IsNullOrWhiteSpace(n))
                                       .Distinct(StringComparer.Ordinal)
                                       .ToList();

                        if (clearRebuild)
                            Store.ClearSkillCaches();

                        // Online-only refresh path: no local usernames selected, just rebuild display from existing partitions + online.
                        var byUser = selected.Count > 0
                            ? runIncrementalAggregation(selected, clearRebuild, progress, token)
                            : new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);

                        token.ThrowIfCancellationRequested();

                        progress?.Report(new EzLocalProfileComputeProgress(0, 1, EzLocalProfileComputePhase.Saving));

                        var online = Store.LoadOnlineScoreContributions();
                        var localOnlineIds = aggregator.CollectLocalOnlineScoreIds();
                        Store.ApplyUsernamePartitions(byUser, replaceOtherUsernames, online, localOnlineIds);

                        if (selected.Count > 0)
                            writePlayerSkills(selected, progress, token);

                        InvalidateSessionCaches();
                        Snapshot.Value = Store.LoadSnapshot();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "[EzLocalProfile] Failed to compute local score analysis.", Ez2ConfigManager.LOGGER_NAME);
                        throw;
                    }
                    finally
                    {
                        IsComputing.Value = false;
                    }
                }, token);
            }
        }

        /// <summary>How many not-yet-cached scores are analysed before progress is flushed to disk.</summary>
        private const int compute_chunk_size = 200;

        /// <summary>
        /// Backfill aggregation: reuse each username's stored partition as a cache and only analyse scores missing from it.
        /// Progress is flushed to the partition after every chunk so cancelling a long run keeps what was already done.
        /// </summary>
        private Dictionary<string, EzLocalProfileAggregationResult> runIncrementalAggregation(
            IReadOnlyList<string> selected,
            bool clearRebuild,
            IProgress<EzLocalProfileComputeProgress>? progress,
            CancellationToken token)
        {
            var collected = aggregator.CollectDetachedScoresByUsername(selected, token);
            var cachedOffsets = Store.LoadAvgAbsOffsets(selected);

            var plan = new List<(string Username, EzLocalProfileAggregationResult Merged, List<ScoreInfo> Missing)>();

            foreach (string username in selected)
            {
                var current = collected.TryGetValue(username, out var list) ? list : new List<ScoreInfo>();
                var currentIds = new HashSet<Guid>(current.Select(s => s.ID));

                // Clear-and-rebuild ignores the stored slice; a backfill reuses it as the incremental cache.
                EzLocalProfilePartitionPayload? existing = clearRebuild
                    ? null
                    : Store.TryLoadPartitionPayload(username);

                // Reusable only when the slice was computed with the current analysis logic AND every stored
                // drill still maps to a live score (a leftover from a deleted score forces that name to rebuild).
                bool reusable = existing != null
                                && existing.ContentVersion == EzLocalProfileStore.CONTENT_VERSION
                                && existing.DrillScores.All(d => currentIds.Contains(d.ScoreId));

                var merged = new EzLocalProfileAggregationResult { IncludedUsernames = new[] { username } };
                var done = new HashSet<Guid>();

                if (reusable)
                {
                    existing!.MergeInto(merged);

                    foreach (var drill in existing.DrillScores)
                        done.Add(drill.ScoreId);
                }

                plan.Add((username, merged, current.Where(s => !done.Contains(s.ID)).ToList()));
            }

            int total = plan.Sum(p => p.Missing.Count);
            int processed = 0;
            var state = aggregator.CreateState();

            progress?.Report(new EzLocalProfileComputeProgress(0, Math.Max(1, total), EzLocalProfileComputePhase.Analysing));

            foreach (var (username, merged, missing) in plan)
            {
                for (int offset = 0; offset < missing.Count; offset += compute_chunk_size)
                {
                    token.ThrowIfCancellationRequested();

                    int count = Math.Min(compute_chunk_size, missing.Count - offset);
                    var chunk = new List<(string Username, ScoreInfo Score)>(count);

                    for (int i = 0; i < count; i++)
                        chunk.Add((username, missing[offset + i]));

                    var partial = aggregator.AggregateScores(
                        chunk,
                        state,
                        cachedOffsets,
                        () =>
                        {
                            processed++;
                            progress?.Report(new EzLocalProfileComputeProgress(processed, Math.Max(1, total), EzLocalProfileComputePhase.Analysing));
                        },
                        token);

                    if (partial.TryGetValue(username, out var partialResult))
                        EzLocalProfilePartitionPayload.FromAggregation(partialResult).MergeInto(merged);

                    // Flush after each chunk so a cancelled run resumes from here instead of starting over.
                    Store.SavePartitionPayload(username, EzLocalProfilePartitionPayload.FromAggregation(merged));
                }
            }

            var byUser = new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);

            foreach (var (username, merged, _) in plan)
                byUser[username] = merged;

            return byUser;
        }

        private void writePlayerSkills(
            IReadOnlyList<string> selectedUsernames,
            IProgress<EzLocalProfileComputeProgress>? progress,
            CancellationToken token)
        {
            if (ssrAggregator == null && danAggregator == null)
                return;

            int passCount = (ssrAggregator != null ? 1 : 0) + (danAggregator != null ? 1 : 0);
            if (passCount == 0)
                return;

            var selectedReal = selectedUsernames
                               .Select(EzLocalProfileConstants.NormaliseUsername)
                               .Where(n => !string.IsNullOrEmpty(n) && !EzLocalProfileConstants.IsAllPlayersFilter(n))
                               .Distinct(StringComparer.Ordinal)
                               .ToList();

            // Per-user lists are the full mania play set — skills need every play, not just the newly backfilled ones.
            var maniaScoresByUser = selectedReal.Count > 0
                ? aggregator.CollectManiaScoresByUsername(selectedReal, token)
                : new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            // All: always re-collect from the included set so incremental recomputes still materialise a complete archive-wide bag.
            var included = Store.LoadIncludedUsernames()
                                .Select(EzLocalProfileConstants.NormaliseUsername)
                                .Where(n => !string.IsNullOrEmpty(n) && !EzLocalProfileConstants.IsAllPlayersFilter(n))
                                .Distinct(StringComparer.Ordinal)
                                .ToList();

            Dictionary<string, List<ScoreInfo>> scoresForAll = included.Count > 0
                ? aggregator.CollectManiaScoresByUsername(included, token)
                : new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            int perUserTotal = maniaScoresByUser.Values.Sum(list => list.Count);
            int allTotal = scoresForAll.Values.Sum(list => list.Count);
            bool materializeAll = allTotal > 0;
            int skillsTotal = Math.Max(1, (perUserTotal + (materializeAll ? allTotal : 0)) * passCount);
            int skillsProcessed = 0;
            int reportEvery = Math.Max(1, Math.Min(yield_every, skillsTotal / 100));

            void tick()
            {
                skillsProcessed++;
                if (skillsProcessed == skillsTotal || skillsProcessed % reportEvery == 0)
                    progress?.Report(new EzLocalProfileComputeProgress(skillsProcessed, skillsTotal, EzLocalProfileComputePhase.Skills));

                if (skillsProcessed % yield_every == 0)
                    Thread.Sleep(1);
            }

            progress?.Report(new EzLocalProfileComputeProgress(0, skillsTotal, EzLocalProfileComputePhase.Skills));

            foreach (string username in selectedReal)
            {
                token.ThrowIfCancellationRequested();

                if (!maniaScoresByUser.TryGetValue(username, out var scores) || scores.Count == 0)
                    continue;

                persistUserSkills(username, scores, tick, token);
            }

            if (materializeAll)
            {
                token.ThrowIfCancellationRequested();

                var combined = new List<ScoreInfo>(allTotal);

                foreach (var list in scoresForAll.Values)
                    combined.AddRange(list);

                persistUserSkills(EzLocalProfileConstants.ALL_PLAYERS, combined, tick, token);
            }

            if (skillsProcessed < skillsTotal)
                progress?.Report(new EzLocalProfileComputeProgress(skillsTotal, skillsTotal, EzLocalProfileComputePhase.Skills));
        }

        private void persistUserSkills(string username, List<ScoreInfo> scores, Action tick, CancellationToken token)
        {
            bool allPlayers = EzLocalProfileConstants.IsAllPlayersFilter(username);

            tryComputeAndPersist(
                username,
                () =>
                {
                    ssrAggregator!.ComputeAndStore(username, scores, token, tick);
                    // All: keep SSR/pattern/history under the sentinel key; do not duplicate axis evidence rows.
                    Store.ReplaceAxisPlays(
                        username,
                        allPlayers ? Array.Empty<EzAxisPlayEvidenceRow>() : ssrAggregator.PendingEvidence);
                },
                ssrAggregator != null,
                "[EzLocalProfile] Failed to compute/persist player SSR skills after profile save.");

            tryComputeAndPersist(
                username,
                () =>
                {
                    danAggregator!.ComputeAndStore(username, scores, token, tick);
                    // All: clear residual All evidence; real-player clears stay; All reads use no username filter.
                    Store.ReplaceDanClears(
                        username,
                        allPlayers ? Array.Empty<EzDanClearEvidenceRow>() : danAggregator.PendingEvidence);
                    skillProvider?.RefreshDanSkillsets(username, tick);
                },
                danAggregator != null,
                "[EzLocalProfile] Failed to compute/persist player Dan estimates after profile save.");
        }

        private static void tryComputeAndPersist(string username, Action action, bool enabled, string errorMessage)
        {
            if (!enabled)
                return;

            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, $"{errorMessage} ({username})", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public IReadOnlyList<EzDanClearEvidenceRow> GetDanClears(string username, int? keyCount = null, string? side = null, int? algorithmVersion = null)
            => Store.GetDanClears(username, keyCount, side, algorithmVersion);

        public IReadOnlyList<EzAxisPlayEvidenceRow> GetAxisPlays(string username, int? keyCount = null, string? skillId = null, int? algorithmVersion = null)
            => Store.GetAxisPlays(username, keyCount, skillId, algorithmVersion);

        public void ReloadFromDisk()
        {
            // Keep session drill/insights caches; only refresh archive snapshot bindable.
            lock (sessionCacheLock)
                displaySnapshotCache.Clear();

            Snapshot.Value = Store.LoadSnapshot();
        }

        public void Dispose()
        {
            computeCts?.Cancel();
            computeCts?.Dispose();
            Store.Dispose();
        }
    }
}
