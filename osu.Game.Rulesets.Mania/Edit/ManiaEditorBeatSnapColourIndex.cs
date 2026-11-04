// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Maps a hit object time to the same beat-line divisor index used by the editor beat snap grid.
    /// </summary>
    public static class ManiaEditorBeatSnapColourIndex
    {
        /// <summary>
        /// Returns the divisor colour key (1, 2, 4, …) for a time on the editor snap grid (same rule as beat snap lines).
        /// </summary>
        public static int GetLineDivisorAtTime(ControlPointInfo controlPointInfo, double time, int beatDivisor, bool fixedGrid)
        {
            if (beatDivisor <= 0)
                return 1;

            TimingControlPoint timingPoint = fixedGrid
                ? controlPointInfo.TimingPoints.Count > 0
                    ? controlPointInfo.TimingPoints[0]
                    : TimingControlPoint.DEFAULT
                : controlPointInfo.TimingPointAt(time);

            double step = timingPoint.BeatLength / beatDivisor;

            if (step <= 0)
                return 1;

            double snappedTime = controlPointInfo.GetClosestSnappedTime(time, beatDivisor);

            int beat;

            if (fixedGrid)
            {
                beat = (int)Math.Round((snappedTime - timingPoint.Time) / step, MidpointRounding.AwayFromZero);

                if (beat < 0)
                    beat = 0;
            }
            else
            {
                timingPoint = controlPointInfo.TimingPointAt(snappedTime);
                step = timingPoint.BeatLength / beatDivisor;

                if (step <= 0)
                    return 1;

                beat = (int)Math.Round((snappedTime - timingPoint.Time) / step, MidpointRounding.AwayFromZero);

                if (beat < 0)
                    beat = 0;
            }

            return BindableBeatDivisor.GetDivisorForBeatIndex(beat, beatDivisor);
        }
    }
}
