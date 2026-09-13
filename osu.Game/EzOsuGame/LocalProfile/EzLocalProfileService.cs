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

        /// <summary>
        /// Serialises the incremental single-play fold against a running compute. A compute holds it for its whole run,
        /// because <see cref="runIncrementalAggregation"/> reads a partition and its drill ledger up-front and flushes
        /// the merged totals later: an append landing in between would be overwritten (leaving a drill row that the
        /// next diff then skips, i.e. a permanently missing score).
        /// </summary>
        private readonly Lock ingestLock = new Lock();

        private readonly Lock sessionCacheLock = new Lock();
        private CancellationTokenSource? computeCts;

        /// <summary>
        /// Handle of the running <see cref="ComputeAsync"/> task, so a delete can wait for it instead of racing
        /// its per-chunk partition flushes. Always non-null once a compute has started.
        /// </summary>
        private Task? inFlightCompute;

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

                Task task = Task.Run(() =>
                {
                    // Held for the whole run so a settled play cannot append to a partition this compute is mid-way
                    // through re-deriving; see ingestLock.
                    lock (ingestLock)
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
                    }
                }, token);

                inFlightCompute = task;
                return task;
            }
        }

        /// <summary>
        /// Fence players out of the analysed archive: their partition — and with it every per-play skill cache —
        /// is kept, but stops feeding the totals and <see cref="EzLocalProfileStore.LoadIncludedUsernames"/>, so the archive-wide
        /// <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> view no longer counts them. Re-selecting a player later
        /// only clears the flag and re-aggregates, so the expensive per-play results survive.
        /// </summary>
        /// <remarks>
        /// Cancels and awaits any in-flight compute first: a compute still flushing a chunk would otherwise
        /// re-write the partition that was just fenced. The excluded player's own Realm skill rows are left alone;
        /// only the archive-wide <c>All</c> sentinel is re-derived from the players that remain.
        /// </remarks>
        /// <returns>Usernames that were newly excluded.</returns>
        public async Task<IReadOnlyList<string>> ExcludeUsernamesAsync(
            IReadOnlyCollection<string> usernames,
            CancellationToken cancellationToken = default)
        {
            var names = usernames
                        .Select(EzLocalProfileConstants.NormaliseUsername)
                        .Where(n => !string.IsNullOrEmpty(n) && !EzLocalProfileConstants.IsAllPlayersFilter(n))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(n => n, StringComparer.Ordinal)
                        .ToList();

            if (names.Count == 0)
                return Array.Empty<string>();

            Task? pending;

            lock (computeLock)
            {
                pending = inFlightCompute;
                computeCts?.Cancel();
            }

            if (pending != null)
            {
                try
                {
                    await pending.ConfigureAwait(false);
                }
                catch
                {
                    // A cancelled / faulted compute is irrelevant to the exclusion; its flushed chunks are re-derived below.
                }
            }

            return await Task.Run<IReadOnlyList<string>>(() =>
            {
                // No ingest may append to a partition we are fencing out; see ingestLock.
                lock (ingestLock)
                {
                    var online = Store.LoadOnlineScoreContributions();
                    // Only worth the whole-library scan when there is something to de-duplicate against.
                    var localOnlineIds = online.Count > 0 ? aggregator.CollectLocalOnlineScoreIds() : new HashSet<long>();
                    var excluded = new List<string>();

                    foreach (string name in names)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (Store.ExcludeUsernames(name, online, localOnlineIds))
                            excluded.Add(name);
                    }

                    if (excluded.Count > 0)
                        refreshAllPlayerSkills();

                    InvalidateSessionCaches();
                    Snapshot.Value = Store.LoadSnapshot();

                    Logger.Log(
                        $"[EzLocalProfile] Excluded {excluded.Count} player(s) from the archive: {(excluded.Count > 0 ? string.Join(", ", excluded) : "(none matched)")}.",
                        Ez2ConfigManager.LOGGER_NAME);

                    return excluded;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Fold a play that has just settled into the local analysis, at the same point its score reaches Realm —
        /// no polling and no process-level watcher.
        /// </summary>
        /// <remarks>
        /// Only the SQLite slice is written here (drill row + partition + archive totals); the player's Realm skill
        /// rows are flagged stale rather than recomputed, so the hot path stays proportional to one score and the
        /// startup warmup does the expensive skill aggregation. The call is fire-and-forget and idempotent: the drill
        /// table is the ledger, and a play whose online id was already pulled as a contribution retires that row first
        /// so the two cannot both be counted. Replays the same id are a no-op.
        /// </remarks>
        /// <param name="scoreId">Realm id of the score that was just imported.</param>
        /// <returns><see langword="true"/> when the score was newly folded in.</returns>
        public Task<bool> IngestSettledScoreAsync(Guid scoreId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                lock (ingestLock)
                {
                    // A running compute holds this lock, so reaching here means none is active; the flag also covers
                    // the short window where one has been queued but has not taken the lock yet (it will pick the
                    // score up when it reads the live ledger).
                    if (IsComputing.Value)
                        return false;

                    var score = aggregator.LoadManagedScore(scoreId);

                    // Not a mania/std play with a matching beatmap, or already gone: nothing for the archive to fold.
                    if (score == null)
                        return false;

                    string username = EzLocalProfileConstants.NormaliseUsername(score.RealmUser.Username);

                    if (string.IsNullOrEmpty(username) || EzLocalProfileConstants.IsAllPlayersFilter(username))
                        return false;

                    // Only players already part of the analysis are maintained; a new player still has to be selected.
                    if (!Store.LoadIncludedUsernames().Contains(username, StringComparer.Ordinal))
                        return false;

                    if (Store.ContainsDrillScore(scoreId))
                        return false;

                    // A pulled online summary of the same play must not be counted alongside its detailed local score.
                    if (score.OnlineID > 0 && Store.RemoveOnlineScoreContribution(score.OnlineID))
                    {
                        Logger.Log($"[EzLocalProfile] Local import of score {score.OnlineID} superseded its pulled online contribution.",
                            Ez2ConfigManager.LOGGER_NAME);
                    }

                    var cachedOffsets = Store.LoadAvgAbsOffsets(new[] { username });
                    var state = aggregator.CreateState();
                    var byUser = aggregator.AggregateScores(new[] { (username, score) }, state, cachedOffsets);

                    if (!byUser.TryGetValue(username, out var delta))
                        return false;

                    var online = Store.LoadOnlineScoreContributions();
                    var localOnlineIds = online.Count > 0 ? aggregator.CollectLocalOnlineScoreIds() : new HashSet<long>();

                    if (!Store.AppendScores(username, delta, online, localOnlineIds))
                        return false;

                    // The Realm-side skill rows now trail the SQLite slice: flag them and let the startup warmup refresh.
                    skillProvider?.MarkPlayerSkillStale(username);
                    skillProvider?.MarkPlayerSkillStale(EzLocalProfileConstants.ALL_PLAYERS);

                    InvalidateSessionCaches();
                    Snapshot.Value = Store.LoadSnapshot();

                    return true;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Fire-and-forget form of <see cref="IngestSettledScoreAsync"/> for the gameplay import path: it never blocks
        /// the caller and never faults at it. A failure is harmless — the score stays unanalysed and the startup
        /// backfill picks it up from the drill ledger.
        /// </summary>
        public void IngestSettledScore(Guid scoreId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await IngestSettledScoreAsync(scoreId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[EzLocalProfile] Incremental fold of a settled score failed.", Ez2ConfigManager.LOGGER_NAME);
                }
            });
        }

        /// <summary>
        /// Re-derive the archive-wide <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> skill rows from whatever
        /// players remain included. Without this, <c>All</c> would keep serving the snapshot it had while the fenced
        /// player was still part of it. With nobody left, the stale sentinel rows are dropped instead.
        /// </summary>
        private void refreshAllPlayerSkills()
        {
            if (ssrAggregator == null && danAggregator == null)
                return;

            var included = Store.LoadIncludedUsernames()
                                .Select(EzLocalProfileConstants.NormaliseUsername)
                                .Where(n => !string.IsNullOrEmpty(n) && !EzLocalProfileConstants.IsAllPlayersFilter(n))
                                .Distinct(StringComparer.Ordinal)
                                .ToList();

            var combined = new List<ScoreInfo>();

            if (included.Count > 0)
            {
                foreach (var list in aggregator.CollectManiaScoresByUsername(included, CancellationToken.None).Values)
                    combined.AddRange(list);
            }

            if (combined.Count == 0)
            {
                skillProvider?.DeletePlayerSkillData(EzLocalProfileConstants.ALL_PLAYERS);
                return;
            }

            persistUserSkills(EzLocalProfileConstants.ALL_PLAYERS, combined, () => { }, CancellationToken.None);
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
            // Ledger first: a reusable slice supplies the counters to carry over, while drill_scores supplies the
            // score ids that must not be analysed again. No usable slice -> empty ledger -> every live play is new.
            var existingByUser = new Dictionary<string, EzLocalProfilePartitionPayload>(StringComparer.Ordinal);
            var ledgerByUser = new Dictionary<string, IReadOnlyCollection<Guid>>(StringComparer.Ordinal);

            foreach (string username in selected)
            {
                var existing = clearRebuild ? null : Store.TryLoadPartitionPayload(username);

                if (existing != null && existing.ContentVersion == EzLocalProfileStore.CONTENT_VERSION)
                {
                    existingByUser[username] = existing;
                    ledgerByUser[username] = Store.GetAnalyzedScoreIds(username);
                }
                else
                {
                    ledgerByUser[username] = Array.Empty<Guid>();
                }
            }

            // One Realm pass; only the plays the ledger is missing get cloned. Live ids come back for free.
            var collect = aggregator.CollectIncrementalScoresByUsername(ledgerByUser, token);
            var cachedOffsets = Store.LoadAvgAbsOffsets(selected);

            // A stored drill whose score no longer exists cannot be merged with a fresh aggregate, so those names
            // rebuild from every live play. Rare (score deletion) — only then do we pay for a second cloning pass.
            var rebuildUsers = new HashSet<string>(StringComparer.Ordinal);

            foreach (string username in selected)
            {
                // An empty ledger already means "collect returned every play", so there is nothing extra to clone.
                if (ledgerByUser[username].Count == 0)
                    continue;

                var liveIds = collect.LiveScoreIds.GetValueOrDefault(username);

                // A stored drill whose score is no longer live (deleted) cannot be merged with a fresh aggregate.
                if (liveIds == null || ledgerByUser[username].Any(id => !liveIds.Contains(id)))
                    rebuildUsers.Add(username);
            }

            Dictionary<string, List<ScoreInfo>> rebuildScores = rebuildUsers.Count > 0
                ? aggregator.CollectDetachedScoresByUsername(rebuildUsers, token)
                : new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            // Names whose drill rows must be rewritten rather than added to: a clear-and-rebuild, a slice from an
            // older content version, or a stored drill pointing at a score that is gone.
            var replaceDrills = new HashSet<string>(rebuildUsers, StringComparer.Ordinal);

            foreach (string username in selected)
            {
                if (clearRebuild || !existingByUser.ContainsKey(username))
                    replaceDrills.Add(username);
            }

            var plan = new List<(string Username, EzLocalProfileAggregationResult Merged, List<ScoreInfo> Missing)>();

            foreach (string username in selected)
            {
                bool rebuild = rebuildUsers.Contains(username);

                var merged = new EzLocalProfileAggregationResult { IncludedUsernames = new[] { username } };

                if (!rebuild && existingByUser.TryGetValue(username, out var existing))
                    existing.MergeInto(merged);

                var missing = rebuild
                    ? (rebuildScores.TryGetValue(username, out var all) ? all : new List<ScoreInfo>())
                    : (collect.NewScores.TryGetValue(username, out var fresh) ? fresh : new List<ScoreInfo>());

                plan.Add((username, merged, missing));
            }

            int total = plan.Sum(p => p.Missing.Count);
            int processed = 0;
            var state = aggregator.CreateState();
            var clearedDrills = new HashSet<string>(StringComparer.Ordinal);

            progress?.Report(new EzLocalProfileComputeProgress(0, Math.Max(1, total), EzLocalProfileComputePhase.Analysing));

            foreach (var (username, merged, missing) in plan)
            {
                // A rewrite must drop the name's old drill rows first, or leftovers from deleted scores survive.
                // A plain backfill never clears: appending is what keeps an interrupted run resumable.
                if (replaceDrills.Contains(username) && clearedDrills.Add(username))
                    Store.DeleteUserDrillScores(username);

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
                    {
                        EzLocalProfilePartitionPayload.FromAggregation(partialResult).MergeStatsInto(merged);

                        // Drill detail lands in its own table; the slice only keeps the fixed-size counters.
                        Store.AppendDrills(partialResult.DrillScores);
                    }

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
