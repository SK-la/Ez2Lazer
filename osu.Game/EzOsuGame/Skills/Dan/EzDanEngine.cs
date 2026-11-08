// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Chart-dan derivation from already-measured inputs. Pure: every function here takes its inputs and returns
    /// a verdict, with no lookup, no store and no side effect.
    /// </summary>
    /// <remarks>
    /// Prefers the SunnySR interval table when a star rating is available, else the MSD Overall heuristic. Skill
    /// family and OverallMsd always come from MSD when present.
    /// </remarks>
    public static class EzDanEngine
    {
        public static EzChartDanVerdict? FromMsdAndPlayable(
            IReadOnlyDictionary<string, double> msd,
            IBeatmap playable,
            double? sunnySR = null)
        {
            ArgumentNullException.ThrowIfNull(msd);
            ArgumentNullException.ThrowIfNull(playable);

            if (!msd.TryGetValue(EzMinaSkillAxis.Overall.ToMsdSkillId(), out double overall) || overall <= 0)
                return null;

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (keyCount <= 0)
                return null;

            double holdRatio = ComputeHoldRatio(playable);
            return FromMsd(msd, keyCount, holdRatio, sunnySR);
        }

        public static EzChartDanVerdict? FromMsd(
            IReadOnlyDictionary<string, double> msd,
            int keyCount,
            double holdRatio,
            double? sunnySR = null)
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

            if (sunnySR is double sr
                && EzSunnyDanIntervals.TryLookup(keyCount, side.ToId(), sr, out var sunny))
            {
                rawDan = sunny.RawDan;
                label = sunny.DisplayLabel;
            }
            else if (EzDanLabels.TryResolveFallbackDan(keyCount, side, overall, dominantAxis, sunnySR) is EzDanLabels.DanLookup fallback)
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
                AlgorithmVersion = EzAnalysisRevision.VERSION,
            };
        }

        public static double ComputeHoldRatio(IBeatmap playable) => EzHoldMetrics.ComputeHoldRatio(playable);

        /// <summary>Column hold (LN) object count on <paramref name="playable"/>.</summary>
        public static int ComputeHoldCount(IBeatmap playable) => EzHoldMetrics.ComputeHoldCount(playable);

        /// <summary>
        /// The inputs a chart-dan derivation needs when only stored data is available.
        /// </summary>
        /// <remarks>
        /// The background backfill and the read-path memory compute both come through here. Reading the same
        /// three inputs from two hand-written blocks is how one chart could be filed from the stored hold ratio
        /// while another was filed from the playable's.
        /// </remarks>
        public static bool TryResolveStoredInputs(BeatmapInfo beatmapInfo, out int keyCount, out double holdRatio, out double? sunnySR, out int holdCount)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            keyCount = EzHoldMetrics.ResolveKeyCount(beatmapInfo);

            holdRatio = EzHoldMetrics.HoldRatio(beatmapInfo);

            if (double.IsNaN(holdRatio))
                holdRatio = 0;

            sunnySR = beatmapInfo.EzRatingData.SunnySR >= 0 ? beatmapInfo.EzRatingData.SunnySR : null;
            holdCount = TryHoldCountFromBeatmapInfo(beatmapInfo);

            return keyCount > 0;
        }

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
