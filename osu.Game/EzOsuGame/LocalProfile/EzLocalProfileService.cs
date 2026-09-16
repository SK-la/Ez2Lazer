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
        /// <remarks>
        /// Only the SQLite side is guarded. The fold's Realm reads and its one-play aggregation happen outside the
        /// lock and take it only around <c>AppendScores</c>, so this never serialises work that does not touch the
        /// archive (and a settled play never waits on another one's analysis).
        /// </remarks>
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

        /// <summary>
        /// Raised when a skill pass found charts the chart-side chain has not rated yet, so MSD / CSI / ChartDan
        /// backfill should be requested. The handler is expected to be cheap and non-blocking.
        /// </summary>
        /// <remarks>
        /// Set by the owning component, which can resolve the chart-side queue
        /// (<see cref="BackgroundDataStoreProcessor.QueueEzRealmMetadataRebuild"/>). Deliberately not a direct
        /// dependency: this service must stay constructible in tests and before that queue exists.
        /// </remarks>
        public Action? ChartSideBackfillRequested { get; set; }

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
        /// <param name="skillsUsernames">
        /// Players whose Realm-side skill rows additionally need re-deriving, e.g. the ones a caller found flagged
        /// stale. Players with no new plays and no flag are skipped — unless <paramref name="clearRebuild"/> is set,
        /// which discards the per-play caches and therefore forces a full re-derive. When omitted, the persisted
        /// stale flag is used as the fallback scope, so a bare compute still refreshes exactly the rows a settled
        /// play left trailing its slice.
        /// </param>
        public Task ComputeAsync(
            IReadOnlyCollection<string> usernamesToRecompute,
            bool replaceOtherUsernames = false,
            bool clearRebuild = false,
            IProgress<EzLocalProfileComputeProgress>? progress = null,
            CancellationToken cancellationToken = default,
            IReadOnlyCollection<string>? skillsUsernames = null)
        {
            lock (computeLock)
            {
                computeCts?.Cancel();
                computeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = computeCts.Token;

                IsComputing.Value = true;

                Task task = Task.Run(() =>
                {
                    // Realm-only read, so it stays outside the gate below; the gate serialises SQLite writes, not Realm reads.
                    var localOnlineIds = aggregator.CollectLocalOnlineScoreIds();

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
                            HashSet<string>? changedUsers = null;

                            var byUser = selected.Count > 0
                                ? runIncrementalAggregation(selected, clearRebuild, progress, token, out changedUsers)
                                : new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);

                            token.ThrowIfCancellationRequested();

                            progress?.Report(new EzLocalProfileComputeProgress(0, 1, EzLocalProfileComputePhase.Saving));

                            var online = Store.LoadOnlineScoreContributions();
                            Store.ApplyUsernamePartitions(byUser, replaceOtherUsernames, online, localOnlineIds);

                            if (selected.Count > 0)
                            {
                                // Only the players the pass actually changed, plus whatever the caller flagged stale.
                                // A clear-rebuild threw the per-play caches away, so it must re-derive everything.
                                HashSet<string>? skillsScope = null;

                                if (!clearRebuild)
                                {
                                    skillsScope = changedUsers ?? new HashSet<string>(StringComparer.Ordinal);

                                    if (skillsUsernames != null)
                                    {
                                        skillsScope.UnionWith(skillsUsernames);
                                    }
                                    else
                                    {
                                        // No caller-supplied scope (the manual compute): cover both reasons a player's
                                        // Realm rows can be behind. A settled play is folded into the SQLite slice by
                                        // the ingest path, so the ledger diff cannot tell that player apart from one with
                                        // nothing to do — the flag is the only record that their rows trail the slice.
                                        // Plays the chain has not rated yet are the other reason: the skills pass skips
                                        // them, so re-deriving that player is what folds them in once the chain is done.
                                        // Without both, a manual compute would leave the readout reporting work for good.
                                        IReadOnlyList<string> stale = skillProvider?.PlayerSkills.StaleUsernames ?? Array.Empty<string>();

                                        foreach (string staleUsername in stale)
                                            skillsScope.Add(EzLocalProfileConstants.NormaliseUsername(staleUsername));

                                        foreach (string debtUsername in CollectChartChainDebt().Usernames)
                                            skillsScope.Add(EzLocalProfileConstants.NormaliseUsername(debtUsername));
                                    }
                                }

                                writePlayerSkills(selected, progress, token, skillsScope);
                            }

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
                // Only worth the whole-library scan when there is something to de-duplicate against. Realm-only read,
                // so it stays outside the gate below (the gate serialises SQLite writes, not Realm reads).
                var online = Store.LoadOnlineScoreContributions();
                var localOnlineIds = online.Count > 0 ? aggregator.CollectLocalOnlineScoreIds() : new HashSet<long>();
                var excluded = new List<string>();

                // No ingest may append to a partition we are fencing out; see ingestLock.
                lock (ingestLock)
                {
                    foreach (string name in names)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (Store.ExcludeUsernames(name, online, localOnlineIds))
                            excluded.Add(name);
                    }
                }

                // Outside the gate: an append that settled meanwhile is either rejected (the player is now fenced)
                // or belongs to a player who is still included, which is what All should count anyway.
                if (excluded.Count > 0)
                    refreshAllPlayerSkills();

                InvalidateSessionCaches();
                Snapshot.Value = Store.LoadSnapshot();

                Logger.Log(
                    $"[EzLocalProfile] Excluded {excluded.Count} player(s) from the archive: {(excluded.Count > 0 ? string.Join(", ", excluded) : "(none matched)")}.",
                    Ez2ConfigManager.LOGGER_NAME);

                return excluded;
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
        /// <para>
        /// A play that settles while a compute is running waits for it instead of being dropped: only the append takes
        /// <see cref="ingestLock"/>, which the compute holds for its whole run, so the append lands either before the
        /// compute read the partition or after it flushed — never in between.
        /// </para>
        /// </remarks>
        /// <param name="scoreId">Realm id of the score that was just imported.</param>
        /// <returns><see langword="true"/> when the score was newly folded in.</returns>
        public Task<bool> IngestSettledScoreAsync(Guid scoreId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
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

                // Reading Realm and aggregating one play touches no SQLite state, so it stays outside the gate: only
                // the append below has to be serialised against a compute (see ingestLock).
                var cachedOffsets = Store.LoadAvgAbsOffsets(new[] { username });

                EzLocalProfileAggregationResult? delta;

                using (var state = aggregator.CreateState())
                {
                    var byUser = aggregator.AggregateScores(new[] { (username, score) }, state, cachedOffsets);
                    byUser.TryGetValue(username, out delta);
                }

                if (delta == null)
                    return false;

                // Which pulled contributions a local score supersedes. Bounded to the contributions themselves, so
                // a settled play no longer triggers a whole-library Realm scan once any pull exists; and it reads no
                // SQLite state, so it stays outside the gate below.
                var pulledOnline = Store.LoadOnlineScoreContributions();
                var localOnlineIds = pulledOnline.Count > 0
                    ? aggregator.CollectLocalOnlineScoreIds(pulledOnline.Select(c => c.OnlineId).ToList())
                    : new HashSet<long>();

                lock (ingestLock)
                {
                    // A pulled online summary of the same play must not be counted alongside its detailed local score.
                    if (score.OnlineID > 0 && Store.RemoveOnlineScoreContribution(score.OnlineID))
                    {
                        Logger.Log($"[EzLocalProfile] Local import of score {score.OnlineID} superseded its pulled online contribution.",
                            Ez2ConfigManager.LOGGER_NAME);
                    }

                    // Read the contributions inside the gate: they are merged into the same rebuild, so a row another
                    // append retired meanwhile must not sneak back in through a list read before the lock.
                    var online = Store.LoadOnlineScoreContributions();

                    if (!Store.AppendScores(username, delta, online, localOnlineIds))
                        return false;
                }

                // The Realm-side skill rows now trail the SQLite slice: flag them and let the startup warmup refresh.
                skillProvider?.PlayerSkills.SetStale(username, true);
                skillProvider?.PlayerSkills.SetStale(EzLocalProfileConstants.ALL_PLAYERS, true);

                InvalidateSessionCaches();
                Snapshot.Value = Store.LoadSnapshot();

                return true;
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
        /// Reconcile the archive against Realm without recomputing anything: which of the already-selected players
        /// have plays the SQLite slice never folded in, and which have Realm skill rows trailing that slice. This is
        /// what makes the startup backfill automatic and cheap to skip — no work means a quiet, costless launch.
        /// </summary>
        /// <remarks>
        /// Both halves come from persisted state rather than a bookkeeping flag: the drill ledger diff catches plays
        /// that settled after the last successful fold (crash, force close), and the stale flag catches plays that
        /// were folded in but whose skills were not refreshed yet.
        /// </remarks>
        public EzLocalProfileStartupAlignPlan PlanStartupAlign(CancellationToken cancellationToken = default)
        {
            var included = Store.LoadIncludedUsernames()
                                .Select(EzLocalProfileConstants.NormaliseUsername)
                                .Where(n => !string.IsNullOrEmpty(n) && !EzLocalProfileConstants.IsAllPlayersFilter(n))
                                .Distinct(StringComparer.Ordinal)
                                .ToList();

            if (included.Count == 0)
                return EzLocalProfileStartupAlignPlan.Empty;

            var ledger = new Dictionary<string, IReadOnlyCollection<Guid>>(StringComparer.Ordinal);

            foreach (string username in included)
                ledger[username] = Store.GetAnalyzedScoreIds(username);

            var collect = aggregator.CollectIncrementalScoresByUsername(ledger, cancellationToken);
            var pending = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string username in included)
            {
                int missing = collect.NewScores.TryGetValue(username, out var fresh) ? fresh.Count : 0;

                // A drill pointing at a score that no longer exists also needs a rebuild; count it as one unit of work.
                var live = collect.LiveScoreIds.GetValueOrDefault(username);

                if (live != null && ledger[username].Any(id => !live.Contains(id)))
                    missing = Math.Max(missing, 1);

                if (missing > 0)
                    pending[username] = missing;
            }

            var stale = skillProvider?.PlayerSkills.StaleUsernames ?? Array.Empty<string>();
            var includedSet = new HashSet<string>(included, StringComparer.Ordinal);
            var staleIncluded = stale.Where(includedSet.Contains).ToList();

            // The other reason a reconcile has work: plays the chain has not rated, so the skills pass skipped them.
            // Derived from the ledger against the chain's own coverage, so it needs no flag on the player's rows and
            // is empty again as soon as the chain catches up.
            var debtIncluded = CollectChartChainDebt().Usernames.Where(includedSet.Contains).ToList();

            return new EzLocalProfileStartupAlignPlan(included, pending, staleIncluded, debtIncluded, Store.NeedsRecompute());
        }

        /// <summary>
        /// Plays the chart-side chain still owes a result for, joined from the drill ledger against the chain's own
        /// coverage. Reads SQLite and Realm, so call it off the UI thread.
        /// </summary>
        public EzChartChainDebt CollectChartChainDebt()
            => skillProvider == null
                ? EzChartChainDebt.Empty
                : EzChartChainDebt.Collect(Store.LoadManiaDrillChartPlays(), skillProvider.Store);

        /// <summary>
        /// Run the reconcile described by <see cref="PlanStartupAlign"/>: one incremental compute over the players the
        /// archive already covers, which folds in missing plays and refreshes the skill rows that were flagged stale.
        /// Deleted plays, older content versions and per-play skill caches are all handled inside the compute.
        /// </summary>
        public Task AlignOnStartupAsync(
            EzLocalProfileStartupAlignPlan plan,
            IProgress<EzLocalProfileComputeProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);

            if (!plan.HasWork || plan.IncludedUsernames.Count == 0)
                return Task.CompletedTask;

            Logger.Log(
                $"[EzLocalProfile] Score analysis reconcile: {plan.TotalPendingPlays} pending play(s) across {plan.PendingPlaysByUser.Count} player(s), "
                + $"{plan.StaleSkillUsernames.Count} with stale skills, {plan.ChartDebtUsernames.Count} waiting on the chart chain, "
                + $"contentVersionStale={plan.ContentVersionStale}.",
                Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

            // Incremental by construction: the ledger only yields plays that were never analysed, and the skills pass
            // reuses the per-play caches. Never a clear-rebuild — a launch must not throw away good work.
            // Skills are narrowed to the three groups that can have moved — pending plays, flagged players and players
            // waiting on the chain; the rest keep their rows (the skill pass re-derives nothing for an untouched player).
            var skillsScope = new HashSet<string>(plan.PendingPlaysByUser.Keys, StringComparer.Ordinal);
            skillsScope.UnionWith(plan.StaleSkillUsernames);
            skillsScope.UnionWith(plan.ChartDebtUsernames);

            return ComputeAsync(plan.IncludedUsernames, replaceOtherUsernames: false, clearRebuild: false, progress, cancellationToken, skillsScope);
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
                skillProvider?.PlayerSkills.Delete(EzLocalProfileConstants.ALL_PLAYERS);
                return;
            }

            // The excluded player is gone from the included set, so anything the pass cannot read is reported here too.
            danAggregator?.BeginSkillPass();

            persistUserSkills(
                EzLocalProfileConstants.ALL_PLAYERS,
                combined,
                () => { },
                CancellationToken.None,
                ssrAggregator?.LoadPlayCache(),
                danAggregator?.LoadPlayCache(),
                new HashSet<string>(StringComparer.Ordinal),
                out _);
        }

        /// <summary>How many not-yet-cached scores are analysed before progress is flushed to disk.</summary>
        private const int compute_chunk_size = 200;

        /// <summary>
        /// Backfill aggregation: reuse each username's stored partition as a cache and only analyse scores missing from it.
        /// Progress is flushed to the partition after every chunk so cancelling a long run keeps what was already done.
        /// </summary>
        /// <param name="changedUsers">
        /// The players whose stored slice actually moved (new plays analysed, or a rebuild). A player with nothing new
        /// is absent, which is what lets the skill pass skip re-deriving rows that cannot have changed.
        /// </param>
        private Dictionary<string, EzLocalProfileAggregationResult> runIncrementalAggregation(
            IReadOnlyList<string> selected,
            bool clearRebuild,
            IProgress<EzLocalProfileComputeProgress>? progress,
            CancellationToken token,
            out HashSet<string> changedUsers)
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
            using var state = aggregator.CreateState();
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
            changedUsers = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (username, merged, missing) in plan)
            {
                byUser[username] = merged;

                // New plays folded in, or a rebuild that rewrote the whole slice: either way the stored numbers moved.
                if (missing.Count > 0 || replaceDrills.Contains(username))
                    changedUsers.Add(username);
            }

            return byUser;
        }

        /// <summary>
        /// Recompute the Realm-side skill rows for a set of players (plus the archive-wide <c>All</c> sentinel).
        /// </summary>
        /// <param name="selectedUsernames">
        /// Players the archive considers selected. The per-player lists are the full mania play set, because skills
        /// aggregate every play rather than just the newly backfilled ones.
        /// </param>
        /// <param name="skillsUsernames">
        /// The subset whose skills actually need re-deriving (new plays, or rows flagged stale). <see langword="null"/>
        /// means all of <paramref name="selectedUsernames"/> — used by a clear-rebuild, which discards the per-play
        /// caches and therefore has to re-derive everything. A player outside this set keeps its Realm rows and its
        /// evidence rows untouched.
        /// </param>
        private void writePlayerSkills(
            IReadOnlyList<string> selectedUsernames,
            IProgress<EzLocalProfileComputeProgress>? progress,
            CancellationToken token,
            IReadOnlyCollection<string>? skillsUsernames = null)
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

            // Narrow to the players that actually need it. The rest keep their Realm rows and evidence rows; their
            // per-play caches are also left intact, so a later pass still only pays for what changed.
            var refreshScope = skillsUsernames?.Select(EzLocalProfileConstants.NormaliseUsername).ToHashSet(StringComparer.Ordinal);

            // All: always re-collect from the included set so incremental recomputes still materialise a complete
            // archive-wide bag (AggregateSSRs is a fixed-point over the individual plays, so it cannot be merged
            // from per-player results).
            var included = Store.LoadIncludedUsernames()
                                .Select(EzLocalProfileConstants.NormaliseUsername)
                                .Where(n => !string.IsNullOrEmpty(n) && !EzLocalProfileConstants.IsAllPlayersFilter(n))
                                .Distinct(StringComparer.Ordinal)
                                .ToList();

            var (refreshReal, orphaned) = EzPlayerSkillRefreshScope.Resolve(selectedReal, refreshScope, included);

            foreach (string orphanUsername in orphaned)
                skillProvider?.PlayerSkills.Delete(orphanUsername);

            // The archive-wide bag is re-derived whenever a real player is. It is also refreshed on its own when the
            // scope names it: a settled play flags All alongside the player, so a pass that covers no real player
            // must not leave All flagged with nothing to pick it up.
            bool refreshAll = refreshReal.Count > 0
                              || refreshScope?.Contains(EzLocalProfileConstants.ALL_PLAYERS) == true;

            if (!refreshAll)
            {
                // Nothing changed: do not touch the skill tables at all (the default skip of a quiet re-run).
                return;
            }

            // One Realm pass for the union: the per-player slices and the All bag come from the same detached set,
            // instead of a second full scan + second clone pass for All.
            var wanted = new HashSet<string>(refreshReal, StringComparer.Ordinal);
            wanted.UnionWith(included);

            var collected = aggregator.CollectManiaScoresByUsername(wanted, token);

            var maniaScoresByUser = new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            foreach (string username in refreshReal)
                maniaScoresByUser[username] = collected.TryGetValue(username, out var list) ? list : new List<ScoreInfo>();

            var allBag = new List<ScoreInfo>();

            foreach (string username in included)
            {
                if (collected.TryGetValue(username, out var list))
                    allBag.AddRange(list);
            }

            int perUserTotal = maniaScoresByUser.Values.Sum(list => list.Count);
            bool materializeAll = refreshAll && allBag.Count > 0;
            int skillsTotal = Math.Max(1, (perUserTotal + (materializeAll ? allBag.Count : 0)) * passCount);
            int skillsProcessed = 0;

            var missingCharts = new HashSet<string>(StringComparer.Ordinal);

            // Loaded once for the whole pass: both aggregators and every player reuse these, instead of each player
            // re-reading (and re-parsing the vectors of) the entire per-play table.
            var ssrPlayCache = ssrAggregator?.LoadPlayCache();
            var danPlayCache = danAggregator?.LoadPlayCache();

            // New pass: drop anything the dan aggregator memoised against the previous one.
            danAggregator?.BeginSkillPass();

            void tick()
            {
                skillsProcessed++;

                // Coarse per-chunk reporting; the notification forwarder drops whatever is not a new percentage.
                if (skillsProcessed == skillsTotal || skillsProcessed % yield_every == 0)
                    progress?.Report(new EzLocalProfileComputeProgress(skillsProcessed, skillsTotal, EzLocalProfileComputePhase.Skills));

                EzLocalProfileCooperativeYield.MaybeYield();
            }

            progress?.Report(new EzLocalProfileComputeProgress(0, skillsTotal, EzLocalProfileComputePhase.Skills));

            var missedChartsByUser = new HashSet<string>(StringComparer.Ordinal);

            foreach (string username in refreshReal)
            {
                token.ThrowIfCancellationRequested();

                if (!maniaScoresByUser.TryGetValue(username, out var scores) || scores.Count == 0)
                {
                    // The slice holds no mania play for this player, so no pass can ever re-derive the rows the stale
                    // flag points at. Drop them instead of leaving a flag the status readout can never clear - the
                    // same rule the archive-wide bag follows (see refreshAllPlayerSkills). Self-healing: a play that
                    // becomes readable again leaves a drill the ledger can no longer match, so the next compute
                    // rebuilds this player's slice and re-derives the rows.
                    skillProvider?.PlayerSkills.Delete(username);
                    continue;
                }

                bool persisted = persistUserSkills(username, scores, tick, token, ssrPlayCache, danPlayCache, missingCharts, out var missedCharts);

                if (!persisted)
                    continue;

                skillProvider?.PlayerSkills.SetStale(username, false);

                if (missedCharts.Count > 0)
                    missedChartsByUser.Add(username);
            }

            if (materializeAll)
            {
                token.ThrowIfCancellationRequested();

                if (persistUserSkills(EzLocalProfileConstants.ALL_PLAYERS, allBag, tick, token, ssrPlayCache, danPlayCache, missingCharts, out _))
                    skillProvider?.PlayerSkills.SetStale(EzLocalProfileConstants.ALL_PLAYERS, false);
            }
            else if (refreshAll)
            {
                // Nothing to build an archive-wide bag from: drop the sentinel rows rather than leave a stale flag
                // no pass can clear (mirrors refreshAllPlayerSkills' empty-bag case).
                skillProvider?.PlayerSkills.Delete(EzLocalProfileConstants.ALL_PLAYERS);
            }

            if (skillsProcessed < skillsTotal)
                progress?.Report(new EzLocalProfileComputeProgress(skillsTotal, skillsTotal, EzLocalProfileComputePhase.Skills));

            // The chart-side chain owns MSD / CSI / ChartDan. Anything this pass could not read is handed to it rather
            // than rated here, and the players it left a hole for are flagged: their rows are missing those plays, so
            // they do trail the slice until a later pass can fold them in. Only the players whose own plays hit an
            // unrated chart are named — one chart's answer must not put every other player back into the queue.
            if (missingCharts.Count > 0)
            {
                Logger.Log(
                    $"[EzLocalProfile] {missingCharts.Count} chart(s) have no chart-side skill data yet; requesting chart-chain backfill.",
                    Ez2ConfigManager.LOGGER_NAME);

                // All aggregates the same plays, so it has the same hole the moment any real player does.
                if (missedChartsByUser.Count > 0)
                    skillProvider?.PlayerSkills.SetStale(EzLocalProfileConstants.ALL_PLAYERS, true);

                foreach (string missingUsername in missedChartsByUser)
                    skillProvider?.PlayerSkills.SetStale(missingUsername, true);

                ChartSideBackfillRequested?.Invoke();
            }
        }

        /// <summary>
        /// Re-derive one player's skill rows and record what the pass could not read.
        /// </summary>
        /// <param name="missedCharts">
        /// Charts this pass needed a chart-side row for and found none, so the plays on them are not in the rows that
        /// were just written. Empty for the archive-wide bag, whose holes are its players' holes.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when at least one aggregator ran without throwing, i.e. this pass did cover the
        /// player's slice and the caller may stop treating their rows as stale.
        /// </returns>
        private bool persistUserSkills(
            string username,
            List<ScoreInfo> scores,
            Action tick,
            CancellationToken token,
            IReadOnlyDictionary<Guid, EzSsrPlayCacheRow>? ssrPlayCache,
            IReadOnlyDictionary<Guid, EzDanPlayCacheRow>? danPlayCache,
            HashSet<string> missingCharts,
            out HashSet<string> missedCharts)
        {
            bool allPlayers = EzLocalProfileConstants.IsAllPlayersFilter(username);
            var playerMissed = new HashSet<string>(StringComparer.Ordinal);

            bool persisted = tryComputeAndPersist(
                username,
                () =>
                {
                    ssrAggregator!.ComputeAndStore(username, scores, token, tick, ssrPlayCache);
                    // All: keep SSR/pattern/history under the sentinel key; do not duplicate axis evidence rows.
                    Store.ReplaceAxisPlays(
                        username,
                        allPlayers ? Array.Empty<EzAxisPlayEvidenceRow>() : ssrAggregator.PendingEvidence);

                    if (!allPlayers)
                        playerMissed.UnionWith(ssrAggregator.MissingChartHashes);
                },
                ssrAggregator != null,
                "[EzLocalProfile] Failed to compute/persist player SSR skills after profile save.");

            persisted |= tryComputeAndPersist(
                username,
                () =>
                {
                    danAggregator!.ComputeAndStore(username, scores, token, tick, danPlayCache);
                    // All: clear residual All evidence; real-player clears stay; All reads use no username filter.
                    Store.ReplaceDanClears(
                        username,
                        allPlayers ? Array.Empty<EzDanClearEvidenceRow>() : danAggregator.PendingEvidence);
                    skillProvider?.RefreshDanSkillsets(username, tick);

                    if (!allPlayers)
                        playerMissed.UnionWith(danAggregator.MissingChartHashes);
                },
                danAggregator != null,
                "[EzLocalProfile] Failed to compute/persist player Dan estimates after profile save.");

            missedCharts = playerMissed;
            missingCharts.UnionWith(playerMissed);

            return persisted;
        }

        private static bool tryComputeAndPersist(string username, Action action, bool enabled, string errorMessage)
        {
            if (!enabled)
                return false;

            try
            {
                action();
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, $"{errorMessage} ({username})", Ez2ConfigManager.LOGGER_NAME);
                return false;
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
            // computeCts is only ever touched under computeLock, so Dispose has to take it too.
            lock (computeLock)
            {
                computeCts?.Cancel();
                computeCts?.Dispose();
                computeCts = null;
            }

            Store.Dispose();
        }
    }
}
