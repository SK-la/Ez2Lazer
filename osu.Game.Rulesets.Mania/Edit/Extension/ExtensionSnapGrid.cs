// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Screens.Edit;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// The beat divisor lines surrounding a single reference time, mirroring the playfield's beat snap grid.
    /// </summary>
    internal class ExtensionSnapGrid
    {
        /// <summary>
        /// How far either side of the reference time lines are shown, in milliseconds.
        /// </summary>
        private const double visible_range = 750;

        /// <summary>
        /// Upper bound on generated lines, guarding against degenerate beat lengths.
        /// </summary>
        private const int max_line_count = 512;

        public readonly List<ExtensionSnapLine> Lines = new List<ExtensionSnapLine>();

        private double builtForTime = double.NaN;
        private int builtForDivisor;

        /// <summary>
        /// Whether the current lines already describe <paramref name="referenceTime"/> at <paramref name="beatDivisor"/>.
        /// </summary>
        public bool IsValidFor(double referenceTime, int beatDivisor) => builtForTime == referenceTime && builtForDivisor == beatDivisor;

        public void Clear()
        {
            Lines.Clear();
            builtForTime = double.NaN;
            builtForDivisor = 0;
        }

        public void Rebuild(double referenceTime, ControlPointInfo controlPointInfo, int beatDivisor, bool fixedGrid, OsuColour colours)
        {
            Lines.Clear();

            builtForTime = referenceTime;
            builtForDivisor = beatDivisor;

            TimingControlPoint timingPoint = fixedGrid
                ? controlPointInfo.TimingPoints.Count > 0 ? controlPointInfo.TimingPoints[0] : TimingControlPoint.DEFAULT
                : controlPointInfo.TimingPointAt(referenceTime - visible_range);

            if (timingPoint.BeatLength <= 0)
                return;

            double time = timingPoint.Time;
            int beat = 0;

            // Progress time until the visible range is reached.
            while (time < referenceTime - visible_range && beat < max_line_count)
            {
                time += timingPoint.BeatLength / beatDivisor;
                beat++;
            }

            while (time < referenceTime + visible_range && Lines.Count < max_line_count)
            {
                if (!fixedGrid)
                {
                    var nextTimingPoint = controlPointInfo.TimingPointAt(time);

                    // Switch to the next timing point once it has been reached.
                    if (nextTimingPoint.Time > timingPoint.Time)
                    {
                        beat = 0;
                        time = nextTimingPoint.Time;
                        timingPoint = nextTimingPoint;
                    }
                }

                int divisor = BindableBeatDivisor.GetDivisorForBeatIndex(beat, beatDivisor);
                double separation = Math.Abs(time - referenceTime);

                Lines.Add(new ExtensionSnapLine(time, BindableBeatDivisor.GetColourFor(divisor, colours), (float)Math.Max(0, 1 - separation / visible_range)));

                beat++;
                time += timingPoint.BeatLength / beatDivisor;
            }
        }
    }

    /// <summary>
    /// A single beat divisor line: a time, its divisor colour and the opacity implied by its distance from the cursor.
    /// </summary>
    internal readonly struct ExtensionSnapLine
    {
        public readonly double Time;
        public readonly Color4 Colour;
        public readonly float Alpha;

        public ExtensionSnapLine(double time, Color4 colour, float alpha)
        {
            Time = time;
            Colour = colour;
            Alpha = alpha;
        }
    }
}
