// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Chart-dan entry for song select and player credit.
    /// Prefer xxySR → Sunny interval tables when available; else MSD Overall heuristic.
    /// Skill family / OverallMsd always come from MSD when present.
    /// </summary>
    public sealed class EzChartDanEstimator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzBeatmapMinaComputer msdComputer;

        public EzChartDanEstimator(BeatmapManager beatmapManager, EzBeatmapMinaComputer msdComputer)
        {
            this.beatmapManager = beatmapManager;
            this.msdComputer = msdComputer;
        }

        /// <summary>
        /// Estimates chart dan for player clear credit (hub exclusive RC/LN primary).
        /// </summary>
        /// <remarks>
        /// Mods that affect the playable are rated from it directly; otherwise the stored axes are used.
        /// </remarks>
        public EzChartDanVerdict? TryEstimate(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            mods ??= Array.Empty<Mod>();
            bool live = EzModRate.AffectsChartSkills(mods);

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var playable = EzPlayableBeatmapCache.GetShared(working, beatmapInfo.Ruleset, mods);

            if (live)
            {
                var liveAxes = computeLiveAxes(playable, mods);

                return liveAxes == null ? null : FromMsdAndPlayable(liveAxes, playable, resolveLiveXxy(playable, beatmapInfo, mods));
            }

            var msd = msdComputer.TryGetOrCompute(beatmapInfo);

            if (msd == null || msd.Count == 0)
                return null;

            double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;
            return FromMsdAndPlayable(msd, playable, xxySr);
        }

        /// <summary>
        /// Axis values measured on a playable under the given mods, keyed by stored skill id.
        /// </summary>
        private static IReadOnlyDictionary<string, double>? computeLiveAxes(IBeatmap playable, IReadOnlyList<Mod> mods)
        {
            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (keyCount <= 0)
                return null;

            using var calc = new EzMinaEngine();

            if (!calc.SupportsKeyCount(keyCount))
                return null;

            try
            {
                var vector = calc.CalculateMsd(EzMinaNoteConverter.Convert(playable), keyCount, EzModRate.Resolve(mods));

                if (vector.Overall <= 0 && vector.Stream <= 0)
                    return null;

                return VectorToMsdDict(vector);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sunny star rating for the current mods, or the stored baseline when it still applies.
        /// </summary>
        private static double? resolveLiveXxy(IBeatmap playable, BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods)
        {
            if (EzAnalysisComputation.TryComputeXxySrFromPlayable(playable, beatmapInfo.Ruleset, mods, CancellationToken.None, out double liveXxy)
                && liveXxy > 0)
                return liveXxy;

            return EzModRate.CanUsePersistedXxy(mods) && beatmapInfo.XxyStarRating >= 0
                ? beatmapInfo.XxyStarRating
                : null;
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

        public static double ComputeHoldRatio(IBeatmap playable)
            => EzHoldMetrics.ComputeHoldRatio(playable);

        /// <summary>Column hold (LN) object count on <paramref name="playable"/>.</summary>
        public static int ComputeHoldCount(IBeatmap playable)
            => EzHoldMetrics.ComputeHoldCount(playable);

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
        public static int TryHoldCountFromBeatmapInfo(BeatmapInfo beatmapInfo)
            => EzHoldMetrics.TryHoldCountFromBeatmapInfo(beatmapInfo);

        public static IReadOnlyDictionary<string, double> VectorToMsdDict(EzMinaSkillAxisVector vector)
            => vector.Enumerate().ToDictionary(p => p.Axis.ToMsdSkillId(), p => p.Value, StringComparer.Ordinal);
    }
}
