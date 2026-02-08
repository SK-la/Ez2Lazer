// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftDump : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Dump;
        protected override string PatternName => "Dump";
        protected override string PatternAcronym => "PSS";

        protected override int DefaultLevel => 3;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 1;
        protected override int DefaultWindowProcessOffset => 1;
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
            double mainFraction = activeBeatFraction;
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;

            if (subStep <= 0)
                return;

            int availableSteps = (int)Math.Floor((windowEnd - (windowStart + subStep)) / subStep) + 1;
            if (availableSteps <= 0)
                return;

            double t = windowStart + subStep;
            double lineOffset = Math.Floor((t - windowStart) / mainStep) * mainStep;
            double prevLine = windowStart + lineOffset;
            double nextLine = prevLine + mainStep;

            applyDumpPattern(beatmap, windowObjects, prevLine, nextLine, mainStep, subStep, t, TIME_TOLERANCE, rng);
        }

        private static void applyDumpPattern(ManiaBeatmap beatmap,
                                             List<ManiaHitObject> windowObjects,
                                             double prevLine,
                                             double nextLine,
                                             double mainStep,
                                             double subStep,
                                             double currentTime,
                                             double tolerance,
                                             Random rng)
        {
            double mainGap = nextLine - prevLine;
            if (mainGap < mainStep)
                return;

            double mainBeatLength = beatmap.ControlPointInfo.TimingPointAt(prevLine).BeatLength;
            if (mainBeatLength <= 0)
                return;

            if (mainGap / mainBeatLength < 0.25)
                return;

            if (ManiaKeyPatternHelp.HasDenseBurstBetweenQuarterNotes(windowObjects, mainBeatLength, beatmap.TotalColumns))
                return;

            var prevCols = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, prevLine, tolerance).ToList();
            prevCols.Sort();
            var nextCols = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, nextLine, tolerance).ToList();
            nextCols.Sort();

            if (prevCols.Count == 0 || nextCols.Count == 0)
                return;

            bool useLeft = rng.Next(0, 2) == 0;
            int prevCol = useLeft ? prevCols.First() : prevCols.Last();
            int nextCol = useLeft ? nextCols.Last() : nextCols.First();

            if (prevCol == nextCol)
                return;

            int direction = prevCol < nextCol ? 1 : -1;
            var betweenCols = new List<int>();

            for (int c = prevCol + direction; c != nextCol; c += direction)
                betweenCols.Add(c);

            if (betweenCols.Count == 0)
                return;

            int minBetween = Math.Min(prevCol, nextCol) + 1;
            int maxBetween = Math.Max(prevCol, nextCol) - 1;
            if (ManiaKeyPatternHelp.HasObjectsInColumnRange(windowObjects, minBetween, maxBetween, prevLine, nextLine))
                return;

            int steps = betweenCols.Count;
            double stepDuration = mainGap / (steps + 1.0);

            for (int i = 0; i < steps; i++)
            {
                double time = prevLine + (stepDuration * (i + 1));
                var candidates = ManiaKeyPatternHelp.BuildNearestCandidates(betweenCols, i);
                ManiaKeyPatternHelp.TryAddNoteFromOrderedCandidates(beatmap, candidates, time);
            }
        }
    }
}
