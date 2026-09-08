// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Public chart-dan entry for song select and player credit. Swap internals later (LeoBlack / rate MSD)
    /// without changing consumers that read <see cref="EzChartDanVerdict"/>.
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
        /// Estimates chart dan for a beatmap. MSD is currently NoMod 1.0x only; <paramref name="mods"/>
        /// only affect key count / LN-side classification via the playable beatmap.
        /// </summary>
        public EzChartDanVerdict? TryEstimate(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var msd = msdComputer.TryGetOrCompute(beatmapInfo);
            if (msd == null || msd.Count == 0)
                return null;

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset, mods ?? Array.Empty<Mod>());
            return FromMsdAndPlayable(msd, playable);
        }

        /// <summary>
        /// Shared path used by player aggregation when MSD and playable are already loaded.
        /// </summary>
        public static EzChartDanVerdict? FromMsdAndPlayable(IReadOnlyDictionary<string, double> msd, IBeatmap playable)
        {
            ArgumentNullException.ThrowIfNull(msd);
            ArgumentNullException.ThrowIfNull(playable);

            if (!msd.TryGetValue(EzSkillIds.Msd(EzSkillIds.OVERALL), out double overall) || overall <= 0)
                return null;

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
            if (keyCount <= 0)
                return null;

            double holdRatio = ComputeHoldRatio(playable);
            return FromMsd(msd, keyCount, holdRatio);
        }

        /// <summary>
        /// Pure mapping for tests and callers that already know key/LN ratio.
        /// </summary>
        public static EzChartDanVerdict? FromMsd(IReadOnlyDictionary<string, double> msd, int keyCount, double holdRatio)
        {
            ArgumentNullException.ThrowIfNull(msd);

            if (keyCount <= 0)
                return null;

            if (!msd.TryGetValue(EzSkillIds.Msd(EzSkillIds.OVERALL), out double overall) || overall <= 0)
                return null;

            string family = EzDanLabels.DominantFamily(msd);
            double rawDan = EzDanLabels.SrToRawDan(overall, family);
            string side = holdRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(keyCount)
                ? DanSkillSystem.SIDE_LN
                : DanSkillSystem.SIDE_RC;

            return new EzChartDanVerdict
            {
                RawDan = rawDan,
                Label = EzDanLabels.LabelFor(rawDan, side, keyCount),
                KeyCount = keyCount,
                Side = side,
                Family = family,
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
    }
}
