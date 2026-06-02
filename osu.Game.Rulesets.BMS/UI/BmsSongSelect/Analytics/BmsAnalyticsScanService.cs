// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Rulesets.BMS.Beatmaps;

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
            RealmAccess? realm = null)
        {
            var charts = beatmapManager.GetAllCharts().ToList();
            if (charts.Count == 0)
                return Task.CompletedTask;

            return Task.Run(() => runOnBackgroundThread(charts, repository, audioManager, realm, progress, cancellationToken), cancellationToken);
        }

        private static void runOnBackgroundThread(
            IReadOnlyList<BMSChartCache> charts,
            BmsAnalyticsSqliteRepository repository,
            AudioManager audioManager,
            RealmAccess? realm,
            IProgress<BmsAnalyticsScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var scope = BmsAnalyticsScanContext.Enter(cancellationToken);

            int total = charts.Count;

            report(progress, 0, total, "准备分析…");

            try
            {
                for (int index = 0; index < total; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chart = charts[index];
                    int displayIndex = index + 1;

                    report(progress, index, total, $"[{displayIndex}/{total}] 开始: {chart.Title}");

                    string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                        ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                        : chart.Md5Hash;

                    try
                    {
                        using var heartbeat = startHeartbeat(progress, index, total, chart.Title, cancellationToken);
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
                            });

                            if (realm != null)
                                BmsAnalyticsRealmWriteback.TryApply(realm, chart, analyticsResult);
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

                    report(progress, displayIndex, total, $"[{displayIndex}/{total}] 完成: {chart.Title}");
                }

                report(progress, total, total, "分析完成");
            }
            catch (OperationCanceledException)
            {
                report(progress, 0, total, "分析已取消");
                throw;
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

                report(progress, completed, total, $"[{completed + 1}/{total}] 解析中: {title}");
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
