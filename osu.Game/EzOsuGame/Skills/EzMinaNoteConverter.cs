// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Converts a playable mania <see cref="IBeatmap"/> into MinaCalc rows
    /// (column bitmask + row time in seconds). Hold notes contribute their head only
    /// (lnTailTaps = false), matching mania-hub's default.
    /// </summary>
    public static class EzMinaNoteConverter
    {
        /// <summary>
        /// Builds MinaCalc rows, or empty when there are no column objects.
        /// </summary>
        /// <remarks>
        /// Rows are keyed by whole milliseconds (same as mania-hub) so a chord shares one bitmask.
        /// osu! charts may start before the audio lead-in; a negative row time walks MinaCalc's
        /// interval index out of bounds and traps the wasm, so such charts are shifted to start at 0
        /// (only inter-row gaps carry difficulty, so the offset is value-preserving).
        /// </remarks>
        public static EzCalcNote[] Convert(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            var byTimeMs = new SortedDictionary<long, uint>();

            foreach (HitObject obj in beatmap.HitObjects)
            {
                if (obj is not IHasColumn columnObj)
                    continue;

                int column = columnObj.Column;
                if (column < 0 || column >= 32)
                    continue;

                long timeMs = (long)Math.Round(obj.StartTime);

                byTimeMs.TryGetValue(timeMs, out uint mask);
                byTimeMs[timeMs] = mask | (1u << column);
            }

            if (byTimeMs.Count == 0)
                return Array.Empty<EzCalcNote>();

            // Rows are sorted, so the first entry decides the shift.
            long firstTimeMs = byTimeMs.Keys.First();
            long offsetMs = firstTimeMs < 0 ? -firstTimeMs : 0;

            var rows = new EzCalcNote[byTimeMs.Count];
            int index = 0;

            foreach (var (timeMs, mask) in byTimeMs)
                rows[index++] = new EzCalcNote(mask, (timeMs + offsetMs) / 1000f);

            return rows;
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
