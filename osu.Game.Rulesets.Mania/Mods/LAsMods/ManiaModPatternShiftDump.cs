// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_Level_Label), nameof(EzManiaModStrings.PatternShift_Level_Description))]
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
            double mainFraction = activeBeatFraction;
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;

            if (subStep <= 0)
                return;

            int availableSteps = (int)Math.Floor((windowEnd - (windowStart + subStep)) / subStep) + 1;
            if (availableSteps <= 0)
                return;

            applyDumpPattern(beatmap, windowObjects, windowStart, windowEnd, beatLength, rng);
        }

        private static void applyDumpPattern(ManiaBeatmap beatmap,
                                             List<ManiaHitObject> windowObjects,
                                             double windowStart,
                                             double windowEnd,
                                             double beatLength,
                                             Random rng)
        {
            // Dump=滑梯: 在窗口内按时间就近横向移动 note, 形成单调的列序列
            if (windowObjects.Count < 3)
                return;

            var groups = new List<(double time, ManiaHitObject obj)>();
            int i = 0;

            while (i < windowObjects.Count)
            {
                var obj = windowObjects[i];
                double time = obj.StartTime;
                int j = i + 1;
                int count = 1;

                while (j < windowObjects.Count && Math.Abs(windowObjects[j].StartTime - time) <= TIME_TOLERANCE)
                {
                    count++;
                    j++;
                }

                if (count == 1 && obj is Note)
                    groups.Add((time, obj));

                i = j;
            }

            if (groups.Count < 3)
                return;

            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            int firstCol = groups[0].obj.Column;
            int lastCol = groups[^1].obj.Column;

            int direction = lastCol == firstCol ? (rng.Next(0, 2) == 0 ? -1 : 1) : (lastCol > firstCol ? 1 : -1);
            int prevTarget = firstCol;

            for (int g = 1; g < groups.Count; g++)
            {
                var (time, cur) = groups[g];
                if (time < windowStart - TIME_TOLERANCE || time > windowEnd + TIME_TOLERANCE)
                    continue;

                int minCol = direction > 0 ? prevTarget + 1 : 0;
                int maxCol = direction > 0 ? totalColumns - 1 : prevTarget - 1;

                if (!tryPickNearestColumn(beatmap, cur, time, minCol, maxCol, out int chosen))
                {
                    // 允许保持不下降的单调（非严格）
                    minCol = direction > 0 ? prevTarget : 0;
                    maxCol = direction > 0 ? totalColumns - 1 : prevTarget;

                    if (!tryPickNearestColumn(beatmap, cur, time, minCol, maxCol, out chosen))
                        continue;
                }

                cur.Column = chosen;
                prevTarget = chosen;
            }

            // 大间隙单调滑梯：首尾间隔至少 1/2 拍才添加
            tryAddLargeGapSlide(beatmap, windowObjects, groups, beatLength, rng);
        }

        private static void tryAddLargeGapSlide(ManiaBeatmap beatmap,
                                                List<ManiaHitObject> windowObjects,
                                                List<(double time, ManiaHitObject obj)> groups,
                                                double beatLength,
                                                Random rng)
        {
            if (groups.Count < 2 || beatLength <= 0)
                return;

            double firstTime = groups[0].time;
            double lastTime = groups[^1].time;
            double gap = lastTime - firstTime;

            if (gap < beatLength / 2.0)
                return;

            if (ManiaKeyPatternHelp.HasDenseBurstBetweenQuarterNotes(windowObjects, beatLength, beatmap.TotalColumns))
                return;

            var firstCols = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, firstTime, TIME_TOLERANCE).ToList();
            var lastCols = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, lastTime, TIME_TOLERANCE).ToList();

            if (firstCols.Count == 0 || lastCols.Count == 0)
                return;

            firstCols.Sort();
            lastCols.Sort();

            bool useLeft = rng.Next(0, 2) == 0;
            int firstCol = useLeft ? firstCols.First() : firstCols.Last();
            int lastCol = useLeft ? lastCols.Last() : lastCols.First();

            if (firstCol == lastCol)
                return;

            int direction = firstCol < lastCol ? 1 : -1;
            var betweenCols = new List<int>();

            for (int c = firstCol + direction; c != lastCol; c += direction)
                betweenCols.Add(c);

            if (betweenCols.Count == 0)
                return;

            int minBetween = Math.Min(firstCol, lastCol) + 1;
            int maxBetween = Math.Max(firstCol, lastCol) - 1;
            if (ManiaKeyPatternHelp.HasObjectsInColumnRange(windowObjects, minBetween, maxBetween, firstTime, lastTime))
                return;

            int steps = betweenCols.Count;
            double stepDuration = gap / (steps + 1.0);

            for (int i = 0; i < steps; i++)
            {
                double time = firstTime + (stepDuration * (i + 1));
                var candidates = ManiaKeyPatternHelp.BuildNearestCandidates(betweenCols, i);
                ManiaKeyPatternHelp.TryAddNoteFromOrderedCandidates(beatmap, candidates, time);
            }
        }

        private static bool tryPickNearestColumn(ManiaBeatmap beatmap,
                                                 ManiaHitObject obj,
                                                 double time,
                                                 int minCol,
                                                 int maxCol,
                                                 out int chosen)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            int clampedMin = Math.Clamp(minCol, 0, totalColumns - 1);
            int clampedMax = Math.Clamp(maxCol, 0, totalColumns - 1);
            chosen = -1;

            if (clampedMin > clampedMax)
                return false;

            int bestDist = int.MaxValue;

            for (int col = clampedMin; col <= clampedMax; col++)
            {
                if (!isColumnAvailable(beatmap, obj, col, time))
                    continue;

                int dist = Math.Abs(col - obj.Column);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    chosen = col;
                }
            }

            return chosen >= 0;
        }

        private static bool isColumnAvailable(ManiaBeatmap beatmap, ManiaHitObject obj, int column, double time)
        {
            if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, column, time, obj, TIME_TOLERANCE))
                return false;

            if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, column, time, obj, TIME_TOLERANCE))
                return false;

            return true;
        }
    }
}
