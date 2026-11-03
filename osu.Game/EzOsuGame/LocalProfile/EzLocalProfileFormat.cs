// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public static class EzLocalProfileFormat
    {
        public static string FormatPp(double pp) =>
            pp.ToString(pp >= 100 ? "N0" : "0.##", CultureInfo.CurrentCulture);

        public static string FormatDuration(long durationMs)
        {
            if (durationMs <= 0)
                return "0s";

            var span = TimeSpan.FromMilliseconds(durationMs);

            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";

            if (span.TotalMinutes >= 1)
                return $"{(int)span.TotalMinutes}m {span.Seconds}s";

            return $"{Math.Max(1, span.Seconds)}s";
        }
    }
}
