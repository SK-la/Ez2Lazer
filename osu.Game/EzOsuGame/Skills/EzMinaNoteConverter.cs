// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MinaCalc;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Converts a playable mania <see cref="IBeatmap"/> into MinaCalc note rows
    /// (column bitmask + absolute time in seconds). Hold notes contribute their head only
    /// (lnTailTaps = false), matching mania-hub default.
    /// </summary>
    public static class EzMinaNoteConverter
    {
        /// <summary>
        /// Builds MinaCalc rows. Returns empty when there are no column objects.
        /// </summary>
        public static MinaCalcNote[] Convert(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            // Group by start time (ms → seconds row). Same-time chords share one bitmask.
            var byTime = new SortedDictionary<double, uint>();

            foreach (HitObject obj in beatmap.HitObjects)
            {
                if (obj is not IHasColumn columnObj)
                    continue;

                int column = columnObj.Column;
                if (column < 0 || column >= 32)
                    continue;

                double timeSec = obj.StartTime / 1000.0;
                // Snap near-equal times so floating chord members stay one row.
                double key = Math.Round(timeSec, 6);

                byTime.TryGetValue(key, out uint mask);
                byTime[key] = mask | (1u << column);
            }

            if (byTime.Count == 0)
                return Array.Empty<MinaCalcNote>();

            return byTime.Select(kvp => new MinaCalcNote
            {
                Notes = kvp.Value,
                RowTime = (float)kvp.Key,
            }).ToArray();
        }

        public static int ResolveKeyCount(IBeatmap beatmap)
        {
            int fromCs = (int)Math.Round(beatmap.Difficulty.CircleSize);
            if (fromCs is >= 1 and <= 18)
                return fromCs;

            int maxColumn = -1;

            foreach (HitObject obj in beatmap.HitObjects)
            {
                if (obj is IHasColumn columnObj)
                    maxColumn = Math.Max(maxColumn, columnObj.Column);
            }

            return maxColumn >= 0 ? maxColumn + 1 : 4;
        }
    }
}
