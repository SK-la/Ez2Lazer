// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.SongSelect;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    /// <summary>
    /// Commits BMS offline analytics into the generic external-ruleset Realm + ez-analysis pipeline.
    /// </summary>
    internal static class BmsAnalyticsStandardPipeline
    {
        public static void TryCommitChart(
            RealmAccess realm,
            EzAnalysisDatabase analysisDatabase,
            BMSChartCache chart,
            string pathKey,
            BmsChartAnalyticsResult analyticsResult)
        {
            Guid beatmapId = BmsAnalyticsRealmWriteback.GetDeterministicBeatmapId(chart.FullPath);
            var payload = createPayload(pathKey, analyticsResult);

            if (analysisDatabase.TryCommitExternalOfflineAnalysis(realm, beatmapId, payload))
                return;

            Logger.Log(
                $"[BMS] Standard analysis commit skipped: beatmap {beatmapId} not in Realm (chart={chart.FullPath}). Sync library to Realm before building analytics.",
                LoggingTarget.Database,
                LogLevel.Debug);
        }

        public static void BulkCommitFromRepository(
            RealmAccess realm,
            EzAnalysisDatabase analysisDatabase,
            IReadOnlyList<BMSChartCache> charts,
            BmsAnalyticsSqliteRepository repository)
        {
            var records = repository.LoadAll();

            foreach (var chart in charts)
            {
                string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                    ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                    : chart.Md5Hash;

                if (!records.TryGetValue(pathKey, out var record))
                    continue;

                Guid beatmapId = BmsAnalyticsRealmWriteback.GetDeterministicBeatmapId(chart.FullPath);
                var payload = createPayload(record);

                analysisDatabase.TryCommitExternalOfflineAnalysis(realm, beatmapId, payload);
            }
        }

        private static EzExternalBeatmapAnalysisPayload createPayload(string pathKey, BmsChartAnalyticsResult analyticsResult)
        {
            var record = new BmsAnalyticsRecord
            {
                PathKey = pathKey,
                Pp = analyticsResult.Pp,
                XxySr = analyticsResult.XxySr,
                AvgKps = analyticsResult.AvgKps,
                MaxKps = analyticsResult.MaxKps,
                StarRating = analyticsResult.StarRating,
                ColumnCountsJson = analyticsResult.ColumnCountsJson,
                KpsListJson = analyticsResult.KpsListJson,
            };

            return createPayload(record, analyticsResult.StarRating, analyticsResult.XxySr, analyticsResult.Pp);
        }

        private static EzExternalBeatmapAnalysisPayload createPayload(BmsAnalyticsRecord record)
            => createPayload(record, record.StarRating, record.XxySr, record.Pp);

        private static EzExternalBeatmapAnalysisPayload createPayload(BmsAnalyticsRecord record, double? starRating, double? xxySr, double? pp)
        {
            EzAnalysisResult? slice = BmsAnalyticsEzConverter.ToEzAnalysisResult(record);
            return new EzExternalBeatmapAnalysisPayload(starRating, xxySr, pp, slice);
        }
    }
}
