// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Mods
{
    /// <summary>
    /// Shared DT/HT rate resolution for MSD/SSR/Dan (clamp matches lazer ModRateAdjust bounds).
    /// </summary>
    public static class EzModRate
    {
        public static float Resolve(IEnumerable<Mod>? mods)
        {
            double rate = 1;

            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    if (mod is ModRateAdjust rateAdjust)
                        rate *= rateAdjust.SpeedChange.Value;
                }
            }

            return (float)Math.Clamp(rate, 0.5, 2.0);
        }

        public static bool IsNoModRate(float rate) => Math.Abs(rate - 1f) < 0.001f;

        /// <summary>
        /// Mods that change playable layout, rate, or difficulty — song-select Skill radar / DualPanel
        /// should live-recompute chart MSD/ChartDan (same idea as xxySR analysis).
        /// </summary>
        public static bool AffectsChartSkills(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return false;

            return mods.Any(AffectsChartSkills);
        }

        public static bool AffectsChartSkills(Mod mod)
            => mod is IApplicableToRate
                      or IApplicableToBeatmapConverter
                      or IApplicableAfterBeatmapConversion
                      or IApplicableToDifficulty
                      or IApplicableToBeatmapProcessor
                      or IApplicableToHitObject
                      or IApplicableToBeatmap;

        /// <summary>Key-conversion / post-convert mods — persisted xxySR must not be reused.</summary>
        public static bool ChangesPlayableKeys(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return false;

            return mods.Any(static m => m is IApplicableToBeatmapConverter or IApplicableAfterBeatmapConversion);
        }

        /// <summary>Whether BeatmapInfo.XxyStarRating (nomod baseline) may be reused without live recompute.</summary>
        public static bool CanUsePersistedXxy(IEnumerable<Mod>? mods)
            => IsNoModRate(Resolve(mods)) && !ChangesPlayableKeys(mods);
    }
}
