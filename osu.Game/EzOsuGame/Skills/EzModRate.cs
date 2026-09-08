// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills
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

        public static bool IsNomodRate(float rate) => Math.Abs(rate - 1f) < 0.001f;
    }
}
