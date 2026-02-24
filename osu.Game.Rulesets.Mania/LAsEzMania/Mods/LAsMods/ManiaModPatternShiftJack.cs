// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Localization;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftJack : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Jack;
        protected override string PatternName => "Jack";
        protected override string PatternAcronym => "PSJ";
        public override LocalisableString Description => EzManiaModStrings.PatternShift_Description_Jack;
        protected override int DefaultLevel => 5;
        protected override EzOscillator.EzWaveform DefaultWaveform => EzOscillator.EzWaveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 2;
        protected override int DefaultWindowProcessOffset => 0;
        protected override int DefaultApplyOrder => 50;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_Level_Label), nameof(EzManiaModStrings.PatternShift_JackLevel_Description))]
        public new BindableNumber<int> Level => base.Level;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_WindowMaxIterations_Label), nameof(EzManiaModStrings.PatternShift_WindowMaxIterations_Description))]
        public BindableNumber<int> WindowMaxIterations { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 4,
            Precision = 1
        };

        protected override int MaxIterationsPerWindow => Math.Clamp(WindowMaxIterations.Value, 1, 4);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                foreach (var item in base.SettingDescription)
                    yield return item;

                yield return ("Window Max Iterations", $"{WindowMaxIterations.Value}");
            }
        }

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

            applyJackPattern(windowObjects, beatmap, windowStart, windowEnd, beatLength, settings.Level, maxIterationsPerWindow, rng);
        }

        private static void applyJackPattern(List<ManiaHitObject> windowObjects,
                                             ManiaBeatmap beatmap,
                                             double windowStart,
                                             double windowEnd,
                                             double beatLength,
                                             int level,
                                             int maxIterationsPerWindow,
                                             Random rng)
        {
            // Jack level map:
            // 1: 1/2 sources, move, one-side columns.
            // 2: 1/2 sources, move, both-sides only (fallback to lower levels on fail).
            // 3: 1/4 sources, move, one-side columns.
            // 4: 1/4 sources, move, both-sides only (fallback).
            // 5: 1/2 sources, add, one-side columns.
            // 6: 1/2 sources, add, both-sides only (fallback).
            // 7: 1/4 sources, add, one-side columns.
            // 8: 1/4 sources, add, both-sides only (fallback).
            // 9: level 5 + level 7.
            // 10: level 6 + level 8.
            if (level <= 0 || beatLength <= 0)
                return;

            double quarter = beatLength / 4.0;
            double half = beatLength / 2.0;

            if (quarter <= 0 || half <= 0)
                return;

            int maxIterations = Math.Max(1, maxIterationsPerWindow);
            var timePoints = buildJackTimePoints(windowStart, windowEnd, quarter, half, level, TIME_TOLERANCE);

            if (timePoints.Count == 0)
                return;

            int windowCap = rng.Next(1, 5);
            maxIterations = Math.Min(maxIterations, windowCap);

            // 快照原始时间，确保 Jack 处理不会修改已有 note 的 StartTime/EndTime。
            var originalTimes = new Dictionary<ManiaHitObject, (double start, double end)>(windowObjects.Count);

            foreach (var obj in windowObjects)
            {
                double s = obj.StartTime;
                double e = obj is HoldNote h ? h.EndTime : obj.StartTime;
                originalTimes[obj] = (s, e);
            }

            int processed = 0;

            for (int i = 0; i < timePoints.Count; i++)
            {
                if (processed >= maxIterations)
                    break;

                double t = timePoints[i];
                if (applyJackWithFallbackAtTime(windowObjects, beatmap, t, quarter, level, rng, TIME_TOLERANCE))
                    processed++;
            }

            // 恢复原始时间（新增的 note 不在 originalTimes 中，因此不会影响新添加的 note）。
            foreach (var kv in originalTimes)
            {
                var obj = kv.Key;
                var times = kv.Value;

                if (obj is HoldNote hold)
                {
                    hold.StartTime = times.start;
                    hold.EndTime = times.end;
                }
                else
                    obj.StartTime = times.start;
            }
        }

        private static List<double> buildJackTimePoints(double windowStart,
                                                        double windowEnd,
                                                        double quarter,
                                                        double half,
                                                        int level,
                                                        double timeTolerance)
        {
            var points = new List<double>();

            void addTimes(double interval)
            {
                if (interval <= 0)
                    return;

                for (double t = windowStart + interval; t <= windowEnd + timeTolerance; t += interval)
                    points.Add(t);
            }

            bool wantsHalf = level == 1 || level == 2 || level == 5 || level == 6 || level == 9 || level == 10;
            bool wantsQuarter = level == 3 || level == 4 || level == 7 || level == 8 || level == 9 || level == 10;

            if (wantsHalf)
                addTimes(half);

            if (wantsQuarter)
                addTimes(quarter);

            points.Sort();

            if (points.Count <= 1)
                return points;

            var unique = new List<double>(points.Count) { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                if (Math.Abs(points[i] - unique[^1]) > timeTolerance)
                    unique.Add(points[i]);
            }

            return unique;
        }

        private static bool applyJackWithFallbackAtTime(List<ManiaHitObject> windowObjects,
                                                        ManiaBeatmap beatmap,
                                                        double time,
                                                        double quarter,
                                                        int level,
                                                        Random rng,
                                                        double timeTolerance)
        {
            for (int current = level; current >= 1; current--)
            {
                if (applyJackLevelAtTime(windowObjects, beatmap, time, quarter, current, rng, timeTolerance))
                    return true;
            }

            return false;
        }

        private static bool applyJackLevelAtTime(List<ManiaHitObject> windowObjects,
                                                 ManiaBeatmap beatmap,
                                                 double time,
                                                 double quarter,
                                                 int level,
                                                 Random rng,
                                                 double timeTolerance)
        {
            switch (level)
            {
                case 1:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, false, false, rng, timeTolerance);

                case 2:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, false, true, rng, timeTolerance);

                case 3:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, false, false, rng, timeTolerance);

                case 4:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, false, true, rng, timeTolerance);

                case 5:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, true, false, rng, timeTolerance);

                case 6:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, true, true, rng, timeTolerance);

                case 7:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, true, false, rng, timeTolerance);

                case 8:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, true, true, rng, timeTolerance);

                case 9:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, true, false, rng, timeTolerance);

                case 10:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, true, true, rng, timeTolerance);

                default:
                    return false;
            }
        }

        private static bool applyJackConvertedAtTime(List<ManiaHitObject> windowObjects,
                                                     ManiaBeatmap beatmap,
                                                     double time,
                                                     double quarter,
                                                     bool addNote,
                                                     bool preferBothSides,
                                                     Random rng,
                                                     double timeTolerance)
        {
            // 不允许在整拍（1/1）上添加 note。
            if (addNote)
            {
                double beatLength = quarter * 4.0;

                if (beatLength > 0)
                {
                    double mod = time % beatLength;
                    if (mod <= timeTolerance || Math.Abs(beatLength - mod) <= timeTolerance)
                        return false;
                }
            }

            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var sourceNotes = ManiaKeyPatternHelp.GetNotesAtTime(windowObjects, time, timeTolerance);

            if (sourceNotes.Count == 0)
                return false;

            var oneSide = new List<int>();
            var bothSides = new List<int>();

            for (int column = 0; column < totalColumns; column++)
            {
                bool hasPrev = time - quarter >= 0 && ManiaKeyPatternHelp.HasNoteAtTime(beatmap, column, time - quarter, null, timeTolerance);
                bool hasNext = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, column, time + quarter, null, timeTolerance);

                if (hasPrev && hasNext)
                    bothSides.Add(column);
                else if (hasPrev || hasNext)
                    oneSide.Add(column);
            }

            List<int> candidates;

            if (preferBothSides)
            {
                if (bothSides.Count == 0)
                    return false;

                candidates = bothSides;
            }
            else
            {
                candidates = new List<int>(oneSide.Count + bothSides.Count);
                candidates.AddRange(oneSide);
                candidates.AddRange(bothSides);
            }

            if (candidates.Count == 0)
                return false;

            ManiaKeyPatternHelp.Shuffle(sourceNotes, rng);
            var available = new List<int>(candidates);
            ManiaKeyPatternHelp.Shuffle(available, rng);
            bool applied = false;

            for (int i = 0; i < sourceNotes.Count && available.Count > 0; i++)
            {
                var source = sourceNotes[i];
                int targetIndex = -1;

                for (int c = 0; c < available.Count; c++)
                {
                    int targetColumn = available[c];

                    if (!addNote && targetColumn == source.Column)
                        continue;

                    if (addNote)
                    {
                        if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, targetColumn, time, null, timeTolerance))
                            continue;

                        if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, targetColumn, time, null, timeTolerance))
                            continue;
                    }
                    else
                    {
                        if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, targetColumn, time, source, timeTolerance))
                            continue;

                        if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, targetColumn, time, source, timeTolerance))
                            continue;
                    }

                    targetIndex = c;
                    break;
                }

                if (targetIndex < 0)
                    continue;

                int target = available[targetIndex];
                available.RemoveAt(targetIndex);

                if (addNote)
                    beatmap.HitObjects.Add(new Note { Column = target, StartTime = time });
                else
                    source.Column = target;

                applied = true;
            }

            return applied;
        }
    }
}
