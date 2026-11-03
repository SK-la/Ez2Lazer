// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>Abramowitz &amp; Stegun 7.1.26 (shared by SSR goal / AggregateSSRs).</summary>
    internal static class EzAbramowitzErf
    {
        public static double Erf(double x)
        {
            int sign = x < 0 ? -1 : 1;
            double ax = Math.Abs(x);
            double t = 1 / (1 + 0.3275911 * ax);
            double poly = ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t;
            return sign * (1 - poly * Math.Exp(-ax * ax));
        }
    }
}
