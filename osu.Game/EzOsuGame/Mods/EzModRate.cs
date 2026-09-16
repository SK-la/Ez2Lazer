// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Mods
{
    /// <summary>
    /// Shared DT/HT rate resolution for MSD/SSR/Dan (clamp matches lazer ModRateAdjust bounds).
    /// </summary>
    public static class EzModRate
    {
        /// <summary>
        /// Resolves the effective rate multiplier from a set of mods, optionally clamping the result to a specified range.
        /// </summary>
        public static float Resolve(IEnumerable<Mod>? mods, bool clamp = true, double min = 0.5, double max = 2.0)
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

            return clamp ? (float)Math.Clamp(rate, min, max) : (float)rate;
        }

        /// <summary>
        /// Resolves the effective rate multiplier from a set of mods, returning a bindable value that stays in sync with live rate changes.
        /// </summary>
        public static Bindable<float> ResolveBindable(IEnumerable<Mod>? mods, bool clamp = true, double min = 0.5, double max = 2.0)
        {
            var rateAdjustMods = mods?.OfType<ModRateAdjust>().ToArray() ?? Array.Empty<ModRateAdjust>();
            var bindable = new Bindable<float>(Resolve(rateAdjustMods, clamp, min, max));

            void updateValue() => bindable.Value = Resolve(rateAdjustMods, clamp, min, max);

            foreach (var mod in rateAdjustMods)
                mod.SpeedChange.BindValueChanged(_ => updateValue(), true);

            return bindable;
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
