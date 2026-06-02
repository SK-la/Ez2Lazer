// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using osu.Framework.Audio;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    public readonly record struct BmsChartAnalyticsResult(
        double? Pp,
        double? XxySr,
        double? AvgKps,
        double? MaxKps,
        double? StarRating,
        string? ColumnCountsJson,
        string? KpsListJson);

    /// <summary>
    /// Chart-stream analytics: parse BMS → mania conversion → EZ metrics. No <see cref="BMSWorkingBeatmap"/> / keysound preload.
    /// </summary>
    public static class BmsChartAnalyticsProcessor
    {
        public static BmsChartAnalyticsResult? TryAnalyze(
            BMSChartCache chart,
            AudioManager audioManager,
            CancellationToken cancellationToken = default)
        {
            if (BmsChartFileParser.TryParse(chart.FullPath, cancellationToken) is not BMSBeatmap bmsBeatmap)
                return null;

            applyChartMetadata(bmsBeatmap, chart);

            var maniaBeatmap = ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(bmsBeatmap);
            applyPlayableDefaults(maniaBeatmap, cancellationToken);

            var maniaRuleset = new ManiaRuleset();
            var beatmapInfo = createBeatmapInfo(chart, maniaBeatmap);
            var working = new BmsAnalyticsManiaWorkingBeatmap(beatmapInfo, maniaBeatmap, audioManager);

            double star = maniaRuleset.CreateDifficultyCalculator(working)
                                      .Calculate(Array.Empty<Mod>()).StarRating;

            double? pp = null;
            double? xxySr = null;
            double? avgKps = null;
            double? maxKps = null;
            string? columnJson = null;
            string? kpsListJson = null;

            try
            {
                var scoreInfo = new ScoreInfo
                {
                    Ruleset = maniaRuleset.RulesetInfo,
                    Statistics =
                    {
                        [HitResult.Perfect] = Math.Max(1, chart.TotalNotes > 0 ? chart.TotalNotes : maniaBeatmap.HitObjects.Count)
                    }
                };
                pp = new BMSRuleset().CreatePerformanceCalculator().Calculate(scoreInfo, working).Total;
            }
            catch
            {
                // PP is optional for analytics rows.
            }

            var analysisProvider = new BMSRuleset().CreateEzAnalysisProvider();
            var request = new EzAnalysisRequest(maniaBeatmap, 1.0, EzAnalysisScope.XxySr | EzAnalysisScope.RulesetSpecificRadarData);

            if (analysisProvider.TryCompute(request, cancellationToken, out var analysis)
                && analysis.TryGetValue(EzAnalysisFields.XXY_SR, out double sr))
                xxySr = sr;

            var (avg, max, kpsList) = OptimizedBeatmapCalculator.GetKpsOptimized(maniaBeatmap);
            avgKps = avg;
            maxKps = max;

            if (kpsList.Count > 0)
                kpsListJson = JsonSerializer.Serialize(kpsList);

            var columnCounts = OptimizedBeatmapCalculator.GetColumnNoteCountsOptimized(maniaBeatmap);
            columnJson = JsonSerializer.Serialize(columnCounts);

            return new BmsChartAnalyticsResult(pp, xxySr, avgKps, maxKps, star, columnJson, kpsListJson);
        }

        private static void applyChartMetadata(BMSBeatmap bmsBeatmap, BMSChartCache chart)
        {
            if (chart.Bpm > 0)
                bmsBeatmap.BeatmapInfo.BPM = chart.Bpm;

            if (chart.TotalNotes > 0)
            {
                bmsBeatmap.BeatmapInfo.TotalObjectCount = chart.TotalNotes;
                bmsBeatmap.BeatmapInfo.EndTimeObjectCount = chart.TotalNotes;
            }

            if (!string.IsNullOrEmpty(chart.Md5Hash))
            {
                bmsBeatmap.BeatmapInfo.MD5Hash = chart.Md5Hash;
                bmsBeatmap.BeatmapInfo.Hash = chart.Md5Hash;
            }
        }

        private static BeatmapInfo createBeatmapInfo(BMSChartCache chart, ManiaBeatmap maniaBeatmap)
        {
            var info = maniaBeatmap.BeatmapInfo;
            info.Difficulty.CircleSize = maniaBeatmap.TotalColumns;

            if (chart.Bpm > 0)
                info.BPM = chart.Bpm;

            if (!string.IsNullOrEmpty(chart.Md5Hash))
            {
                info.MD5Hash = chart.Md5Hash;
                info.Hash = chart.Md5Hash;
            }

            return info;
        }

        private static void applyPlayableDefaults(ManiaBeatmap maniaBeatmap, CancellationToken cancellationToken)
        {
            var ruleset = new ManiaRuleset();
            var processor = ruleset.CreateBeatmapProcessor(maniaBeatmap);
            processor?.PreProcess();

            foreach (var obj in maniaBeatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                obj.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty, cancellationToken);
            }

            processor?.PostProcess();
        }
    }
}
