// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Chart-dan entry for song select and player credit.
    /// Prefer xxySR → Sunny interval tables when available; else MSD Overall heuristic.
    /// Skill family / OverallMsd always come from MSD when present.
    /// </summary>
    public sealed class EzChartDanEstimator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzBeatmapMsdComputer msdComputer;

        public EzChartDanEstimator(BeatmapManager beatmapManager, EzBeatmapMsdComputer msdComputer)
        {
            this.beatmapManager = beatmapManager;
            this.msdComputer = msdComputer;
        }

        /// <summary>
        /// Estimates chart dan. <paramref name="mods"/> affect rate (MSD) and key/LN classification.
        /// </summary>
        public EzChartDanVerdict? TryEstimate(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            mods ??= Array.Empty<Mod>();
            float rate = EzModRate.Resolve(mods);

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset, mods);

            IReadOnlyDictionary<string, double>? msd;

            if (EzModRate.IsNomodRate(rate))
            {
                msd = msdComputer.TryGetOrCompute(beatmapInfo);
            }
            else
            {
                using var calc = new EzMinaCalcFacade();
                var vector = calc.CalculateMsd(playable, rate);
                if (vector.Overall <= 0 && vector.Stream <= 0)
                    return null;

                msd = VectorToMsdDict(vector);
            }

            if (msd == null || msd.Count == 0)
                return null;

            double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;
            return FromMsdAndPlayable(msd, playable, xxySr);
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

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
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
            else
            {
                rawDan = EzDanLabels.SrToRawDan(overall, dominantAxis);
                label = EzDanLadders.For(keyCount, side).ParseLabel(rawDan);
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

        public static double ComputeHoldRatio(IBeatmap playable)
        {
            ArgumentNullException.ThrowIfNull(playable);

            int total = 0;
            int holds = 0;

            foreach (HitObject obj in playable.HitObjects)
            {
                if (obj is not IHasColumn)
                    continue;

                total++;

                if (obj is IHasDuration duration && duration.Duration > 0)
                    holds++;
            }

            return total <= 0 ? 0 : (double)holds / total;
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

        public static IReadOnlyDictionary<string, double> VectorToMsdDict(EzSkillsetVector vector)
            => vector.Enumerate().ToDictionary(p => p.Axis.ToMsdSkillId(), p => p.Value, StringComparer.Ordinal);
    }
}
