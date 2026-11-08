// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Chart-dan entry for song select and player credit.
    /// Prefer xxySR → Sunny interval tables when available; else MSD Overall heuristic.
    /// Skill family / OverallMsd always come from MSD when present.
    /// </summary>
    /// <remarks>
    /// This reads the shared live analysis (<see cref="IEzChartAnalysisLookup"/>) rather than measuring anything:
    /// the Mina vector and the Sunny rating are produced once per chart + mods by the analysis pass.
    /// </remarks>
    public sealed class EzChartDanEstimator
    {
        private readonly IEzChartAnalysisLookup? analysisLookup;

        public EzChartDanEstimator(IEzChartAnalysisLookup? analysisLookup = null)
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
            // The pass already resolved every side through the single entry point. Reading it here is what keeps
            // the HUD, the player credit and the persisted row from disagreeing about the same chart.
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
                AlgorithmVersion = EzDanAlgorithm.VERSION,
            };
        }

        public static EzChartDanVerdict? FromMsdAndPlayable(
            IReadOnlyDictionary<string, double> msd,
            IBeatmap playable,
            double? xxySr = null)
        {
            ArgumentNullException.ThrowIfNull(msd);
            ArgumentNullException.ThrowIfNull(playable);

            if (!msd.TryGetValue(EzMinaSkillAxis.Overall.ToMsdSkillId(), out double overall) || overall <= 0)
                return null;

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);
            if (keyCount <= 0)
                return null;

            double holdRatio = ComputeHoldRatio(playable);
            return FromMsd(msd, keyCount, holdRatio, xxySr);
        }

        public static EzChartDanVerdict? FromMsd(
            IReadOnlyDictionary<string, double> msd,
            int keyCount,
            double holdRatio,
            double? xxySr = null)
        {
            ArgumentNullException.ThrowIfNull(msd);

            if (keyCount <= 0)
                return null;

            if (!msd.TryGetValue(EzMinaSkillAxis.Overall.ToMsdSkillId(), out double overall) || overall <= 0)
                return null;

            var dominantAxis = EzDanLabels.DominantAxis(msd);
            var side = holdRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(keyCount)
                ? EzDanSide.Ln
                : EzDanSide.Rc;

            double rawDan;
            string label;

            if (xxySr is double sr
                && EzSunnyDanIntervals.TryLookup(keyCount, side.ToId(), sr, out var sunny))
            {
                rawDan = sunny.RawDan;
                label = sunny.DisplayLabel;
            }
            else if (EzDanLabels.TryResolveFallbackDan(keyCount, side, overall, dominantAxis, xxySr) is EzDanLabels.DanLookup fallback)
            {
                rawDan = fallback.RawDan;
                label = fallback.Label;
            }
            else
            {
                // 5K / 8K+ without a star rating: no table of its own and nothing to read it off
                // → no invented dan.
                return null;
            }

            return new EzChartDanVerdict
            {
                RawDan = rawDan,
                Label = label,
                KeyCount = keyCount,
                Side = side,
                DominantAxis = dominantAxis,
                OverallMsd = overall,
                HoldRatio = holdRatio,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
            };
        }

        public static double ComputeHoldRatio(IBeatmap playable) => EzHoldMetrics.ComputeHoldRatio(playable);

        /// <summary>
        /// The inputs a chart-dan derivation needs when only stored data is available.
        /// </summary>
        /// <remarks>
        /// The background backfill and the read-path memory compute both come through here. Reading the same
        /// three inputs from two hand-written blocks is how one chart could be filed from the stored hold ratio
        /// while another was filed from the playable's.
        /// </remarks>
        public static bool TryResolveStoredInputs(BeatmapInfo beatmapInfo, out int keyCount, out double holdRatio, out double? xxySr, out int holdCount)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            keyCount = EzHoldMetrics.ResolveKeyCount(beatmapInfo);

            holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            xxySr = beatmapInfo.SunnyStarRating >= 0 ? beatmapInfo.SunnyStarRating : null;
            holdCount = TryHoldCountFromBeatmapInfo(beatmapInfo);

            return keyCount > 0;
        }

        /// <summary>Column hold (LN) object count on <paramref name="playable"/>.</summary>
        public static int ComputeHoldCount(IBeatmap playable) => EzHoldMetrics.ComputeHoldCount(playable);

        /// <summary>Hold ratio from cached mania column/LN counts when available.</summary>
        public static double? TryHoldRatioFromManiaSummary(EzManiaSummary summary)
        {
            if (!summary.HasHoldNoteCounts)
                return null;

            int total = 0;
            int holds = 0;

            foreach (var kvp in summary.ColumnCounts)
                total += kvp.Value;

            foreach (var kvp in summary.HoldNoteCounts)
                holds += kvp.Value;

            if (total <= 0)
                return null;

            return (double)holds / total;
        }

        /// <summary>Hold object count from mania summary when available.</summary>
        public static int? TryHoldCountFromManiaSummary(EzManiaSummary summary)
        {
            if (!summary.HasHoldNoteCounts)
                return null;

            int holds = 0;

            foreach (var kvp in summary.HoldNoteCounts)
                holds += kvp.Value;

            return holds;
        }

        /// <summary>
        /// Same LN count source as song-select <c>ln&gt;</c> (<see cref="BeatmapInfo.EndTimeObjectCount"/>).
        /// Returns &lt; 0 when unset (−1 sentinel). No playable load.
        /// </summary>
        public static int TryHoldCountFromBeatmapInfo(BeatmapInfo beatmapInfo) => EzHoldMetrics.TryHoldCountFromBeatmapInfo(beatmapInfo);

        public static IReadOnlyDictionary<string, double> VectorToMsdDict(EzMinaSkillAxisVector vector)
            => vector.Enumerate().ToDictionary(p => p.Axis.ToMsdSkillId(), p => p.Value, StringComparer.Ordinal);
    }
}
