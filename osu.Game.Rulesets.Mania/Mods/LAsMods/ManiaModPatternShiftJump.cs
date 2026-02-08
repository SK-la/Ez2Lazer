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

            applyJumpPattern(beatmap, t, minK, maxK, rng);
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
    }
}
