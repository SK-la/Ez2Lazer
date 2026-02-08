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

            remapFineNotes(windowObjects, beatmap, beatLength);

            double targetTime = getJumpTargetTime(windowObjects, t, beatLength);
            applyJumpPattern(beatmap, targetTime, minK, maxK, rng);
        }

        private static void applyJumpPattern(ManiaBeatmap beatmap, double time, int minK, int maxK, Random rng)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var columns = new List<int>(totalColumns);

            for (int i = 0; i < totalColumns; i++)
                columns.Add(i);

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, columns.Count);

            for (int i = 0; i < count; i++)
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, columns, null, rng, time);
        }

        private static void remapFineNotes(List<ManiaHitObject> windowObjects, ManiaBeatmap beatmap, double beatLength)
        {
            if (beatLength <= 0)
                return;

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            foreach (var obj in windowObjects)
            {
                if (obj is not Note note)
                    continue;

                bool isOn1To4 = isOnDivisionLocal(note.StartTime, 1)
                                || isOnDivisionLocal(note.StartTime, 2)
                                || isOnDivisionLocal(note.StartTime, 3)
                                || isOnDivisionLocal(note.StartTime, 4);

                if (isOn1To4)
                    continue;

                double beatStart = Math.Floor(note.StartTime / beatLength) * beatLength;
                if (beatStart < 0)
                    beatStart = 0;

                double half = beatStart + (beatLength / 2.0);
                double beatEnd = beatStart + beatLength;

                int leftCount = 0;
                int rightCount = 0;

                foreach (var other in windowObjects)
                {
                    if (other.StartTime < beatStart - TIME_TOLERANCE || other.StartTime > beatEnd + TIME_TOLERANCE)
                        continue;

                    if (other.StartTime < half - TIME_TOLERANCE)
                        leftCount++;
                    else
                        rightCount++;
                }

                double primaryTarget = leftCount <= rightCount ? beatStart : half;
                double secondaryTarget = leftCount <= rightCount ? half : beatStart;

                if (!tryMoveNoteToTime(beatmap, note, primaryTarget))
                    tryMoveNoteToTime(beatmap, note, secondaryTarget);
            }
        }

        private static bool tryMoveNoteToTime(ManiaBeatmap beatmap, Note note, double targetTime)
        {
            if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, note.Column, targetTime, note, TIME_TOLERANCE))
                return false;

            if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, note.Column, targetTime, note, TIME_TOLERANCE))
                return false;

            note.StartTime = targetTime;
            return true;
        }

        private static double getJumpTargetTime(List<ManiaHitObject> windowObjects, double time, double beatLength)
        {
            if (beatLength <= 0)
                return time;

            double beatStart = Math.Floor(time / beatLength) * beatLength;
            if (beatStart < 0)
                beatStart = 0;

            double half = beatStart + (beatLength / 2.0);
            double beatEnd = beatStart + beatLength;

            int leftCount = 0;
            int rightCount = 0;

            foreach (var obj in windowObjects)
            {
                if (obj.StartTime < beatStart - TIME_TOLERANCE || obj.StartTime > beatEnd + TIME_TOLERANCE)
                    continue;

                if (obj.StartTime < half - TIME_TOLERANCE)
                    leftCount++;
                else
                    rightCount++;
            }

            return leftCount <= rightCount ? beatStart : half;
        }
    }
}
