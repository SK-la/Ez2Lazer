// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public enum KeyPatternType
    {
        // 切
        Cut,

        // 楼梯
        Dump,

        // 叉
        Cross,

        // 叠
        Jack,

        // 拍
        Jump,

        // 高分偏移
        Delay
    }

    public class KeyPatternSettings
    {
        public int MinK { get; set; } = 1;
        public int MaxK { get; set; } = 5;
        public int Level { get; set; }
    }

    public static class ManiaKeyPatternHelp
    {
        private static readonly double[] even_beat_fractions =
        {
            1.0, 1.0 / 2, 1.0 / 4, 1.0 / 8, 1.0 / 12, 1.0 / 16, 1.0 / 24, 1.0 / 32
        };

        private static readonly double[] odd_beat_fractions =
        {
            1.0, 1.0 / 3, 1.0 / 6, 1.0 / 9, 1.0 / 12
        };

        public static void ProcessRollingWindow(ManiaBeatmap beatmap,
                            KeyPatternType patternType,
                            int windowQuarterBeats = 2,
                            int intervalQuarterBeats = 4,
                            bool incrementalMode = false,
                            KeyPatternSettings? settings = null,
                            int? seed = null,
                            int windowProcessInterval = 1,
                            int windowProcessOffset = 0)
        {
            var objects = beatmap.HitObjects.ToList();
            objects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            if (objects.Count == 0)
                return;

            double currentTime = getRedLineStart(beatmap, objects[0].StartTime);
            double endTime = getMaxEndTime(objects);

            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);
            int intervalQuarterBeatsSafe = Math.Max(1, intervalQuarterBeats);
            int startIndex = 0;
            int endIndex = 0;
            seed ??= RNG.Next();

            int windowIndex = 0;

            int processIntervalSafe = Math.Max(1, windowProcessInterval);
            int processOffsetSafe = Math.Clamp(windowProcessOffset, 0, processIntervalSafe - 1);
            var skipCache = new Dictionary<long, bool>();

            while (currentTime <= endTime)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(currentTime).BeatLength;
                double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;
                double stepDuration = windowDuration * (intervalQuarterBeatsSafe / 4.0);

                double windowEnd = currentTime + windowDuration;

                while (startIndex < objects.Count && objects[startIndex].StartTime < currentTime)
                    startIndex++;

                while (endIndex < objects.Count && objects[endIndex].StartTime < windowEnd)
                    endIndex++;

                int windowCount = endIndex - startIndex;

                long skipBeatIndex = beatLength > 0 ? (long)Math.Floor(currentTime / beatLength) : (long)Math.Floor(currentTime);
                if (!skipCache.TryGetValue(skipBeatIndex, out bool skip))
                {
                    skip = shouldSkipDenseWindow(patternType, objects, currentTime, beatLength, beatmap.TotalColumns);
                    skipCache[skipBeatIndex] = skip;
                }

                if (!skip && windowCount > 0 && windowIndex % processIntervalSafe == processOffsetSafe)
                {
                    var windowObjects = buildWindowObjects(objects, startIndex, endIndex);

                    if (incrementalMode)
                    {
                        int windowSeed = unchecked(seed.Value * 397) ^ (int)Math.Round(currentTime);
                        var rng = new Random(windowSeed);

                        if (!shouldSkipDenseWindow(patternType, objects, currentTime, beatLength, beatmap.TotalColumns))
                            applyIncrementalPattern(windowObjects, patternType, beatmap, currentTime, windowEnd, settings, rng, 1);
                    }
                    else
                        applyPattern(windowObjects, patternType, beatmap);
                }

                if (stepDuration <= 0)
                    break;

                windowIndex++;
                currentTime += stepDuration;
            }
        }

        public static void ProcessRollingWindowWithOscillator(ManiaBeatmap beatmap,
                                                              KeyPatternType patternType,
                                                              int level,
                                                              EzOscillator oscillator,
                                                              int seed,
                                                              int oscillationBeats,
                                                              int windowProcessInterval,
                                                              int windowProcessOffset,
                                                              int maxIterationsPerWindow,
                                                              int windowQuarterBeats = 2,
                                                              int intervalQuarterBeats = 4)
        {
            if (level <= 0)
                return;

            var objects = beatmap.HitObjects.ToList();
            objects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            if (objects.Count == 0)
                return;

            double currentTime = getRedLineStart(beatmap, objects[0].StartTime);
            double endTime = getMaxEndTime(objects);

            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);
            int intervalQuarterBeatsSafe = Math.Max(1, intervalQuarterBeats);
            int startIndex = 0;
            int endIndex = 0;

            int processIntervalSafe = Math.Max(1, windowProcessInterval);
            int processOffsetSafe = Math.Clamp(windowProcessOffset, 0, processIntervalSafe - 1);
            int windowIndex = 0;
            var skipCache = new Dictionary<long, bool>();

            while (currentTime <= endTime)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(currentTime).BeatLength;
                double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;
                double stepDuration = windowDuration * (intervalQuarterBeatsSafe / 4.0);

                double windowEnd = currentTime + windowDuration;

                while (startIndex < objects.Count && objects[startIndex].StartTime < currentTime)
                    startIndex++;

                while (endIndex < objects.Count && objects[endIndex].StartTime < windowEnd)
                    endIndex++;

                int windowCount = endIndex - startIndex;

                // 先按整拍缓存计算是否跳过该整拍（使同一整拍内的所有半拍窗口共享同一跳过决策）
                long skipBeatIndex = beatLength > 0 ? (long)Math.Floor(currentTime / beatLength) : (long)Math.Floor(currentTime);
                if (!skipCache.TryGetValue(skipBeatIndex, out bool skip))
                {
                    skip = shouldSkipDenseWindow(patternType, objects, currentTime, beatLength, beatmap.TotalColumns);
                    skipCache[skipBeatIndex] = skip;
                }

                if (!skip && windowCount > 0 && windowIndex % processIntervalSafe == processOffsetSafe)
                {
                    int oscillationBeatsSafe = Math.Max(1, oscillationBeats);
                    long beatIndex = beatLength > 0 ? (long)Math.Round(currentTime / beatLength) : (long)Math.Round(currentTime);
                    long oscillationIndex = beatIndex / oscillationBeatsSafe;
                    oscillator.Reset(unchecked(seed + oscillationIndex));
                    double oscValue = oscillator.NextSigned();
                    var settings = getPatternSettingsFromLevel(level, beatmap.TotalColumns, oscValue, patternType);

                    if (settings.MaxK > 0)
                    {
                        int windowSeed = unchecked(seed * 397) ^ (int)Math.Round(currentTime);
                        var rng = new Random(windowSeed);
                        var windowObjects = buildWindowObjects(objects, startIndex, endIndex);

                        if (!shouldSkipDenseWindow(patternType, objects, currentTime, beatLength, beatmap.TotalColumns))
                            applyIncrementalPattern(windowObjects, patternType, beatmap, currentTime, windowEnd, settings, rng, maxIterationsPerWindow);
                    }
                }

                if (stepDuration <= 0)
                    break;

                windowIndex++;
                currentTime += stepDuration;
            }
        }

        private static void applyPattern(List<ManiaHitObject> windowObjects, KeyPatternType patternType, ManiaBeatmap beatmap)
        {
            switch (patternType)
            {
                case KeyPatternType.Cut:
                    // TODO: 切
                    break;

                case KeyPatternType.Dump:
                    // TODO: 楼梯
                    break;

                case KeyPatternType.Cross:
                    // TODO: 叉
                    break;

                case KeyPatternType.Jack:
                    // TODO: 叠
                    break;

                case KeyPatternType.Jump:
                    // TODO: 拍
                    break;

                case KeyPatternType.Delay:
                    // TODO: 延迟
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(patternType), patternType, null);
            }
        }

        private static void applyIncrementalPattern(List<ManiaHitObject> windowObjects,
                                                    KeyPatternType patternType,
                                                    ManiaBeatmap beatmap,
                                                    double windowStart,
                                                    double windowEnd,
                                                    KeyPatternSettings? settings,
                                                    Random rng,
                                                    int maxIterationsPerWindow)
        {
            if (windowObjects.Count == 0)
                return;

            var patternSettings = settings ?? new KeyPatternSettings();
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(windowStart).BeatLength;

            if (beatLength <= 0)
                return;

            double activeBeatFraction = getActiveBeatFraction(windowObjects, beatmap, 1.0 / 4);

            double mainFraction = getPatternMainFraction(patternType, activeBeatFraction);
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;

            if (patternType == KeyPatternType.Cut)
            {
                if (!tryGetCutJackBaseFraction(activeBeatFraction, out double baseFraction))
                    return;

                mainFraction = baseFraction;
                mainStep = Math.Max(1, beatLength * mainFraction);
                subStep = Math.Max(1, beatLength * mainFraction);
            }

            if (subStep <= 0)
                return;

            const double time_tolerance = 0.5;

            int availableSteps = (int)Math.Floor((windowEnd - (windowStart + subStep)) / subStep) + 1;
            if (availableSteps <= 0)
                return;

            double t = windowStart + subStep;
            int minK = Math.Max(0, patternSettings.MinK);
            int maxK = Math.Max(minK, patternSettings.MaxK);

            double lineOffset = Math.Floor((t - windowStart) / mainStep) * mainStep;
            double prevLine = windowStart + lineOffset;
            double nextLine = prevLine + mainStep;

            var prevColumns = getColumnsAtTime(windowObjects, prevLine, time_tolerance);
            var nextColumns = getColumnsAtTime(windowObjects, nextLine, time_tolerance);

            switch (patternType)
            {
                case KeyPatternType.Cut:
                    applyCutPattern(beatmap, windowObjects, prevColumns, nextColumns, t, minK, maxK, rng);
                    break;

                case KeyPatternType.Cross:
                    applyCrossPattern(beatmap, prevColumns, nextColumns, t, minK, maxK, rng);
                    break;

                case KeyPatternType.Dump:
                    applyDumpPattern(beatmap, windowObjects, prevLine, nextLine, mainStep, subStep, t, time_tolerance, rng);
                    break;

                case KeyPatternType.Jack:
                    applyJackPattern(windowObjects, beatmap, windowStart, windowEnd, beatLength, patternSettings.Level, maxIterationsPerWindow, rng);
                    break;

                case KeyPatternType.Jump:
                    applyJumpPattern(beatmap, t, minK, maxK, rng);
                    break;

                case KeyPatternType.Delay:
                    applyDelayPattern(windowObjects, beatmap, patternSettings.Level, rng);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(patternType), patternType, null);
            }
        }

        private static bool shouldSkipDenseWindow(KeyPatternType patternType,
                                                  List<ManiaHitObject> objects,
                                                  double windowStart,
                                                  double beatLength,
                                                  int totalColumns)
        {
            if (patternType == KeyPatternType.Delay || patternType == KeyPatternType.Dump)
                return false;

            // 保持与之前行为一致：当无对象或节拍长度非法时跳过处理。
            if (objects.Count == 0 || beatLength <= 0)
                return true;

            // 为了让半拍（或其它步进）窗口属于同一整拍的跳过判断，
            // 将传入的 windowStart 锚定到其所属的整拍起点（向下取整到 beatLength 网格）。
            double anchoredStart = Math.Floor(windowStart / beatLength) * beatLength;
            if (anchoredStart < 0)
                anchoredStart = 0;

            double windowEnd = anchoredStart + beatLength;
            int startIndex = lowerBoundByTime(objects, anchoredStart);

            const double tolerance = 10.0; // ms
            double quarter = beatLength / 4.0;

            bool isOnDivisionLocal(double time, int divisor)
            {
                if (divisor <= 0)
                    return false;

                double interval = beatLength / divisor;
                if (interval <= 0)
                    return false;

                double mod = time % interval;
                return mod <= tolerance || Math.Abs(interval - mod) <= tolerance;
            }

            int countQuarter = 0;
            int countOther = 0;

            for (int i = startIndex; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (obj.StartTime >= windowEnd)
                    break;

                double time = obj.StartTime;

                // 忽略落在 1/1、1/2、1/3 线上的 note
                if (isOnDivisionLocal(time, 1) || isOnDivisionLocal(time, 2) || isOnDivisionLocal(time, 3))
                    continue;

                // 如果命中 1/4，则计为 quarter，否则计为 other（1/5,1/6,1/8...）
                bool onQuarter = false;
                if (quarter > 0)
                {
                    double modQ = time % quarter;
                    onQuarter = modQ <= tolerance || Math.Abs(quarter - modQ) <= tolerance;
                }

                if (onQuarter)
                    countQuarter++;
                else
                    countOther++;
            }

            // 没有剩余 note -> 跳过（不处理）
            if (countQuarter + countOther == 0)
                return true;

            int quarterThreshold = Math.Max(1, totalColumns / 4);
            // int mixedThreshold = Math.Max(1, totalColumns / 4);

            // 如果全部都是 1/4，则按 quarterThreshold 判定
            if (countOther == 0)
                return countQuarter >= quarterThreshold;

            // 存在更细分，则按 1/4 + 其他 的合计与 mixedThreshold 比较
            return countQuarter >= 1;
        }

        private static void applyCutPattern(ManiaBeatmap beatmap,
                                            List<ManiaHitObject> windowObjects,
                                            HashSet<int> prevColumns,
                                            HashSet<int> nextColumns,
                                            double time,
                                            int minK,
                                            int maxK,
                                            Random rng)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var avoid = new HashSet<int>(prevColumns);
            avoid.UnionWith(nextColumns);

            var available = new List<int>();

            for (int i = 0; i < totalColumns; i++)
            {
                if (!avoid.Contains(i))
                    available.Add(i);
            }

            if (available.Count == 0)
                return;

            const double tolerance = 10.0;
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;
            double quarter = beatLength / 4.0;

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= tolerance || Math.Abs(interval - mod) <= tolerance;
            }

            // 决定放置使用 1/2 还是 1/3 网格：优先选窗口内已有 note 所处的细分
            int preferredDivisor = 2; // 默认 1/2
            foreach (var obj in windowObjects)
            {
                if (isOnDivisionLocal(obj.StartTime, 2))
                {
                    preferredDivisor = 2;
                    break;
                }
                if (isOnDivisionLocal(obj.StartTime, 3))
                {
                    preferredDivisor = 3;
                    break;
                }
            }

            double targetInterval = beatLength / preferredDivisor;

            // 计算 base 对齐点并尝试多个偏移（优先 0, -1, +1, -2, +2）以寻找合适的 candidateTime
            double baseAligned = Math.Round(time / targetInterval) * targetInterval;
            double windowStart = windowObjects.Count > 0 ? windowObjects[0].StartTime : time;
            double windowEnd = windowObjects.Count > 0 ? windowObjects[^1].StartTime : time;

            double? chosenCandidate = null;
            int[] tries = { 0, -1, 1, -2, 2 };
            foreach (int off in tries)
            {
                double ct = baseAligned + off * targetInterval;
                // 保证在窗口范围内（加上小容差）
                if (ct < windowStart - tolerance || ct > windowEnd + tolerance)
                    continue;

                // 避免整拍位置
                bool onBeat = false;
                if (beatLength > 0)
                {
                    double modBeat = ct % beatLength;
                    onBeat = modBeat <= tolerance || Math.Abs(beatLength - modBeat) <= tolerance;
                }

                if (onBeat)
                    continue;

                // 预筛列：检查前后 quarter 是否为空
                var filteredTry = new List<int>();
                double prevTimeTry = ct - quarter;
                double nextTimeTry = ct + quarter;
                foreach (var col in available)
                {
                    if (hasNoteAtTime(beatmap, col, prevTimeTry, null, tolerance))
                        continue;
                    if (hasNoteAtTime(beatmap, col, nextTimeTry, null, tolerance))
                        continue;
                    if (isHoldOccupyingColumn(beatmap, col, ct, null, tolerance))
                        continue;
                    filteredTry.Add(col);
                }

                if (filteredTry.Count > 0)
                {
                    chosenCandidate = ct;
                    break;
                }
                // 否则记住第一个非整拍对齐点作为备选
                if (chosenCandidate == null && !onBeat)
                    chosenCandidate = ct;
            }

            double candidateTime = chosenCandidate ?? baseAligned;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, available.Count);

            if (avoid.Count == 0)
            {
                for (int i = 0; i < count; i++)
                    tryAddNoteFromCandidates(beatmap, available, null, rng, candidateTime);
                return;
            }

            double median = getMedianFromList(available);
            var weights = new List<double>(available.Count);

            for (int i = 0; i < available.Count; i++)
            {
                int column = available[i];
                weights.Add(Math.Max(0.001, Math.Abs(column - median)));
            }

            // 过滤出在 candidateTime 前后 quarter 无 note 的列
            var filtered = new List<int>();
            foreach (var col in available)
            {
                // 前后1/4时间点
                double prevTime = candidateTime - quarter;
                double nextTime = candidateTime + quarter;

                if (hasNoteAtTime(beatmap, col, prevTime, null, tolerance))
                    continue;

                if (hasNoteAtTime(beatmap, col, nextTime, null, tolerance))
                    continue;

                // 避免 hold 占用
                if (isHoldOccupyingColumn(beatmap, col, candidateTime, null, tolerance))
                    continue;

                filtered.Add(col);
            }

            var useCandidates = filtered.Count > 0 ? filtered : available;

            // 如果使用了 weights 且 useCandidates 是 available 的子集，需要为 useCandidates 构建对应的 weights 列表
            List<double>? weightsForUse = null;
            if (weights != null)
            {
                // weights 对应于 available 列的顺序
                if (useCandidates == available)
                    weightsForUse = weights;
                else
                {
                    weightsForUse = new List<double>(useCandidates.Count);
                    for (int i = 0; i < useCandidates.Count; i++)
                    {
                        int col = useCandidates[i];
                        int idx = available.IndexOf(col);
                        if (idx >= 0 && idx < weights.Count)
                            weightsForUse.Add(weights[idx]);
                        else
                            weightsForUse.Add(1.0);
                    }
                }
            }

            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, useCandidates, weightsForUse, rng, candidateTime);
        }

        private static void applyCrossPattern(ManiaBeatmap beatmap,
                                              HashSet<int> prevColumns,
                                              HashSet<int> nextColumns,
                                              double time,
                                              int minK,
                                              int maxK,
                                              Random rng)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var avoid = new HashSet<int>(prevColumns);
            avoid.UnionWith(nextColumns);

            var available = new List<int>();

            for (int i = 0; i < totalColumns; i++)
            {
                if (!avoid.Contains(i))
                    available.Add(i);
            }

            if (available.Count == 0)
                return;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, available.Count);

            if (avoid.Count == 0)
            {
                for (int i = 0; i < count; i++)
                    tryAddNoteFromCandidates(beatmap, available, null, rng, time);
                return;
            }

            double median = getMedian(avoid);
            double maxDistance = 1;

            for (int i = 0; i < available.Count; i++)
            {
                double dist = distanceToNearest(avoid, available[i]);
                if (dist > maxDistance)
                    maxDistance = dist;
            }

            var weights = new List<double>(available.Count);

            for (int i = 0; i < available.Count; i++)
            {
                int column = available[i];
                double dist = distanceToNearest(avoid, column);
                double medianBias = maxDistance - Math.Abs(column - median);
                weights.Add(Math.Max(0.001, dist * 2.0 + medianBias));
            }

            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, available, weights, rng, time);
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

            if (hasDenseBurstBetweenQuarterNotes(windowObjects, mainBeatLength, beatmap.TotalColumns))
                return;

            var prevCols = getColumnsAtTime(windowObjects, prevLine, tolerance).ToList();
            prevCols.Sort();
            var nextCols = getColumnsAtTime(windowObjects, nextLine, tolerance).ToList();
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
            if (hasObjectsInColumnRange(windowObjects, minBetween, maxBetween, prevLine, nextLine))
                return;

            int steps = betweenCols.Count;
            double stepDuration = mainGap / (steps + 1.0);

            for (int i = 0; i < steps; i++)
            {
                double time = prevLine + stepDuration * (i + 1);
                var candidates = buildNearestCandidates(betweenCols, i);
                tryAddNoteFromOrderedCandidates(beatmap, candidates, time);
            }
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
                tryAddNoteFromCandidates(beatmap, columns, null, rng, time);
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

            const double time_tolerance = 10.0;
            int maxIterations = Math.Max(1, maxIterationsPerWindow);
            var timePoints = buildJackTimePoints(windowStart, windowEnd, quarter, half, level, time_tolerance);

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
                applyJackWithFallbackAtTime(windowObjects, beatmap, t, quarter, level, rng, time_tolerance);
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
                {
                    obj.StartTime = times.start;
                }
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
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: false, preferBothSides: false, rng, timeTolerance);

                case 2:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: false, preferBothSides: true, rng, timeTolerance);

                case 3:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: false, preferBothSides: false, rng, timeTolerance);

                case 4:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: false, preferBothSides: true, rng, timeTolerance);

                case 5:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: true, preferBothSides: false, rng, timeTolerance);

                case 6:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: true, preferBothSides: true, rng, timeTolerance);

                case 7:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: true, preferBothSides: false, rng, timeTolerance);

                case 8:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: true, preferBothSides: true, rng, timeTolerance);

                case 9:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: true, preferBothSides: false, rng, timeTolerance);

                case 10:
                    return applyJackConvertedAtTime(windowObjects, beatmap, time, quarter, addNote: true, preferBothSides: true, rng, timeTolerance);

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
            var sourceNotes = getNotesAtTime(windowObjects, time, timeTolerance);

            if (sourceNotes.Count == 0)
                return false;

            var oneSide = new List<int>();
            var bothSides = new List<int>();

            for (int column = 0; column < totalColumns; column++)
            {
                bool hasPrev = time - quarter >= 0 && hasNoteAtTime(beatmap, column, time - quarter, null, timeTolerance);
                bool hasNext = hasNoteAtTime(beatmap, column, time + quarter, null, timeTolerance);

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

            shuffle(sourceNotes, rng);
            var available = new List<int>(candidates);
            shuffle(available, rng);
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
                        if (hasNoteAtTime(beatmap, targetColumn, time, null, timeTolerance))
                            continue;

                        if (isHoldOccupyingColumn(beatmap, targetColumn, time, null, timeTolerance))
                            continue;
                    }
                    else
                    {
                        if (hasNoteAtTime(beatmap, targetColumn, time, source, timeTolerance))
                            continue;

                        if (isHoldOccupyingColumn(beatmap, targetColumn, time, source, timeTolerance))
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

        internal static double GetDelayBeatFraction(int level)
        {
            double t;

            if (level <= 3)
                t = (level - 1) / 2.0;
            else if (level <= 6)
                t = (level - 4) / 2.0;
            else
                t = (level - 7) / 3.0;

            return 1.0 / 16.0 * (1 + t);
        }

        internal static int GetDelayMaxShiftCount(int level, int noteCount)
        {
            if (noteCount <= 0)
                return 0;

            if (level <= 3)
                return Math.Max(0, Math.Min(level, noteCount - level));

            if (level <= 6)
                return Math.Max(0, Math.Min(level, noteCount - 1));

            return Math.Min(level, noteCount);
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

                int count = GetDelayMaxShiftCount(level, noteCount);

                if (count == 0)
                    continue;

                double beatLength = beatmap.ControlPointInfo.TimingPointAt(list[0].StartTime).BeatLength;
                double offsetAmount = beatLength * GetDelayBeatFraction(level);

                var indexes = new List<int>(noteCount);
                for (int i = 0; i < noteCount; i++)
                    indexes.Add(i);

                shuffle(indexes, rng);

                for (int i = 0; i < count; i++)
                {
                    var obj = list[indexes[i]];
                    double direction = rng.NextDouble() < 0.5 ? -1 : 1;
                    double offset = direction * offsetAmount;

                    if (TryApplyDelayOffset(beatmap, obj, offset, out bool holdConflict))
                        continue;

                    if (holdConflict)
                        TryApplyDelayOffset(beatmap, obj, -offset, out _);
                }
            }
        }

        private static double getRedLineStart(ManiaBeatmap beatmap, double fallbackTime)
        {
            var timingPoints = beatmap.ControlPointInfo.TimingPoints;
            if (timingPoints.Count > 0)
                return timingPoints[0].Time;

            return fallbackTime;
        }

        private static double getMaxEndTime(List<ManiaHitObject> objects)
        {
            double maxEnd = objects[0].StartTime;

            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                double end = obj is HoldNote hold ? hold.EndTime : obj.StartTime;
                if (end > maxEnd)
                    maxEnd = end;
            }

            return maxEnd;
        }

        private static bool hasDenseBurstBetweenQuarterNotes(List<ManiaHitObject> windowObjects,
                                                             double beatLength,
                                                             int totalColumns)
        {
            double anchorInterval = beatLength;
            if (anchorInterval <= 0)
                return false;

            const double tolerance = 10.0;
            double quarter = beatLength / 4.0;

            if (quarter <= 0)
                return false;

            bool isOnAnchor(double time)
            {
                double mod = time % anchorInterval;
                return mod <= tolerance || Math.Abs(anchorInterval - mod) <= tolerance;
            }

            bool isOnQuarter(double time)
            {
                double mod = time % quarter;
                return mod <= tolerance || Math.Abs(quarter - mod) <= tolerance;
            }

            bool isOnDivision(double time, int divisor)
            {
                if (divisor <= 0)
                    return false;

                double interval = beatLength / divisor;
                if (interval <= 0)
                    return false;

                double mod = time % interval;
                return mod <= tolerance || Math.Abs(interval - mod) <= tolerance;
            }

            const int max_fine_divisor = 64;

            for (int start = 0; start + 1 < windowObjects.Count; start++)
            {
                double anchorTime = windowObjects[start].StartTime;

                if (!isOnAnchor(anchorTime))
                    continue;

                for (int end = start + 1; end < windowObjects.Count; end++)
                {
                    double span = windowObjects[end].StartTime - anchorTime;
                    if (span < anchorInterval)
                        continue;

                    if (!isOnAnchor(windowObjects[end].StartTime))
                        continue;

                    int countQuarter = 0;
                    int countOther = 0;
                    int mixedThreshold = Math.Max(1, totalColumns / 4);

                    for (int i = start + 1; i < end; i++)
                    {
                        double time = windowObjects[i].StartTime;

                        // 忽略落在 1/1、1/2、1/3 线上的 note
                        if (isOnDivision(time, 1) || isOnDivision(time, 2) || isOnDivision(time, 3))
                            continue;

                        // 落在 1/4 线（只计为 quarter）
                        if (isOnQuarter(time))
                        {
                            countQuarter++;
                        }
                        else
                        {
                            countOther++;
                        }

                        if (countQuarter + countOther >= mixedThreshold)
                            return true;
                    }

                    break;
                }
            }

            return false;
        }

        private static int lowerBoundByTime(List<ManiaHitObject> objects, double time)
        {
            int left = 0;
            int right = objects.Count;

            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (objects[mid].StartTime < time)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static bool hasObjectsInColumnRange(List<ManiaHitObject> objects,
                                                    int minColumn,
                                                    int maxColumn,
                                                    double startTime,
                                                    double endTime)
        {
            if (minColumn > maxColumn)
                return false;

            double minTime = Math.Min(startTime, endTime);
            double maxTime = Math.Max(startTime, endTime);
            int startIndex = lowerBoundByTime(objects, minTime);

            for (int i = startIndex; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (obj.StartTime > maxTime)
                    break;

                if (obj.Column >= minColumn && obj.Column <= maxColumn)
                    return true;
            }

            return false;
        }

        private static List<ManiaHitObject> buildWindowObjects(List<ManiaHitObject> objects, int startIndex, int endIndex)
        {
            int count = Math.Max(0, endIndex - startIndex);
            var windowObjects = new List<ManiaHitObject>(count);

            for (int i = startIndex; i < endIndex; i++)
                windowObjects.Add(objects[i]);

            return windowObjects;
        }

        private static HashSet<int> getColumnsAtTime(List<ManiaHitObject> objects, double time, double tolerance)
        {
            var cols = new HashSet<int>();

            if (objects.Count == 0)
                return cols;

            double minTime = time - tolerance;
            double maxTime = time + tolerance;
            int startIndex = lowerBoundByTime(objects, minTime);

            for (int i = startIndex; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (obj.StartTime > maxTime)
                    break;

                if (obj.StartTime >= minTime)
                    cols.Add(obj.Column);
            }

            return cols;
        }

        private static List<Note> getNotesAtTime(List<ManiaHitObject> objects, double time, double tolerance)
        {
            var notes = new List<Note>();

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] is Note note && Math.Abs(note.StartTime - time) <= tolerance)
                    notes.Add(note);
            }

            return notes;
        }

        private static void applyAvoidColumnsIncremental(ManiaBeatmap beatmap,
                                                         HashSet<int> prevColumns,
                                                         HashSet<int> nextColumns,
                                                         double time,
                                                         int minK,
                                                         int maxK,
                                                         Random rng)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var avoid = new HashSet<int>(prevColumns);
            avoid.UnionWith(nextColumns);

            var availableColumns = new List<int>();

            for (int i = 0; i < totalColumns; i++)
            {
                if (!avoid.Contains(i))
                    availableColumns.Add(i);
            }

            if (availableColumns.Count == 0)
                return;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, availableColumns.Count);

            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, availableColumns, null, rng, time);
        }

        private static KeyPatternSettings getPatternSettingsFromLevel(int level, int totalColumns, double oscValue, KeyPatternType patternType)
        {
            if (level <= 0 || totalColumns <= 0)
                return new KeyPatternSettings { MinK = 0, MaxK = 0, Level = level };

            var range = getPatternRangeFromLevel(patternType, level, totalColumns);
            if (range.max <= 0)
                return new KeyPatternSettings { MinK = 0, MaxK = 0, Level = level };

            double normalized = (oscValue + 1.0) * 0.5;
            int count = range.min + (int)Math.Round(normalized * (range.max - range.min));
            count = Math.Clamp(count, range.min, range.max);

            int minCount = Math.Max(range.min, count - 1);
            int maxCount = Math.Min(range.max, count + 1);

            return new KeyPatternSettings { MinK = minCount, MaxK = maxCount, Level = level };
        }

        private static (int min, int max) getPatternRangeFromLevel(KeyPatternType patternType, int level, int totalColumns)
        {
            int maxCount;
            int minCount;

            switch (patternType)
            {
                case KeyPatternType.Jack:
                    maxCount = Math.Clamp((int)Math.Ceiling(level * 0.6), 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 1);
                    break;

                case KeyPatternType.Jump:
                    maxCount = Math.Clamp(level, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 2);
                    break;

                case KeyPatternType.Dump:
                    maxCount = Math.Clamp(level + 1, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 2);
                    break;

                case KeyPatternType.Cut:
                case KeyPatternType.Cross:
                    maxCount = Math.Clamp(level, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 1);
                    break;

                case KeyPatternType.Delay:
                    maxCount = Math.Clamp(level, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 1);
                    break;

                default:
                    maxCount = Math.Clamp(level, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 1);
                    break;
            }

            return (minCount, maxCount);
        }

        private static double getActiveBeatFraction(List<ManiaHitObject> windowObjects, ManiaBeatmap beatmap, double defaultFraction)
        {
            if (windowObjects.Count < 2)
                return defaultFraction;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(windowObjects[0].StartTime).BeatLength;
            if (beatLength <= 0)
                return defaultFraction;

            var counts = new Dictionary<double, int>();

            for (int i = 1; i < windowObjects.Count; i++)
            {
                double gap = windowObjects[i].StartTime - windowObjects[i - 1].StartTime;
                if (gap <= 0)
                    continue;

                double gapFraction = gap / beatLength;
                double evenNearest = getNearestFraction(gapFraction, even_beat_fractions);
                double oddNearest = getNearestFraction(gapFraction, odd_beat_fractions);
                double snapped = Math.Abs(gapFraction - evenNearest) <= Math.Abs(gapFraction - oddNearest)
                    ? evenNearest
                    : oddNearest;

                if (counts.TryGetValue(snapped, out int count))
                    counts[snapped] = count + 1;
                else
                    counts[snapped] = 1;
            }

            if (counts.Count == 0)
                return defaultFraction;

            double bestFraction = defaultFraction;
            int bestCount = -1;

            foreach (var pair in counts)
            {
                if (pair.Value > bestCount || pair.Value == bestCount && pair.Key > bestFraction)
                {
                    bestCount = pair.Value;
                    bestFraction = pair.Key;
                }
            }

            return bestFraction;
        }

        private static double getNearestFraction(double value, IReadOnlyList<double> candidates)
        {
            double best = candidates[0];
            double bestDiff = Math.Abs(value - best);

            for (int i = 1; i < candidates.Count; i++)
            {
                double diff = Math.Abs(value - candidates[i]);

                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = candidates[i];
                }
            }

            return best;
        }

        private static double getPatternMainFraction(KeyPatternType patternType, double active)
        {
            switch (patternType)
            {
                case KeyPatternType.Cut:
                case KeyPatternType.Cross:
                    return Math.Max(active, 1.0 / 8);

                case KeyPatternType.Jack:
                    return Math.Max(active, 1.0 / 4);

                case KeyPatternType.Jump:
                    return Math.Max(active, 1.0 / 2);

                case KeyPatternType.Delay:
                    return Math.Max(active, 1.0 / 4);

                case KeyPatternType.Dump:
                    return active;

                default:
                    return active;
            }
        }

        private static bool tryGetCutJackBaseFraction(double activeFraction, out double baseFraction)
        {
            const double eps = 1e-6;

            if (Math.Abs(activeFraction - 1.0 / 3.0) < eps)
            {
                baseFraction = 1.0 / 3.0;
                return true;
            }

            if (Math.Abs(activeFraction - 1.0) < eps
                || Math.Abs(activeFraction - 1.0 / 2.0) < eps
                || Math.Abs(activeFraction - 1.0 / 4.0) < eps)
            {
                baseFraction = 1.0 / 4.0;
                return true;
            }

            baseFraction = 0;
            return false;
        }

        private static List<int> buildNearestCandidates(IReadOnlyList<int> columns, int index)
        {
            int count = columns.Count;
            var ordered = new List<int>(count);

            for (int offset = 0; offset < count; offset++)
            {
                int left = index - offset;
                if (left >= 0)
                    ordered.Add(columns[left]);

                int right = index + offset;
                if (right < count && right != left)
                    ordered.Add(columns[right]);
            }

            return ordered;
        }

        private static bool tryAddNoteFromOrderedCandidates(ManiaBeatmap beatmap, IReadOnlyList<int> candidates, double time)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                int column = candidates[i];

                if (isHoldOccupyingColumn(beatmap, column, time))
                    continue;

                if (hasNoteAtTime(beatmap, column, time))
                    return false;

                beatmap.HitObjects.Add(new Note { Column = column, StartTime = time });
                return true;
            }

            return false;
        }

        private static bool tryAddNoteFromCandidates(ManiaBeatmap beatmap,
                                                     List<int> candidates,
                                                     List<double>? weights,
                                                     Random rng,
                                                     double time)
        {
            int pickedIndex = pickIndexAvoidingHolds(beatmap, candidates, weights, rng, time);
            if (pickedIndex < 0)
                return false;

            int column = candidates[pickedIndex];
            candidates.RemoveAt(pickedIndex);
            weights?.RemoveAt(pickedIndex);

            if (hasNoteAtTime(beatmap, column, time))
                return false;

            beatmap.HitObjects.Add(new Note { Column = column, StartTime = time });
            return true;
        }

        private static int pickIndexAvoidingHolds(ManiaBeatmap beatmap,
                                                  List<int> candidates,
                                                  List<double>? weights,
                                                  Random rng,
                                                  double time)
        {
            while (candidates.Count > 0)
            {
                int picked = weights != null
                    ? pickWeightedIndex(weights, rng)
                    : rng.Next(candidates.Count);

                int column = candidates[picked];
                if (!isHoldOccupyingColumn(beatmap, column, time))
                    return picked;

                candidates.RemoveAt(picked);
                weights?.RemoveAt(picked);
            }

            return -1;
        }

        // Placement helpers (shared rule: hold occupied -> reselect, note exists -> skip).

        private static bool hasNoteAtTime(ManiaBeatmap beatmap, int column, double time, ManiaHitObject? ignore = null, double tolerance = 0.5)
        {
            foreach (var obj in beatmap.HitObjects)
            {
                if (ReferenceEquals(obj, ignore))
                    continue;

                if (obj.Column != column)
                    continue;

                if (obj is Note && Math.Abs(obj.StartTime - time) <= tolerance)
                    return true;
            }

            return false;
        }

        private static bool isHoldOverlappingColumn(ManiaBeatmap beatmap, int column, double startTime, double endTime, ManiaHitObject? ignore = null, double tolerance = 0.5)
        {
            foreach (var obj in beatmap.HitObjects)
            {
                if (ReferenceEquals(obj, ignore))
                    continue;

                if (obj.Column != column)
                    continue;

                if (obj is HoldNote hold)
                {
                    double holdStart = hold.StartTime - tolerance;
                    double holdEnd = hold.EndTime + tolerance;
                    if (holdStart <= endTime && holdEnd >= startTime)
                        return true;
                }
            }

            return false;
        }

        internal static bool TryApplyDelayOffset(ManiaBeatmap beatmap, ManiaHitObject obj, double offset, out bool holdConflict)
        {
            holdConflict = false;

            if (obj is HoldNote hold)
            {
                double duration = hold.EndTime - hold.StartTime;
                double newStart = Math.Max(0, hold.StartTime + offset);
                double newEnd = Math.Max(newStart, newStart + duration);

                if (hasNoteAtTime(beatmap, hold.Column, newStart, hold))
                    return false;

                if (isHoldOverlappingColumn(beatmap, hold.Column, newStart, newEnd, hold))
                {
                    holdConflict = true;
                    return false;
                }

                hold.StartTime = newStart;
                hold.EndTime = newEnd;
                return true;
            }

            double targetTime = Math.Max(0, obj.StartTime + offset);

            if (hasNoteAtTime(beatmap, obj.Column, targetTime, obj))
                return false;

            if (isHoldOccupyingColumn(beatmap, obj.Column, targetTime, obj))
            {
                holdConflict = true;
                return false;
            }

            obj.StartTime = targetTime;
            return true;
        }

        private static bool isHoldOccupyingColumn(ManiaBeatmap beatmap, int column, double time, ManiaHitObject? ignore = null, double tolerance = 0.5)
        {
            foreach (var obj in beatmap.HitObjects)
            {
                if (ReferenceEquals(obj, ignore))
                    continue;

                if (obj.Column != column)
                    continue;

                if (obj is HoldNote hold && time >= hold.StartTime - tolerance && time <= hold.EndTime + tolerance)
                    return true;
            }

            return false;
        }

        private static void shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static double distanceToNearest(HashSet<int> occupied, int col)
        {
            double best = double.MaxValue;
            foreach (int o in occupied)
                best = Math.Min(best, Math.Abs(col - o));
            return best == double.MaxValue ? 0 : best;
        }

        private static double getMedian(HashSet<int> values)
        {
            var ordered = values.OrderBy(v => v).ToList();
            int count = ordered.Count;

            if (count == 0)
                return 0;

            if (count % 2 == 1)
                return ordered[count / 2];

            return (ordered[count / 2 - 1] + ordered[count / 2]) / 2.0;
        }

        private static double getMedianFromList(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
                return 0;

            var ordered = values.OrderBy(v => v).ToList();
            int count = ordered.Count;

            if (count % 2 == 1)
                return ordered[count / 2];

            return (ordered[count / 2 - 1] + ordered[count / 2]) / 2.0;
        }

        private static int pickWeightedIndex(IReadOnlyList<double> weights, Random rng)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                total += weights[i];

            if (total <= 0)
                return rng.Next(weights.Count);

            double roll = rng.NextDouble() * total;
            double acc = 0;

            for (int i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc)
                    return i;
            }

            return weights.Count - 1;
        }
    }
}
