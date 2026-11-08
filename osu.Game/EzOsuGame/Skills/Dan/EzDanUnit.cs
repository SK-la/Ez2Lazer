// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Runs the dan system against the shared analysis: reads the verdict the pass already resolved for a chart.
    /// </summary>
    /// <remarks>
    /// This never measures anything itself: the Mina vector and the Sunny rating are produced once per chart and
    /// mods by the analysis pass, and the pass resolves both sides through one entry point. Reading it here is
    /// what keeps the HUD, the player credit and the persisted row from disagreeing about the same chart.
    /// </remarks>
    public sealed class EzDanUnit
    {
        private readonly IEzChartAnalysisLookup? analysisLookup;

        public EzDanUnit(IEzChartAnalysisLookup? analysisLookup = null)
        {
            this.analysisLookup = analysisLookup;
        }

        /// <summary>
        /// Estimates chart dan for a read path (HUD, labels): only ever uses an analysis that already exists.
        /// Null means the pass has not produced this chart yet; the caller treats that as "not available".
        /// </summary>
        public EzChartDanVerdict? TryEstimate(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            return analysisLookup?.TryGet(beatmapInfo, mods) is { } analysis
                ? fromAnalysis(analysis)
                : null;
        }

        /// <summary>
        /// Same estimate, but fills the shared analysis store when the inputs have not been measured yet.
        /// </summary>
        /// <remarks>
        /// Batch callers only (player aggregation), which run off the update thread. The measurement is the one the
        /// analysis pass would do, so a later HUD read of the same chart is a cache hit rather than a second pass.
        /// </remarks>
        public EzChartDanVerdict? TryEstimateOrCompute(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            return analysisLookup?.GetOrCompute(beatmapInfo, beatmapInfo.Ruleset, mods) is { } analysis
                ? fromAnalysis(analysis)
                : null;
        }

        private static EzChartDanVerdict? fromAnalysis(EzAnalysisResult analysis)
        {
            if (analysis.Dan is not { } dan || dan.Sides.Count == 0)
                return null;

            var side = dan.HoldRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(dan.KeyCount)
                ? EzDanSide.Ln
                : EzDanSide.Rc;

            var entry = dan.For(side);
            var resolvedSide = side;

            if (entry == null)
            {
                resolvedSide = side == EzDanSide.Ln ? EzDanSide.Rc : EzDanSide.Ln;
                entry = dan.For(resolvedSide);
            }

            if (entry == null)
                return null;

            return new EzChartDanVerdict
            {
                RawDan = entry.RawDan,
                Label = entry.Label,
                KeyCount = dan.KeyCount,
                Side = resolvedSide,
                DominantAxis = dan.DominantAxis,
                OverallMsd = dan.OverallMsd,
                HoldRatio = dan.HoldRatio,
                AlgorithmVersion = EzAnalysisRevision.VERSION,
            };
        }
    }
}
