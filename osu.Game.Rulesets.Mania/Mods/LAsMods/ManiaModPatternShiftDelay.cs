// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftDelay : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Delay;
        protected override string PatternName => "Delay";
        protected override string PatternAcronym => "PSD";

        protected override int DefaultLevel => 1;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 1;
        protected override int DefaultWindowProcessOffset => 1;
        protected override int DefaultApplyOrder => 50;

        [SettingSource("Level", "0=off. 1-3: offsets 1/16, 3/32, 1/8; shift up to level and keep at least level unshifted. 4-6: same offsets; keep at least 1 unshifted. 7-10: offsets 1/16, 1/12, 5/48, 1/8; shift up to level with no minimum unshifted.")]
        public new BindableNumber<int> Level => base.Level;

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
            double mainFraction = Math.Max(activeBeatFraction, 1.0 / 4.0);
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;

            if (subStep <= 0)
                return;

            int availableSteps = (int)Math.Floor((windowEnd - (windowStart + subStep)) / subStep) + 1;
            if (availableSteps <= 0)
                return;

            applyDelayPattern(windowObjects, beatmap, settings.Level, rng);
        }

        private static List<List<ManiaHitObject>> buildGroupsByStartTime(List<ManiaHitObject> objects)
        {
            var groups = new List<List<ManiaHitObject>>();

            if (objects.Count == 0)
                return groups;

            double currentTime = objects[0].StartTime;
            var currentGroup = new List<ManiaHitObject> { objects[0] };

            for (int i = 1; i < objects.Count; i++)
            {
                var obj = objects[i];

                if (obj.StartTime != currentTime)
                {
                    groups.Add(currentGroup);
                    currentTime = obj.StartTime;
                    currentGroup = new List<ManiaHitObject>();
                }

                currentGroup.Add(obj);
            }

            groups.Add(currentGroup);
            return groups;
        }

        private static void applyDelayPattern(List<ManiaHitObject> windowObjects,
                                              ManiaBeatmap beatmap,
                                              int level,
                                              Random rng)
        {
            var groups = buildGroupsByStartTime(windowObjects);

            foreach (var group in groups)
            {
                var list = group.ToList();
                int noteCount = list.Count;

                if (noteCount == 0)
                    continue;

                int count = ManiaKeyPatternHelp.GetDelayMaxShiftCount(level, noteCount);

                if (count == 0)
                    continue;

                double beatLength = beatmap.ControlPointInfo.TimingPointAt(list[0].StartTime).BeatLength;
                double offsetAmount = beatLength * ManiaKeyPatternHelp.GetDelayBeatFraction(level);

                var indexes = new List<int>(noteCount);
                for (int i = 0; i < noteCount; i++)
                    indexes.Add(i);

                ManiaKeyPatternHelp.Shuffle(indexes, rng);

                for (int i = 0; i < count; i++)
                {
                    var obj = list[indexes[i]];
                    double direction = rng.NextDouble() < 0.5 ? -1 : 1;
                    double offset = direction * offsetAmount;

                    if (ManiaKeyPatternHelp.TryApplyDelayOffset(beatmap, obj, offset, out bool holdConflict))
                        continue;

                    if (holdConflict)
                        ManiaKeyPatternHelp.TryApplyDelayOffset(beatmap, obj, -offset, out _);
                }
            }
        }
    }
}
