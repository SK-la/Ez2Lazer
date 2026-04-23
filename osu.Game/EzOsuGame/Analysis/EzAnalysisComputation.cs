// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.EzOsuGame.Analysis
{
    // 不主动释放 workingCache，让框架自行决定。主动释放会频繁触发大量 Invalidating 日志。
    internal static class EzAnalysisComputation
    {
        public static bool TryComputeXxySr(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double xxySr)
        {
            xxySr = 0;

            PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out double? computedXxySr)
                || computedXxySr is not double resolvedXxySr)
                return false;

            xxySr = resolvedXxySr;
            return true;
        }

        public static bool TryComputeXxySrAndPp(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                out double? xxySr, out double? pp)
        {
            PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return tryComputeOperationValues(workingBeatmap, analysisBeatmap, lookup, cancellationToken, out xxySr, out pp);
        }

        public static EzAnalysisResult Compute(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
            => Compute(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo), lookup, cancellationToken);

        public static EzAnalysisResult Compute(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            PlayableCachedWorkingBeatmap playableWorkingBeatmap = new PlayableCachedWorkingBeatmap(workingBeatmap);

            bool onlyKps = lookup.Ruleset.OnlineID != 3;
            IBeatmap analysisBeatmap = playableWorkingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(analysisBeatmap, onlyKps: onlyKps);

            // 将速率调整 mod 应用到分析结果，保持与实际游玩时长一致。
            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (!Precision.AlmostEquals(rate, 1.0))
            {
                averageKps *= rate;
                maxKps *= rate;

                for (int i = 0; i < kpsList.Count; i++)
                    kpsList[i] *= rate;
            }

            double? xxySr = null;
            double? pp = tryComputePerfectPp(playableWorkingBeatmap, analysisBeatmap, lookup, cancellationToken);

            if (!onlyKps)
                tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out xxySr);

            cancellationToken.ThrowIfCancellationRequested();

            kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

            var commonSummary = new KpsSummary(averageKps, maxKps, kpsList);
            EzManiaSummary? maniaSummary = onlyKps
                ? null
                : new EzManiaSummary(columnCounts, holdNoteCounts, xxySr);

            return new EzAnalysisResult(commonSummary, pp, maniaSummary);
        }

        public static bool TryComputeRulesetSpecificRadarData(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                              out EzRulesetSpecificRadarResult radarResult)
        {
            radarResult = default;

            PlayableCachedWorkingBeatmap playableWorkingBeatmap = new PlayableCachedWorkingBeatmap(workingBeatmap);
            IBeatmap analysisBeatmap = playableWorkingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            return EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset, new EzAnalysisRequest(analysisBeatmap, rate), EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT,
                cancellationToken, out radarResult);
        }

        private static double getRateAdjustMultiplier(Mod[] mods)
        {
            try
            {
                double rate = 1.0;

                for (int i = 0; i < mods.Length; i++)
                {
                    if (mods[i] is IApplicableToRate applicableToRate)
                        rate = applicableToRate.ApplyToRate(0, rate);
                }

                if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0)
                    return 1.0;

                return rate;
            }
            catch
            {
                return 1.0;
            }
        }

        private static bool tryComputeOperationValues(PlayableCachedWorkingBeatmap playableWorkingBeatmap, IBeatmap analysisBeatmap,
                                                      in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                      out double? xxySr, out double? pp)
        {
            pp = tryComputePerfectPp(playableWorkingBeatmap, analysisBeatmap, lookup, cancellationToken);

            return tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out xxySr) || pp != null;
        }

        private static bool tryComputeXxySr(IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double? xxySr)
        {
            xxySr = null;

            if (analysisBeatmap.HitObjects.Count == 0)
                return false;

            if (lookup.Ruleset.OnlineID != 3)
                return false;

            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset, new EzAnalysisRequest(analysisBeatmap, rate), EzAnalysisFields.XXY_SR, cancellationToken, out double sr))
                xxySr = sr;

            return xxySr != null;
        }

        private static double? tryComputePerfectPp(PlayableCachedWorkingBeatmap playableWorkingBeatmap, IBeatmap analysisBeatmap,
                                                   in EzAnalysisLookupCache lookup, CancellationToken cancellationToken)
        {
            var ruleset = lookup.Ruleset.CreateInstance();

            if (ruleset == null)
                return null;

            var performanceCalculator = ruleset.CreatePerformanceCalculator();

            if (performanceCalculator == null)
                return null;

            var difficulty = ruleset.CreateDifficultyCalculator(playableWorkingBeatmap).Calculate(lookup.OrderedMods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = lookup.OrderedMods;
            scoreProcessor.ApplyBeatmap(analysisBeatmap);
            cancellationToken.ThrowIfCancellationRequested();

            ScoreInfo perfectScore = new ScoreInfo(lookup.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = lookup.OrderedMods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics
            };

            var performance = performanceCalculator.Calculate(perfectScore, difficulty);
            cancellationToken.ThrowIfCancellationRequested();

            return double.IsFinite(performance.Total) ? performance.Total : null;
        }

        private class PlayableCachedWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private IBeatmap? playable;

            public PlayableCachedWorkingBeatmap(IWorkingBeatmap working)
            {
                this.working = working;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods) => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) =>
                playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => working.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => working.BeatmapLoaded;
            bool IWorkingBeatmap.TrackLoaded => working.TrackLoaded;
            IBeatmap IWorkingBeatmap.Beatmap => working.Beatmap;
            Texture IWorkingBeatmap.GetBackground() => working.GetBackground();
            Texture IWorkingBeatmap.GetPanelBackground() => working.GetPanelBackground();
            Waveform IWorkingBeatmap.Waveform => working.Waveform;
            Storyboard IWorkingBeatmap.Storyboard => working.Storyboard;
            ISkin IWorkingBeatmap.Skin => working.Skin;
            Track IWorkingBeatmap.Track => working.Track;
            Track IWorkingBeatmap.LoadTrack() => working.LoadTrack();
            Stream IWorkingBeatmap.GetStream(string storagePath) => working.GetStream(storagePath);
            void IWorkingBeatmap.BeginAsyncLoad() => working.BeginAsyncLoad();
            void IWorkingBeatmap.CancelAsyncLoad() => working.CancelAsyncLoad();
            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double? offsetFromPreviewPoint) => working.PrepareTrackForPreview(looping, offsetFromPreviewPoint);
        }
    }
}
