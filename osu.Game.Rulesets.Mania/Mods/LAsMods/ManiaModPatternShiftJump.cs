// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftJump : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Jump;
        protected override string PatternName => "Jump";
        protected override string PatternAcronym => "PSJ";

        protected override int DefaultLevel => 4;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 1;
        protected override int DefaultWindowProcessOffset => 0;
        protected override int DefaultApplyOrder => 50;

        protected override void ApplyPatternForWindow(List<ManiaHitObject> windowObjects,
                                                      ManiaBeatmap beatmap,
                                                      double windowStart,
                                                      double windowEnd,
                                                      KeyPatternSettings settings,
                                                      Random rng,
                                                      int maxIterationsPerWindow)
        {
            if (windowObjects.Count == 0)
                return;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(windowStart).BeatLength;
            if (beatLength <= 0)
                return;

            double activeBeatFraction = ManiaKeyPatternHelp.GetActiveBeatFraction(windowObjects, beatmap, 1.0 / 4.0);
            double mainFraction = Math.Max(activeBeatFraction, 1.0 / 2.0);
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;

            if (subStep <= 0)
                return;

            int availableSteps = (int)Math.Floor((windowEnd - (windowStart + subStep)) / subStep) + 1;
            if (availableSteps <= 0)
                return;

            double t = windowStart + subStep;
            int minK = Math.Max(0, settings.MinK);
            int maxK = Math.Max(minK, settings.MaxK);

            relocateNotesToHalfOrBeat(windowObjects, beatmap, beatLength, windowStart, windowEnd, rng);
            applyJumpPattern(beatmap, t, beatLength, windowStart, windowEnd, minK, maxK, rng);
        }

        private static void relocateNotesToHalfOrBeat(List<ManiaHitObject> windowObjects,
                                                      ManiaBeatmap beatmap,
                                                      double beatLength,
                                                      double windowStart,
                                                      double windowEnd,
                                                      Random rng)
        {
            if (beatLength <= 0)
                return;

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            bool isOnHalfOrBeat(double t) => isOnDivisionLocal(t, 1) || isOnDivisionLocal(t, 2);

            bool isFinerThanQuarter(double t)
            {
                bool on1to4 = isOnDivisionLocal(t, 1)
                               || isOnDivisionLocal(t, 2)
                               || isOnDivisionLocal(t, 3)
                               || isOnDivisionLocal(t, 4);
                return !on1to4;
            }

            double getBeatAnchor(double t)
            {
                double anchor = Math.Floor(t / beatLength) * beatLength;
                return anchor < 0 ? 0 : anchor;
            }

            int countAtTime(double time)
            {
                var cols = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, time, TIME_TOLERANCE);
                return cols.Count;
            }

            foreach (var obj in windowObjects)
            {
                if (isOnHalfOrBeat(obj.StartTime))
                    continue;

                double anchor = getBeatAnchor(obj.StartTime);
                double half = anchor + (beatLength / 2.0);

                if (anchor < windowStart - TIME_TOLERANCE || anchor > windowEnd + TIME_TOLERANCE)
                    continue;

                if (half < windowStart - TIME_TOLERANCE || half > windowEnd + TIME_TOLERANCE)
                    continue;

                double primary = anchor;
                double secondary = half;

                if (isFinerThanQuarter(obj.StartTime))
                {
                    int countAnchor = countAtTime(anchor);
                    int countHalf = countAtTime(half);

                    if (countHalf < countAnchor)
                    {
                        primary = half;
                        secondary = anchor;
                    }
                    else if (countHalf == countAnchor)
                    {
                        double dAnchor = Math.Abs(obj.StartTime - anchor);
                        double dHalf = Math.Abs(obj.StartTime - half);

                        if (dHalf < dAnchor)
                        {
                            primary = half;
                            secondary = anchor;
                        }
                    }
                }
                else
                {
                    double dAnchor = Math.Abs(obj.StartTime - anchor);
                    double dHalf = Math.Abs(obj.StartTime - half);

                    if (dHalf < dAnchor)
                    {
                        primary = half;
                        secondary = anchor;
                    }
                }

                if (Math.Abs(obj.StartTime - primary) <= TIME_TOLERANCE)
                    continue;

                double offset = primary - obj.StartTime;

                if (!ManiaKeyPatternHelp.TryApplyDelayOffset(beatmap, obj, offset, out _))
                {
                    double secondaryOffset = secondary - obj.StartTime;
                    ManiaKeyPatternHelp.TryApplyDelayOffset(beatmap, obj, secondaryOffset, out _);
                }
            }
        }

        private static void applyJumpPattern(ManiaBeatmap beatmap,
                                             double time,
                                             double beatLength,
                                             double windowStart,
                                             double windowEnd,
                                             int minK,
                                             int maxK,
                                             Random rng)
        {
            if (beatLength <= 0)
                return;

            double anchor = Math.Floor(time / beatLength) * beatLength;
            double half = anchor + (beatLength / 2.0);

            double candidateTime = Math.Abs(time - half) < Math.Abs(time - anchor) ? half : anchor;
            if (candidateTime < windowStart - TIME_TOLERANCE || candidateTime > windowEnd + TIME_TOLERANCE)
                return;

            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var columns = new List<int>(totalColumns);

            for (int i = 0; i < totalColumns; i++)
                columns.Add(i);

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, columns.Count);

            for (int i = 0; i < count; i++)
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, columns, null, rng, candidateTime);
        }
    }
}
