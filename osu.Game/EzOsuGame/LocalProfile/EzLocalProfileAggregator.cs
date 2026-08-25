// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public class EzLocalProfileAggregator
    {
        private readonly RealmAccess realm;
        private readonly EzAnalysisPersistentStore analysisStore;

        public EzLocalProfileAggregator(RealmAccess realm, EzAnalysisPersistentStore analysisStore)
        {
            this.realm = realm;
            this.analysisStore = analysisStore;
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

        public EzLocalProfileAggregationResult Aggregate(IReadOnlyCollection<string> includedUsernames)
        {
            var includeSet = new HashSet<string>(includedUsernames.Select(normaliseUsername), StringComparer.Ordinal);
            var result = new EzLocalProfileAggregationResult
            {
                IncludedUsernames = includeSet.OrderBy(n => n, StringComparer.Ordinal).ToList(),
                ComputedAt = DateTimeOffset.UtcNow,
            };

            if (includeSet.Count == 0)
                return result;

            realm.Run(r =>
            {
                foreach (var score in queryValidScores(r))
                {
                    string username = normaliseUsername(score.RealmUser.Username);
                    if (!includeSet.Contains(username))
                        continue;

                    int rulesetId = score.Ruleset.OnlineID;
                    var beatmap = score.BeatmapInfo;
                    if (beatmap == null)
                        continue;

                    long keys = countKeys(score);
                    bool hasKps = analysisStore.TryGet(beatmap, out var analysis);
                    double avgKps = hasKps ? analysis.AverageKps : 0;
                    double maxKps = hasKps ? analysis.MaxKps : 0;

                    var rulesetStats = getOrCreate(result.RulesetStats, rulesetId, () => new EzLocalProfileAggregationResult.MutableRulesetStats());
                    rulesetStats.TotalKeys += keys;
                    rulesetStats.ScoreCount++;

                    if (hasKps)
                    {
                        rulesetStats.KpsSum += avgKps;
                        rulesetStats.KpsSampleCount++;
                        if (maxKps > rulesetStats.MaxKps)
                            rulesetStats.MaxKps = maxKps;
                    }

                    incrementGrade(result, rulesetId, score.Rank);
                    incrementStar(result, rulesetId, beatmap.StarRating);

                    if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
                        accumulateMania(result, beatmap, analysis, hasKps, keys, avgKps, maxKps);

                    if (rulesetId == EzLocalProfileConstants.OSU_RULESET_ID)
                        accumulateStdAttr(result, beatmap, score.Rank);
                }
            });

            return result;
        }

        private static void accumulateMania(
            EzLocalProfileAggregationResult result,
            BeatmapInfo beatmap,
            EzAnalysisResult analysis,
            bool hasKps,
            long keys,
            double avgKps,
            double maxKps)
        {
            int keyCount = (int)Math.Round(beatmap.Difficulty.CircleSize);
            if (keyCount <= 0)
                return;

            var keyStats = getOrCreate(result.ManiaKeyStats, keyCount, () => new EzLocalProfileAggregationResult.MutableManiaKeyStats());
            keyStats.TotalKeys += keys;
            keyStats.ScoreCount++;

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
            foreach (var count in columnCounts.Values)
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

        private static void accumulateStdAttr(EzLocalProfileAggregationResult result, BeatmapInfo beatmap, ScoreRank rank)
        {
            bool highGrade = isHighGrade(rank);
            addStd(result, EzLocalProfileStdAttr.ApproachRate, roundAttr(beatmap.Difficulty.ApproachRate), highGrade);
            addStd(result, EzLocalProfileStdAttr.CircleSize, roundAttr(beatmap.Difficulty.CircleSize), highGrade);
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

        private static bool isHighGrade(ScoreRank rank) =>
            rank is ScoreRank.S or ScoreRank.SH or ScoreRank.X or ScoreRank.XH;

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
    }
}
