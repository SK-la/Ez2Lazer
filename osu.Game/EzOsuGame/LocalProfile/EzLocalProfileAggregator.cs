// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public class EzLocalProfileAggregator
    {
        private const int yield_every = 32;

        private readonly RealmAccess realm;
        private readonly EzAnalysisPersistentStore analysisStore;
        private readonly BeatmapManager beatmapManager;
        private readonly EzLocalProfilePpResolver ppResolver;

        public EzLocalProfileAggregator(
            RealmAccess realm,
            EzAnalysisPersistentStore analysisStore,
            BeatmapManager beatmapManager)
        {
            this.realm = realm;
            this.analysisStore = analysisStore;
            this.beatmapManager = beatmapManager;
            ppResolver = new EzLocalProfilePpResolver(beatmapManager);
        }

        public IReadOnlyList<EzLocalProfileUsernameCount> ScanUsernameCounts()
        {
            return realm.Run(r =>
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var score in queryValidScores(r))
                {
                    string username = normaliseUsername(score.RealmUser.Username);
                    counts.TryGetValue(username, out int existing);
                    counts[username] = existing + 1;
                }

                return counts
                       .Select(kv => new EzLocalProfileUsernameCount(kv.Key, kv.Value))
                       .OrderByDescending(c => c.ScoreCount)
                       .ThenBy(c => c.Username, StringComparer.Ordinal)
                       .ToList();
            });
        }

        /// <summary>
        /// All valid detached scores for the given usernames (one Realm pass), keyed by normalised username.
        /// The caller filters already-cached scores and re-chunks what is left for incremental compute.
        /// </summary>
        public Dictionary<string, List<ScoreInfo>> CollectDetachedScoresByUsername(
            IReadOnlyCollection<string> usernames,
            CancellationToken cancellationToken = default)
        {
            var includeSet = new HashSet<string>(usernames.Select(normaliseUsername), StringComparer.Ordinal);
            var byUser = new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            realm.Run(r =>
            {
                if (includeSet.Count == 0)
                    return;

                foreach (var score in queryValidScores(r))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string username = normaliseUsername(score.RealmUser.Username);
                    if (!includeSet.Contains(username))
                        continue;

                    if (score.BeatmapInfo == null)
                        continue;

                    if (!byUser.TryGetValue(username, out var list))
                        byUser[username] = list = new List<ScoreInfo>();

                    list.Add(score.DeepClone());
                }
            });

            foreach (string username in includeSet)
            {
                if (!byUser.ContainsKey(username))
                    byUser[username] = new List<ScoreInfo>();
            }

            return byUser;
        }

        /// <summary>Result of one incremental Realm pass — see <see cref="CollectIncrementalScoresByUsername"/>.</summary>
        /// <param name="LiveScoreIds">Every valid live score id per username (the reconciliation side of the diff).</param>
        /// <param name="NewScores">Only the plays a username's ledger did not already contain (already cloned).</param>
        public readonly record struct EzIncrementalScoreCollect(
            Dictionary<string, HashSet<Guid>> LiveScoreIds,
            Dictionary<string, List<ScoreInfo>> NewScores);

        /// <summary>
        /// Incremental variant of <see cref="CollectDetachedScoresByUsername"/>: one Realm pass that records every
        /// live score id but only <em>clones</em> the plays a username's ledger does not already contain. This is
        /// what keeps a backfill proportional to the missing scores instead of the whole library.
        /// </summary>
        /// <param name="storedDrillScoreIdsByUser">
        /// The "already analysed" ledger per username (see <c>EzLocalProfileStore.GetAnalyzedScoreIds</c>).
        /// An empty entry means nothing is cached, so every play comes back as new.
        /// </param>
        public EzIncrementalScoreCollect CollectIncrementalScoresByUsername(
            IReadOnlyDictionary<string, IReadOnlyCollection<Guid>> storedDrillScoreIdsByUser,
            CancellationToken cancellationToken = default)
        {
            var ledger = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);

            foreach (var (username, ids) in storedDrillScoreIdsByUser)
                ledger[normaliseUsername(username)] = ids as HashSet<Guid> ?? new HashSet<Guid>(ids);

            var live = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
            var fresh = new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            realm.Run(r =>
            {
                if (ledger.Count == 0)
                    return;

                foreach (var score in queryValidScores(r))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string username = normaliseUsername(score.RealmUser.Username);
                    if (!ledger.TryGetValue(username, out var stored))
                        continue;

                    // Mirrors CollectDetachedScoresByUsername: an unplayable score is not part of the aggregation.
                    if (score.BeatmapInfo == null)
                        continue;

                    if (!live.TryGetValue(username, out var liveIds))
                        live[username] = liveIds = new HashSet<Guid>();

                    liveIds.Add(score.ID);

                    if (stored.Contains(score.ID))
                        continue;

                    if (!fresh.TryGetValue(username, out var list))
                        fresh[username] = list = new List<ScoreInfo>();

                    list.Add(score.DeepClone());
                }
            });

            foreach (string username in ledger.Keys)
            {
                if (!live.ContainsKey(username))
                    live[username] = new HashSet<Guid>();

                if (!fresh.ContainsKey(username))
                    fresh[username] = new List<ScoreInfo>();
            }

            return new EzIncrementalScoreCollect(live, fresh);
        }

        /// <summary>Shared per-run caches for chunked aggregation (difficulty attributes, playable analysis, failure counters).</summary>
        public EzLocalProfileAggregationState CreateState() => new EzLocalProfileAggregationState(analysisStore);

        /// <summary>
        /// Carries the per-run caches, plus one memoising analysis read session so a chart encountered by many
        /// scores is read and parsed once for the whole run instead of once per score.
        /// </summary>
        public sealed class EzLocalProfileAggregationState : IDisposable
        {
            private readonly EzAnalysisPersistentStore analysisStore;
            private EzAnalysisPersistentStore.ReadSession? analysisSession;

            internal EzLocalProfileAggregationState(EzAnalysisPersistentStore analysisStore)
            {
                this.analysisStore = analysisStore;
            }

            internal Dictionary<string, DifficultyAttributes?> AttributeCache { get; } = new Dictionary<string, DifficultyAttributes?>(StringComparer.Ordinal);
            internal Dictionary<string, CachedAnalysis> AnalysisCache { get; } = new Dictionary<string, CachedAnalysis>(StringComparer.Ordinal);
            internal int PpFailures;
            internal int PpLoggedFailures;

            /// <summary>
            /// The run's shared analysis read session, opened on first use so a state that never reads the
            /// analysis store pays nothing. <see langword="null"/> when the store is disabled.
            /// </summary>
            internal EzAnalysisPersistentStore.ReadSession? AnalysisSession => analysisSession ??= analysisStore.OpenReadSession();

            public void Dispose() => analysisSession?.Dispose();
        }

        /// <summary>
        /// Aggregate one chunk of scores into per-username result slices (all scores counted).
        /// Reuse one <see cref="EzLocalProfileAggregationState"/> across chunks so the difficulty / playable-analysis
        /// caches survive; only not-yet-cached scores should be passed in (incremental backfill).
        /// Does not merge online contributions — that happens when rebuilding display totals.
        /// </summary>
        public Dictionary<string, EzLocalProfileAggregationResult> AggregateScores(
            IReadOnlyList<(string Username, ScoreInfo Score)> scores,
            EzLocalProfileAggregationState state,
            IReadOnlyDictionary<Guid, double>? cachedAvgAbsOffsets = null,
            Action? afterEachScore = null,
            CancellationToken cancellationToken = default)
        {
            var byUser = new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);

            for (int i = 0; i < scores.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (username, score) = scores[i];

                if (!byUser.TryGetValue(username, out var result))
                {
                    byUser[username] = result = new EzLocalProfileAggregationResult
                    {
                        IncludedUsernames = new[] { username },
                        ComputedAt = DateTimeOffset.UtcNow,
                    };
                }

                accumulateScore(result, username, score, state, cachedAvgAbsOffsets, cancellationToken);

                afterEachScore?.Invoke();

                if ((i + 1) % yield_every == 0)
                    EzLocalProfileCooperativeYield.MaybeYield();
            }

            if (state.PpFailures > 0)
            {
                Logger.Log(
                    $"[EzLocalProfile] Score analysis PP failures: {state.PpFailures} of {scores.Count} (this chunk).",
                    Ez2ConfigManager.LOGGER_NAME);
            }

            return byUser;
        }

        private void accumulateScore(
            EzLocalProfileAggregationResult result,
            string username,
            ScoreInfo score,
            EzLocalProfileAggregationState state,
            IReadOnlyDictionary<Guid, double>? cachedAvgAbsOffsets,
            CancellationToken cancellationToken)
        {
            int rulesetId = score.Ruleset.OnlineID;
            var beatmap = score.BeatmapInfo!;
            bool modsAffect = EzLocalProfileModAffects.AffectsPlayableAnalysis(score.Mods);

            var resolved = ppResolver.Resolve(score, state.AttributeCache, ref state.PpFailures, ref state.PpLoggedFailures);
            double pp = resolved.Pp;
            double starRating = resolved.StarRating >= 0 ? resolved.StarRating : beatmap.StarRating;

            long keys = countKeys(score);
            var cached = resolveAnalysis(score, modsAffect, state, cancellationToken);
            bool hasKps = cached.HasKps;
            var analysis = cached.Result;
            double avgKps = hasKps ? analysis.AverageKps : 0;
            double maxKps = hasKps ? analysis.MaxKps : 0;
            double xxyStarRating = resolveXxyStarRating(beatmap, analysis, hasKps, modsAffect, starRating);
            long durationMs = resolveDurationMs(score, beatmap);

            double? avgAbsOffsetMs = null;
            if (cachedAvgAbsOffsets != null && cachedAvgAbsOffsets.TryGetValue(score.ID, out double storedOffset))
                avgAbsOffsetMs = storedOffset;

            result.DrillScores.Add(EzLocalProfileDrillScoreRow.FromScore(
                score,
                username,
                pp,
                hasKps ? analysis : default,
                hasKps,
                avgAbsOffsetMs,
                starRating,
                xxyStarRating));

            var rulesetStats = getOrCreate(result.RulesetStats, rulesetId, () => new EzLocalProfileAggregationResult.MutableRulesetStats());
            rulesetStats.TotalKeys += keys;
            rulesetStats.ScoreCount++;
            rulesetStats.TotalPp += pp;
            rulesetStats.TotalDurationMs += durationMs;

            if (hasKps)
            {
                rulesetStats.KpsSum += avgKps;
                rulesetStats.KpsSampleCount++;
                if (maxKps > rulesetStats.MaxKps)
                    rulesetStats.MaxKps = maxKps;
            }

            incrementGrade(result, rulesetId, score.Rank);
            incrementStar(result, rulesetId, starRating);
            incrementXxy(result, rulesetId, xxyStarRating);

            if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
            {
                int keyCount = resolveManiaKeyCount(score, analysis, hasKps);
                accumulateMania(result, keyCount, analysis, hasKps, keys, avgKps, maxKps, pp, durationMs);
            }

            if (rulesetId == EzLocalProfileConstants.OSU_RULESET_ID)
                accumulateStdAttr(result, beatmap, score);
        }

        /// <summary>
        /// Lightweight mania <see cref="ScoreInfo"/> collect for included usernames (no PP / analysis).
        /// Used to materialize Track All after an incremental per-user recompute.
        /// </summary>
        public Dictionary<string, List<ScoreInfo>> CollectManiaScoresByUsername(
            IReadOnlyCollection<string> usernames,
            CancellationToken cancellationToken = default)
        {
            var includeSet = new HashSet<string>(usernames.Select(normaliseUsername), StringComparer.Ordinal);
            var maniaScoresByUser = new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);

            if (includeSet.Count == 0)
                return maniaScoresByUser;

            realm.Run(r =>
            {
                foreach (var score in queryValidScores(r))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (score.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
                        continue;

                    string username = normaliseUsername(score.RealmUser.Username);
                    if (!includeSet.Contains(username))
                        continue;

                    if (score.BeatmapInfo == null)
                        continue;

                    if (!maniaScoresByUser.TryGetValue(username, out var list))
                        maniaScoresByUser[username] = list = new List<ScoreInfo>();

                    list.Add(score.DeepClone());
                }
            });

            return maniaScoresByUser;
        }

        public HashSet<long> CollectLocalOnlineScoreIds()
        {
            return realm.Run(r =>
            {
                var ids = new HashSet<long>();

                foreach (var score in r.All<ScoreInfo>().Where(s => !s.DeletePending && s.OnlineID > 0))
                    ids.Add(score.OnlineID);

                return ids;
            });
        }

        /// <summary>
        /// The subset of <paramref name="candidateIds"/> that also exists as a local score — all the online-contribution
        /// merge ever asks ("is this pulled summary superseded by a local play?").
        /// </summary>
        /// <remarks>
        /// <see cref="ScoreInfo.OnlineID"/> is indexed, so this is one point lookup per candidate instead of the
        /// whole-library scan <see cref="CollectLocalOnlineScoreIds()"/> performs. The per-play ingest path used to
        /// pay that scan on every settlement as soon as a single pulled contribution existed.
        /// </remarks>
        public HashSet<long> CollectLocalOnlineScoreIds(IReadOnlyCollection<long> candidateIds)
        {
            var candidates = candidateIds.Where(static id => id > 0).Distinct().ToList();

            if (candidates.Count == 0)
                return new HashSet<long>();

            return realm.Run(r =>
            {
                var ids = new HashSet<long>();

                foreach (long id in candidates)
                {
                    if (r.All<ScoreInfo>().Filter($"{nameof(ScoreInfo.OnlineID)} == $0", id).Count() > 0)
                        ids.Add(id);
                }

                return ids;
            });
        }

        /// <summary>
        /// Load one just-settled score the way the full compute would see it: the managed instance, with its persisted
        /// beatmap (and the Ez ratings stored on it) rather than the working copy gameplay still holds. Without this
        /// the incremental fold could persist a different slice than a rebuild of the same score.
        /// </summary>
        /// <returns>The detached score, or <see langword="null"/> when it is not a valid play.</returns>
        public ScoreInfo? LoadManagedScore(Guid scoreId)
        {
            return realm.Run(r =>
            {
                var score = r.Find<ScoreInfo>(scoreId);

                // Same validity gate as the bulk collector; anything it would skip must not be folded in either.
                if (score == null || score.DeletePending || score.BeatmapInfo?.Hash != score.BeatmapHash)
                    return null;

                return score.DeepClone();
            });
        }

        public static void MergeOnlineContributions(
            EzLocalProfileAggregationResult result,
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> contributions,
            HashSet<long> localOnlineIds)
        {
            mergeOnlineContributions(result, contributions, localOnlineIds);
        }

        private CachedAnalysis resolveAnalysis(
            ScoreInfo score,
            bool modsAffect,
            EzLocalProfileAggregationState state,
            CancellationToken cancellationToken)
        {
            var beatmap = score.BeatmapInfo!;

            if (!modsAffect)
            {
                // Read through the run's session: repeated scores of one chart hit the memo, not SQLite.
                var session = state.AnalysisSession;

                if (session != null)
                {
                    bool hasCachedKps = session.TryGet(beatmap, out var stored);
                    return new CachedAnalysis(hasCachedKps ? stored : default, hasCachedKps);
                }

                bool hasKps = analysisStore.TryGet(beatmap, out var fallback);
                return new CachedAnalysis(hasKps ? fallback : default, hasKps);
            }

            var cache = state.AnalysisCache;
            string cacheKey = $"{beatmap.ID:N}|{score.Ruleset.ShortName}|{score.ModsJson}";

            if (cache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var lookup = new EzAnalysisLookupCache(beatmap, score.Ruleset, score.Mods);
                var computed = EzAnalysisComputation.Compute(beatmapManager, lookup, cancellationToken);
                cached = new CachedAnalysis(computed, true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(
                    $"[EzLocalProfile] Playable analysis failed for score {score.ID}: {ex.Message}",
                    Ez2ConfigManager.LOGGER_NAME,
                    LogLevel.Verbose);
                cached = new CachedAnalysis(default, false);
            }

            cache[cacheKey] = cached;
            return cached;
        }

        private static int resolveManiaKeyCount(ScoreInfo score, EzAnalysisResult analysis, bool hasKps)
        {
            try
            {
                // ManiaRuleset.GetVariantForBeatmap == GetKeyCount (post converter-mod columns).
                int fromMods = score.Ruleset.CreateInstance().GetVariantForBeatmap(score.BeatmapInfo!, score.Mods);
                if (fromMods > 0)
                    return fromMods;
            }
            catch
            {
                // fall through
            }

            if (hasKps && analysis.ManiaSummary is { } summary && summary.ColumnCounts.Count > 0)
                return summary.ColumnCounts.Keys.Max() + 1;

            return (int)Math.Round(score.BeatmapInfo!.Difficulty.CircleSize);
        }

        private static long resolveDurationMs(ScoreInfo score, BeatmapInfo beatmap)
        {
            long durationMs = beatmap.Length > 0 ? (long)beatmap.Length : 0;
            if (durationMs <= 0)
                return 0;

            double rate = 1.0;

            foreach (var mod in score.Mods)
            {
                if (mod is IApplicableToRate applicableToRate)
                    rate = applicableToRate.ApplyToRate(0, rate);
            }

            if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0)
                return durationMs;

            return (long)Math.Round(durationMs / rate);
        }

        private static void mergeOnlineContributions(
            EzLocalProfileAggregationResult result,
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> contributions,
            HashSet<long> localOnlineIds)
        {
            foreach (var c in contributions)
            {
                if (c.OnlineId > 0 && localOnlineIds.Contains(c.OnlineId))
                    continue;

                int rulesetId = c.RulesetId;
                var rulesetStats = getOrCreate(result.RulesetStats, rulesetId, () => new EzLocalProfileAggregationResult.MutableRulesetStats());
                rulesetStats.TotalKeys += Math.Max(0, c.KeyCount);
                rulesetStats.ScoreCount++;
                rulesetStats.TotalPp += Math.Max(0, c.Pp);
                rulesetStats.TotalDurationMs += Math.Max(0, c.DurationMs);

                incrementGrade(result, rulesetId, c.Rank);
                incrementStar(result, rulesetId, c.StarRating);

                if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
                {
                    int keyMode = (int)Math.Round(c.CircleSize);

                    if (keyMode > 0)
                    {
                        var keyStats = getOrCreate(result.ManiaKeyStats, keyMode, () => new EzLocalProfileAggregationResult.MutableManiaKeyStats());
                        keyStats.TotalKeys += Math.Max(0, c.KeyCount);
                        keyStats.ScoreCount++;
                        keyStats.TotalPp += Math.Max(0, c.Pp);
                        keyStats.TotalDurationMs += Math.Max(0, c.DurationMs);
                    }
                }

                if (rulesetId == EzLocalProfileConstants.OSU_RULESET_ID)
                {
                    bool highGrade = isHighGrade(c.Rank);
                    addStd(result, EzLocalProfileStdAttr.ApproachRate, roundAttr(c.ApproachRate), highGrade);
                    addStd(result, EzLocalProfileStdAttr.CircleSize, roundAttr(c.CircleSize), highGrade);
                }
            }
        }

        private static void accumulateMania(
            EzLocalProfileAggregationResult result,
            int keyCount,
            EzAnalysisResult analysis,
            bool hasKps,
            long keys,
            double avgKps,
            double maxKps,
            double pp,
            long durationMs)
        {
            if (keyCount <= 0)
                return;

            var keyStats = getOrCreate(result.ManiaKeyStats, keyCount, () => new EzLocalProfileAggregationResult.MutableManiaKeyStats());
            keyStats.TotalKeys += keys;
            keyStats.ScoreCount++;
            keyStats.TotalPp += pp;
            keyStats.TotalDurationMs += durationMs;

            if (hasKps)
            {
                keyStats.KpsSum += avgKps;
                keyStats.KpsSampleCount++;
                if (maxKps > keyStats.MaxKps)
                    keyStats.MaxKps = maxKps;
            }

            if (!hasKps || analysis.ManiaSummary == null)
                return;

            var columnCounts = analysis.ManiaSummary.Value.ColumnCounts;
            long totalNotes = 0;
            foreach (int count in columnCounts.Values)
                totalNotes += count;

            if (totalNotes <= 0)
                return;

            foreach (var (column, count) in columnCounts)
            {
                var columnStats = getOrCreate(result.ManiaColumnStats, (keyCount, column), () => new EzLocalProfileAggregationResult.MutableManiaColumnStats());
                columnStats.TotalKeys += count;
                columnStats.ScoreCount++;

                // Density-proportional column KPS from chart-wide analysis (no per-column series in SQLite yet).
                double share = (double)count / totalNotes;
                double columnAvg = avgKps * share;
                double columnMax = maxKps * share;
                columnStats.KpsSum += columnAvg;
                columnStats.KpsSampleCount++;
                if (columnMax > columnStats.MaxKps)
                    columnStats.MaxKps = columnMax;
            }
        }

        private static void accumulateStdAttr(EzLocalProfileAggregationResult result, BeatmapInfo beatmap, ScoreInfo score)
        {
            var difficulty = new BeatmapDifficulty(beatmap.Difficulty);

            foreach (var mod in score.Mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(difficulty);

            bool highGrade = isHighGrade(score.Rank);
            addStd(result, EzLocalProfileStdAttr.ApproachRate, roundAttr(difficulty.ApproachRate), highGrade);
            addStd(result, EzLocalProfileStdAttr.CircleSize, roundAttr(difficulty.CircleSize), highGrade);
        }

        private static void addStd(EzLocalProfileAggregationResult result, EzLocalProfileStdAttr attr, double value, bool highGrade)
        {
            var stats = getOrCreate(result.StdAttrAffinities, (attr, value), () => new EzLocalProfileAggregationResult.MutableStdAttr());
            stats.PlayCount++;
            if (highGrade)
                stats.HighGradeCount++;
        }

        private static void incrementGrade(EzLocalProfileAggregationResult result, int rulesetId, ScoreRank rank)
        {
            var key = (rulesetId, rank);
            result.GradeCounts.TryGetValue(key, out int existing);
            result.GradeCounts[key] = existing + 1;
        }

        private static void incrementStar(EzLocalProfileAggregationResult result, int rulesetId, double starRating)
        {
            if (starRating < 0)
                return;

            int bucket = (int)Math.Floor(starRating);
            var key = (rulesetId, bucket);
            result.StarPlayCounts.TryGetValue(key, out int existing);
            result.StarPlayCounts[key] = existing + 1;
        }

        private static void incrementXxy(EzLocalProfileAggregationResult result, int rulesetId, double xxyStarRating)
        {
            if (xxyStarRating < 0)
                return;

            int bucket = (int)Math.Floor(xxyStarRating);
            var key = (rulesetId, bucket);
            result.XxyPlayCounts.TryGetValue(key, out int existing);
            result.XxyPlayCounts[key] = existing + 1;
        }

        /// <summary>
        /// Resolve xxy SR for bucket counting. With beatmap-affecting mods, prefer playable analysis
        /// (not Realm NoMod <see cref="BeatmapInfo.XxyStarRating"/>).
        /// </summary>
        private static double resolveXxyStarRating(
            BeatmapInfo beatmap,
            EzAnalysisResult analysis,
            bool hasKps,
            bool modsAffectAnalysis,
            double resolvedStarRating)
        {
            if (modsAffectAnalysis)
            {
                if (hasKps && analysis.ManiaSummary?.XxySr is double analysisXxy && analysisXxy >= 0)
                    return analysisXxy;

                return resolvedStarRating >= 0 ? resolvedStarRating : -1;
            }

            if (beatmap.XxyStarRating >= 0)
                return beatmap.XxyStarRating;

            if (hasKps && analysis.ManiaSummary?.XxySr is double storedXxy && storedXxy >= 0)
                return storedXxy;

            // Align play counts with official star buckets when dedicated xxy is unavailable.
            if (beatmap.StarRating >= 0)
                return beatmap.StarRating;

            return -1;
        }

        private static IQueryable<ScoreInfo> queryValidScores(Realm realm)
        {
            return realm.All<ScoreInfo>()
                        .Filter($"{nameof(ScoreInfo.DeletePending)} == false"
                                + $" && {nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.Hash)} == {nameof(ScoreInfo.BeatmapHash)}");
        }

        private static string normaliseUsername(string? username)
            => EzLocalProfileConstants.NormaliseUsername(username);

        private static long countKeys(ScoreInfo score)
        {
            long fromMaximum = sumCountable(score.MaximumStatistics);
            if (fromMaximum > 0)
                return fromMaximum;

            return sumCountable(score.Statistics);
        }

        private static long sumCountable(IReadOnlyDictionary<HitResult, int> statistics)
        {
            long total = 0;

            foreach (var (result, count) in statistics)
            {
                if (result.IsScorable() && !result.IsBonus())
                    total += count;
            }

            return total;
        }

        private static double roundAttr(float value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

        private static bool isHighGrade(ScoreRank rank) => rank is ScoreRank.S or ScoreRank.SH or ScoreRank.X or ScoreRank.XH;

        private static TValue getOrCreate<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, Func<TValue> factory)
            where TKey : notnull
            where TValue : class
        {
            if (dict.TryGetValue(key, out var existing))
                return existing;

            var created = factory();
            dict[key] = created;
            return created;
        }

        internal readonly record struct CachedAnalysis(EzAnalysisResult Result, bool HasKps);
    }
}
