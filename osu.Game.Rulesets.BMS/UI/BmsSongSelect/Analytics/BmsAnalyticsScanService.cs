// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Localization;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    public sealed class BmsAnalyticsScanProgress
    {
        public double Progress { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public static class BmsAnalyticsScanService
    {
        public static bool IsRunning => BmsAnalyticsScanContext.IsRunning;

        public static Task RunAsync(
            BMSBeatmapManager beatmapManager,
            BmsAnalyticsSqliteRepository repository,
            AudioManager audioManager,
            IProgress<BmsAnalyticsScanProgress>? progress = null,
            CancellationToken cancellationToken = default,
            RealmAccess? realm = null,
            EzAnalysisDatabase? analysisDatabase = null)
        {
            if (beatmapManager.ChartCount == 0)
                return Task.CompletedTask;

            return Task.Run(() => runOnBackgroundThread(beatmapManager, repository, audioManager, realm, analysisDatabase, progress, cancellationToken), cancellationToken);
        }

        private static void runOnBackgroundThread(
            BMSBeatmapManager beatmapManager,
            BmsAnalyticsSqliteRepository repository,
            AudioManager audioManager,
            RealmAccess? realm,
            EzAnalysisDatabase? analysisDatabase,
            IProgress<BmsAnalyticsScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!BmsAnalyticsScanContext.TryEnter(cancellationToken, out IDisposable? scope) || scope == null)
            {
                report(progress, 0, 1, BmsStrings.ANALYTICS_BUILDING.ToString());
                return;
            }

            using (scope)
            {
                int total = beatmapManager.ChartCount;
                report(progress, 0, total, BmsStrings.ANALYTICS_PREPARING.ToString());

                try
                {
                    const int page_size = 128;
                    int completed = 0;
                    int analyzed = 0;
                    var writebackBatch = new List<(BMSChartCache Chart, string PathKey, BmsChartAnalyticsResult Result)>(page_size);

                    for (int offset = 0; ; offset += page_size)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IReadOnlyList<BMSChartCache> charts = beatmapManager.GetChartPage(offset, page_size);

                        if (charts.Count == 0)
                            break;

                        writebackBatch.Clear();

                        foreach (BMSChartCache chart in charts)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int displayIndex = completed + 1;
                            string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                                ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                                : chart.Md5Hash;
                            long fileSize = chart.FileSize;
                            long modifiedTicks = chart.LastModified.ToUniversalTime().Ticks;

                            if (repository.IsUpToDate(pathKey, fileSize, modifiedTicks))
                            {
                                completed++;
                                report(progress, completed, total, BmsStrings.Analytics_ChartFinished(completed, total, chart.Title));
                                continue;
                            }

                            report(progress, completed, total, BmsStrings.Analytics_ChartStarted(displayIndex, total, chart.Title));

                            try
                            {
                                using var heartbeat = startHeartbeat(progress, completed, total, chart.Title, cancellationToken);
                                var result = BmsChartAnalyticsProcessor.TryAnalyze(chart, audioManager, cancellationToken);

                                if (result != null)
                                {
                                    var analyticsResult = result.Value;

                                    repository.Upsert(new BmsAnalyticsRecord
                                    {
                                        PathKey = pathKey,
                                        Pp = analyticsResult.Pp,
                                        XxySr = analyticsResult.XxySr,
                                        AvgKps = analyticsResult.AvgKps,
                                        MaxKps = analyticsResult.MaxKps,
                                        StarRating = analyticsResult.StarRating,
                                        ColumnCountsJson = analyticsResult.ColumnCountsJson,
                                        KpsListJson = analyticsResult.KpsListJson,
                                        FileSize = fileSize,
                                        LastModifiedTicks = modifiedTicks,
                                    });

                                    writebackBatch.Add((chart, pathKey, analyticsResult));
                                    analyzed++;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"[BMS] Analytics scan failed for {chart.FullPath}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                            }

                            completed++;
                            report(progress, completed, total, BmsStrings.Analytics_ChartFinished(completed, total, chart.Title));
                        }

                        if (writebackBatch.Count > 0)
                        {
                            if (realm != null && analysisDatabase != null)
                                BmsAnalyticsStandardPipeline.BulkCommitResults(realm, analysisDatabase, writebackBatch);
                            else if (realm != null)
                                BmsAnalyticsRealmWriteback.TryApplyBatch(realm, writebackBatch);
                        }

                        if (charts.Count < page_size)
                            break;
                    }

                    Logger.Log($"[BMS] Analytics scan finished: analyzed {analyzed}/{total}.", LoggingTarget.Runtime, LogLevel.Debug);
                    report(progress, total, total, BmsStrings.ANALYTICS_COMPLETE.ToString());
                }
                catch (OperationCanceledException)
                {
                    report(progress, 0, total, BmsStrings.ANALYTICS_CANCELLED_SHORT.ToString());
                    throw;
                }
            }
        }

        /// <summary>
        /// While a single chart is parsing (can take minutes), keep nudging the UI so the notification does not look frozen.
        /// </summary>
        private static IDisposable startHeartbeat(IProgress<BmsAnalyticsScanProgress>? progress, int completed, int total, string title, CancellationToken cancellationToken)
        {
            return new Timer(_ =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                report(progress, completed, total, BmsStrings.Analytics_ChartParsing(completed + 1, total, title));
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private static void report(IProgress<BmsAnalyticsScanProgress>? progress, int completed, int total, string status)
        {
            progress?.Report(new BmsAnalyticsScanProgress
            {
                Progress = total > 0 ? (double)completed / total : 0,
                Status = status,
            });
        }
    }
}
