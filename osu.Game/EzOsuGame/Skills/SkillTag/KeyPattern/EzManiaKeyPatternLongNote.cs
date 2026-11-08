// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern
{
    internal static class EzManiaKeyPatternLongNote
    {
        /// <summary>
        /// The hold share of every hit object in the chart, as a percentage.
        /// </summary>
        public static double Compute(EzManiaChartInput chart)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var notes = chart.Notes;
            int noteObjectCount = notes.Count;
            int holdNoteCount = 0;

            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].IsHold)
                    holdNoteCount++;
            }

            if (noteObjectCount == 0)
                return 0;

            return Math.Round(holdNoteCount / (double)noteObjectCount * 100, 2, MidpointRounding.AwayFromZero);
        }
    }
}
