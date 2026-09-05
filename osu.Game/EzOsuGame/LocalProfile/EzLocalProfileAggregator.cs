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
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public class EzLocalProfileAggregator
    {
        private readonly RealmAccess realm;
        private readonly EzAnalysisPersistentStore analysisStore;
        private readonly BeatmapManager beatmapManager;
        private readonly ScoreManager scoreManager;
        private readonly IEzReplaySession replaySession;
        private readonly EzLocalProfilePpResolver ppResolver;

        public EzLocalProfileAggregator(
            RealmAccess realm,
            EzAnalysisPersistentStore analysisStore,
            BeatmapManager beatmapManager,
            ScoreManager scoreManager,
            IEzReplaySession replaySession)
        {
            this.realm = realm;
            this.analysisStore = analysisStore;
            this.beatmapManager = beatmapManager;
            this.scoreManager = scoreManager;
            this.replaySession = replaySession;
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
        /// Aggregate only the given usernames, returning one result slice per username (all scores counted).
        /// Does not merge online contributions — that happens when rebuilding display totals.
        /// </summary>
        public Dictionary<string, EzLocalProfileAggregationResult> AggregateByUsername(
            IReadOnlyCollection<string> usernames,
            IProgress<EzLocalProfileComputeProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var includeSet = new HashSet<string>(usernames.Select(normaliseUsername), StringComparer.Ordinal);
            var byUser = new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);
            var detachedScores = new List<(string Username, ScoreInfo Score)>();

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

                    detachedScores.Add((username, score.DeepClone()));
                }
            });

            foreach (string username in includeSet)
            {
                byUser[username] = new EzLocalProfileAggregationResult
                {
                    IncludedUsernames = new[] { username },
                    ComputedAt = DateTimeOffset.UtcNow,
                };
            }

            int total = Math.Max(1, detachedScores.Count);
            int processed = 0;

            void report() => progress?.Report(new EzLocalProfileComputeProgress(processed, total, Saving: false));

            report();

            var scoreList = detachedScores.Select(s => s.Score).ToList();
            var resolved = ppResolver.ResolveAll(scoreList, progress, total, cancellationToken);
            processed = scoreList.Count;
            report();

            var analysisCache = new Dictionary<string, CachedAnalysis>(StringComparer.Ordinal);

            for (int i = 0; i < detachedScores.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (username, score) = detachedScores[i];
                var result = byUser[username];
                int rulesetId = score.Ruleset.OnlineID;
                var beatmap = score.BeatmapInfo!;
                bool modsAffect = EzLocalProfileModAffects.AffectsPlayableAnalysis(score.Mods);

                long keys = countKeys(score);
                var cached = resolveAnalysis(score, modsAffect, analysisCache, cancellationToken);
                bool hasKps = cached.HasKps;
                var analysis = cached.Result;
                double avgKps = hasKps ? analysis.AverageKps : 0;
                double maxKps = hasKps ? analysis.MaxKps : 0;
                double pp = resolved.Pp[i];
                double starRating = resolved.StarRatings[i] >= 0 ? resolved.StarRatings[i] : beatmap.StarRating;
                double xxyStarRating = resolveXxyStarRating(beatmap, analysis, hasKps, modsAffect, starRating);
                long durationMs = resolveDurationMs(score, beatmap);

                double? avgAbsOffsetMs = EzLocalProfileHitEventResolver.ResolveAvgAbsOffsetMs(
                    score,
                    realm,
                    scoreManager,
                    beatmapManager,
                    replaySession,
                    cancellationToken);

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

            return byUser;
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
            Dictionary<string, CachedAnalysis> cache,
            CancellationToken cancellationToken)
        {
            var beatmap = score.BeatmapInfo!;

            if (!modsAffect)
            {
                bool hasKps = analysisStore.TryGet(beatmap, out var stored);
                return new CachedAnalysis(hasKps ? stored : default, hasKps);
            }

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
        {
            if (string.IsNullOrWhiteSpace(username))
                return EzLocalProfileConstants.UNKNOWN_USERNAME;

            return username.Trim();
        }

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

        private readonly record struct CachedAnalysis(EzAnalysisResult Result, bool HasKps);
    }
}
