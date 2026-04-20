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

            if (lookup.Ruleset.OnlineID != 3)
                return false;

            PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (analysisBeatmap.HitObjects.Count == 0)
                return false;

            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (!XxySrCalculatorBridge.TryCalculate(lookup.Ruleset, analysisBeatmap, rate, out double sr) || double.IsNaN(sr) || double.IsInfinity(sr))
                return false;

            xxySr = sr;
            return true;
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

            bool shouldCalculateXxy = !onlyKps && lookup.Ruleset.OnlineID == 3;

            if (shouldCalculateXxy && analysisBeatmap.HitObjects.Count > 0 && XxySrCalculatorBridge.TryCalculate(lookup.Ruleset, analysisBeatmap, rate, out double sr) && !double.IsNaN(sr)
                && !double.IsInfinity(sr))
                xxySr = sr;

            cancellationToken.ThrowIfCancellationRequested();

            kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

            var commonSummary = new KpsSummary(averageKps, maxKps, kpsList);
            EzManiaSummary? maniaSummary = onlyKps
                ? null
                : new EzManiaSummary(columnCounts, holdNoteCounts, xxySr);

            return new EzAnalysisResult(commonSummary, maniaSummary);
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
