// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    public sealed class BmsAnalyticsScanProgress
    {
        public double Progress { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public static class BmsAnalyticsScanService
    {
        /// <summary>
        /// Chart-only analysis is CPU-bound once keysound preload is disabled; allow more parallelism than disk-heavy scans.
        /// </summary>
        private static readonly int max_parallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

        public static async Task RunAsync(
            BMSBeatmapManager beatmapManager,
            BmsAnalyticsSqliteRepository repository,
            AudioManager audioManager,
            IRenderer renderer,
            IProgress<BmsAnalyticsScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var charts = beatmapManager.GetAllCharts().ToList();
            if (charts.Count == 0)
                return;

            var ruleset = new BMSRuleset();
            var performanceCalculator = ruleset.CreatePerformanceCalculator();
            var analysisProvider = ruleset.CreateEzAnalysisProvider();
            var maniaRuleset = new ManiaRuleset();

            using var gate = new SemaphoreSlim(max_parallelism);
            int completed = 0;

            var tasks = charts.Select(async chart =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                        ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                        : chart.Md5Hash;

                    // Chart stream only: no background textures, no keysound preload (avoids mass audio IO).
                    var bmsWorking = new BMSWorkingBeatmap(chart.FullPath, audioManager, renderer: null, chart);
                    var maniaWorking = new ManiaConvertedWorkingBeatmap(bmsWorking, audioManager, preloadKeysounds: false);
                    var playable = maniaWorking.GetPlayableBeatmap(maniaRuleset.RulesetInfo);

                    double star = maniaRuleset.CreateDifficultyCalculator(maniaWorking)
                                              .Calculate(Array.Empty<Mod>()).StarRating;

                    double? pp = null;
                    double? xxySr = null;
                    double? avgKps = null;
                    double? maxKps = null;
                    string? columnJson = null;

                    try
                    {
                        var scoreInfo = new ScoreInfo
                        {
                            Ruleset = maniaRuleset.RulesetInfo,
                            Statistics =
                            {
                                [HitResult.Perfect] = Math.Max(1, chart.TotalNotes)
                            }
                        };
                        pp = performanceCalculator.Calculate(scoreInfo, maniaWorking).Total;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[BMS] Analytics PP failed for {chart.FullPath}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                    }

                    if (playable is ManiaBeatmap maniaBeatmap)
                    {
                        var request = new EzAnalysisRequest(maniaBeatmap, 1.0, EzAnalysisScope.XxySr | EzAnalysisScope.RulesetSpecificRadarData);
                        if (analysisProvider.TryCompute(request, cancellationToken, out var analysis)
                            && analysis.TryGetValue(EzAnalysisFields.XXY_SR, out double sr))
                            xxySr = sr;

                        var (avg, max, _) = OptimizedBeatmapCalculator.GetKpsOptimized(maniaBeatmap);
                        avgKps = avg;
                        maxKps = max;

                        var columnCounts = OptimizedBeatmapCalculator.GetColumnNoteCountsOptimized(maniaBeatmap);
                        columnJson = JsonSerializer.Serialize(columnCounts);
                    }

                    repository.Upsert(new BmsAnalyticsRecord
                    {
                        PathKey = pathKey,
                        Pp = pp,
                        XxySr = xxySr,
                        AvgKps = avgKps,
                        MaxKps = maxKps,
                        StarRating = star,
                        ColumnCountsJson = columnJson,
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BMS] Analytics scan failed for {chart.FullPath}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
                finally
                {
                    gate.Release();
                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(new BmsAnalyticsScanProgress
                    {
                        Progress = (double)done / charts.Count,
                        Status = $"分析中 {done}/{charts.Count}: {chart.Title}",
                    });
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
