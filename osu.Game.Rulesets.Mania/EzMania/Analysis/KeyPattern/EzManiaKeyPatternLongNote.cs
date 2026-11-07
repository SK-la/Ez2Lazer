// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal static class EzManiaKeyPatternLongNote
    {
        public static double Compute(ManiaBeatmap beatmap)
        {
            int noteObjectCount = 0;
            int holdNoteCount = 0;

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                switch (beatmap.HitObjects[i])
                {
                    case HoldNote:
                        holdNoteCount++;
                        noteObjectCount++;
                        break;

                    case Note:
                        noteObjectCount++;
                        break;
                }
            }

            if (noteObjectCount == 0)
                return 0;

            return Math.Round(holdNoteCount / (double)noteObjectCount * 100, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The same ratio from a lossless extraction. The ratio counts every hit object, so a filtered view would
        /// divide by a smaller denominator; the caller only reaches here when nothing was dropped.
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
