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
    /// The beat divisor lines surrounding a reference time, mirroring the playfield's beat snap grid.
    /// </summary>
    /// <remarks>
    /// The playfield's grid is built inside the playfield's own scrolling containers. The dock draws its lines itself, so it
    /// reproduces the same generation rule (visible range, divisor colours, distance falloff) instead of sharing the
    /// playfield's grid instance, which cannot be scaled into the dock's space.
    /// </remarks>
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
        private double builtRangeStart = double.NaN;
        private double builtRangeEnd = double.NaN;
        private int builtForDivisor;

        /// <summary>
        /// Whether the current lines already describe <paramref name="referenceTime"/> at <paramref name="beatDivisor"/>.
        /// </summary>
        public bool IsValidFor(double referenceTime, int beatDivisor) => builtForTime == referenceTime && builtForDivisor == beatDivisor;

        /// <summary>
        /// Whether the current lines already describe the range [<paramref name="start"/>, <paramref name="end"/>].
        /// </summary>
        public bool IsValidForRange(double start, double end, int beatDivisor)
            => builtRangeStart == start && builtRangeEnd == end && builtForDivisor == beatDivisor;

        public void Clear()
        {
            Lines.Clear();
            builtForTime = double.NaN;
            builtRangeStart = double.NaN;
            builtRangeEnd = double.NaN;
            builtForDivisor = 0;
        }

        /// <summary>
        /// Rebuilds the lines around a single reference time, as used while hovering the columns.
        /// </summary>
        public void Rebuild(double referenceTime, ControlPointInfo controlPointInfo, int beatDivisor, bool fixedGrid, OsuColour colours)
        {
            builtForTime = referenceTime;
            builtRangeStart = double.NaN;
            builtRangeEnd = double.NaN;
            builtForDivisor = beatDivisor;

            build(referenceTime, referenceTime, controlPointInfo, beatDivisor, fixedGrid, colours);
        }

        /// <summary>
        /// Rebuilds the lines across a time band, as used while a selection owns them.
        /// </summary>
        public void RebuildForRange(double start, double end, ControlPointInfo controlPointInfo, int beatDivisor, bool fixedGrid, OsuColour colours)
        {
            builtForTime = double.NaN;
            builtRangeStart = start;
            builtRangeEnd = end;
            builtForDivisor = beatDivisor;

            build(start, end, controlPointInfo, beatDivisor, fixedGrid, colours);
        }

        /// <summary>
        /// Generates the divisor lines for [<paramref name="rangeStart"/>, <paramref name="rangeEnd"/>], with a
        /// <see cref="visible_range"/> falloff on either side. A degenerate band reproduces the cursor's own lines.
        /// </summary>
        private void build(double rangeStart, double rangeEnd, ControlPointInfo controlPointInfo, int beatDivisor, bool fixedGrid, OsuColour colours)
        {
            Lines.Clear();

            TimingControlPoint timingPoint = fixedGrid
                ? controlPointInfo.TimingPoints.Count > 0 ? controlPointInfo.TimingPoints[0] : TimingControlPoint.DEFAULT
                : controlPointInfo.TimingPointAt(rangeStart - visible_range);

            if (timingPoint.BeatLength <= 0)
                return;

            double step = timingPoint.BeatLength / beatDivisor;

            if (step <= 0)
                return;

            double time = timingPoint.Time;
            int beat = 0;

            // Skip to the window in one step. A fixed grid is anchored at the map's first timing point, so stepping beat by
            // beat would exhaust the line budget long before the cursor on a long map.
            if (time < rangeStart - visible_range)
            {
                long skipped = (long)Math.Floor((rangeStart - visible_range - time) / step);

                if (skipped > 0)
                {
                    time += skipped * step;
                    beat = (int)Math.Min(int.MaxValue, skipped);
                }
            }

            while (time < rangeEnd + visible_range && Lines.Count < max_line_count)
            {
                if (!fixedGrid)
                {
                    var nextTimingPoint = controlPointInfo.TimingPointAt(time);

                    // switch to the next timing point once it has been reached.
                    if (nextTimingPoint.Time > timingPoint.Time)
                    {
                        beat = 0;
                        time = nextTimingPoint.Time;
                        timingPoint = nextTimingPoint;
                        step = timingPoint.BeatLength / beatDivisor;
                    }
                }

                int divisor = BindableBeatDivisor.GetDivisorForBeatIndex(beat, beatDivisor);
                double separation = time < rangeStart ? rangeStart - time : time > rangeEnd ? time - rangeEnd : 0;

                Lines.Add(new ExtensionSnapLine(time, BindableBeatDivisor.GetColourFor(divisor, colours), (float)Math.Max(0, 1 - separation / visible_range)));

                beat++;
                time += step;
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
