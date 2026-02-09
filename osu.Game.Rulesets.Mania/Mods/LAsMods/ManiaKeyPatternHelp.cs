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

        // 叉
        Cross,

        // 高分偏移
        Delay,

        // 楼梯
        Dump,

        // 叠
        Jack,

        // 拍
        Jump,
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

        // Pools to reduce per-window allocations
        private static readonly Stack<List<ManiaHitObject>> windowObjectsPool = new Stack<List<ManiaHitObject>>(16);

        public static void ProcessRollingWindow(ManiaBeatmap beatmap,
                                                KeyPatternType patternType,
                                                int windowQuarterBeats = 2,
                                                int intervalQuarterBeats = 4,
                                                bool incrementalMode = false,
                                                KeyPatternSettings? settings = null,
                                                int? seed = null,
                                                int windowProcessInterval = 1,
                                                int windowProcessOffset = 0,
                                                Action<List<ManiaHitObject>, ManiaBeatmap, double, double, KeyPatternSettings, Random, int>? applyPattern = null)
        {
            var objects = beatmap.HitObjects.ToList();
            objects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            if (objects.Count == 0)
                return;

            if (applyPattern == null)
                return;

            double currentTime = getRedLineStart(beatmap, objects[0].StartTime);
            double endTime = getMaxEndTime(objects);

            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);
            int intervalQuarterBeatsSafe = Math.Max(1, intervalQuarterBeats);
            int startIndex = 0;
            int endIndex = 0;
            seed ??= RNG.Next();

            int windowIndex = 0;

            int processIntervalSafe = Math.Clamp(windowProcessInterval, 1, 4);
            int processOffsetSafe = Math.Clamp(windowProcessOffset - 1, 0, processIntervalSafe - 1);
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

                long skipWindowIndex = windowDuration > 0 ? (long)Math.Floor(currentTime / windowDuration) : (long)Math.Floor(currentTime);

                if (!skipCache.TryGetValue(skipWindowIndex, out bool skip))
                {
                    skip = shouldSkipDenseWindow(patternType, objects, currentTime, windowDuration, beatLength, beatmap.TotalColumns);
                    skipCache[skipWindowIndex] = skip;
                }

                if (!skip && windowCount > 0 && (skipWindowIndex % processIntervalSafe) == processOffsetSafe)
                {
                    var windowObjects = buildWindowObjects(objects, startIndex, endIndex);
                    try
                    {
                        int windowSeed = unchecked(seed.Value * 397) ^ (int)Math.Round(currentTime);
                        var rng = new Random(windowSeed);
                        var patternSettings = settings ?? new KeyPatternSettings();

                        if (!shouldSkipDenseWindow(patternType, objects, currentTime, windowDuration, beatLength, beatmap.TotalColumns))
                            applyPattern(windowObjects, beatmap, currentTime, windowEnd, patternSettings, rng, 1);
                    }
                    finally
                    {
                        returnWindowObjectsToPool(windowObjects);
                    }
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
                                                              Action<List<ManiaHitObject>, ManiaBeatmap, double, double, KeyPatternSettings, Random, int> applyPattern,
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

            int processIntervalSafe = Math.Clamp(windowProcessInterval, 1, 4);
            int processOffsetSafe = Math.Clamp(windowProcessOffset - 1, 0, processIntervalSafe - 1);
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

                // 按窗口索引缓存，避免整拍级别缓存造成误跳过
                long skipWindowIndex = windowDuration > 0 ? (long)Math.Floor(currentTime / windowDuration) : (long)Math.Floor(currentTime);

                if (!skipCache.TryGetValue(skipWindowIndex, out bool skip))
                {
                    skip = shouldSkipDenseWindow(patternType, objects, currentTime, windowDuration, beatLength, beatmap.TotalColumns);
                    skipCache[skipWindowIndex] = skip;
                }

                if (!skip && windowCount > 0 && (skipWindowIndex % processIntervalSafe) == processOffsetSafe)
                {
                    int oscillationBeatsSafe = Math.Max(1, oscillationBeats);
                    long beatIndex = beatLength > 0 ? (long)Math.Round(currentTime / beatLength) : (long)Math.Round(currentTime);
                    long oscillationIndex = beatIndex / oscillationBeatsSafe;
                    oscillator.Reset(unchecked(seed + oscillationIndex));
                    double oscValue = oscillator.NextSigned();

                    int windowSeed = unchecked(seed * 397) ^ (int)Math.Round(currentTime);
                    var rng = new Random(windowSeed);
                    var windowObjects = buildWindowObjects(objects, startIndex, endIndex);
                    try
                    {
                        if (!shouldSkipDenseWindow(patternType, objects, currentTime, windowDuration, beatLength, beatmap.TotalColumns))
                        {
                            bool useLevelFallback = patternType == KeyPatternType.Jack
                                                    || patternType == KeyPatternType.Jump
                                                    || patternType == KeyPatternType.Cut
                                                    || patternType == KeyPatternType.Cross;

                            if (!useLevelFallback)
                            {
                                var settings = getPatternSettingsFromLevel(level, beatmap.TotalColumns, oscValue, patternType);
                                if (settings.MaxK > 0)
                                    applyPattern(windowObjects, beatmap, currentTime, windowEnd, settings, rng, maxIterationsPerWindow);
                            }
                            else
                            {
                                int beforeCount = beatmap.HitObjects.Count;
                                bool applied = false;

                                for (int current = level; current >= 1; current--)
                                {
                                    var settings = getPatternSettingsFromLevel(current, beatmap.TotalColumns, oscValue, patternType);
                                    if (settings.MaxK <= 0)
                                        continue;

                                    applyPattern(windowObjects, beatmap, currentTime, windowEnd, settings, rng, maxIterationsPerWindow);

                                    if (beatmap.HitObjects.Count > beforeCount)
                                    {
                                        applied = true;
                                        break;
                                    }
                                }

                                if (!applied)
                                {
                                    // No changes; keep window as-is.
                                }
                            }
                        }
                    }
                    finally
                    {
                        returnWindowObjectsToPool(windowObjects);
                    }
                }

                if (stepDuration <= 0)
                    break;

                windowIndex++;
                currentTime += stepDuration;
            }
        }

        private static bool shouldSkipDenseWindow(KeyPatternType patternType,
                                                  List<ManiaHitObject> objects,
                                                  double windowStart,
                                                  double windowDuration,
                                                  double beatLength,
                                                  int totalColumns)
        {
            if (patternType == KeyPatternType.Delay || patternType == KeyPatternType.Dump)
                return false;

            if (objects.Count == 0 || beatLength <= 0)
                return false;

            const double tolerance = 5.0; // ms

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

            bool isOn1To4(double time)
            {
                return isOnDivisionLocal(time, 1)
                       || isOnDivisionLocal(time, 2)
                       || isOnDivisionLocal(time, 3)
                       || isOnDivisionLocal(time, 4);
            }

            bool isQuarterOrFiner(double time)
            {
                return isOnDivisionLocal(time, 4) || !isOn1To4(time);
            }

            // 半窗口内：1/4 以上（不含 1/4）的 note 数量 >= 2 => 跳过
            if (windowDuration > 0)
            {
                double halfWindowEnd = windowStart + (windowDuration / 2.0);

                if (halfWindowEnd > windowStart)
                {
                    int halfIndex = lowerBoundByTime(objects, windowStart);
                    int fineCount = 0;

                    for (int i = halfIndex; i < objects.Count; i++)
                    {
                        var obj = objects[i];
                        if (obj.StartTime >= halfWindowEnd)
                            break;

                        if (!isOn1To4(obj.StartTime))
                        {
                            fineCount++;
                            if (fineCount >= 2)
                                return true;
                        }
                    }

                    // 半窗口内：检查是否存在 >=3 个 note 且列位置单调（滑梯）
                    // 这里包含所有节拍细分（包括 1/1,1/2,1/3,1/4 等），
                    // 按时间顺序构建每个时间点的列集合；同一时间点若存在多个不同列视为不确定并重置单调判断。
                    var halfGroups = new List<(double time, List<int> cols)>();

                    for (int i = halfIndex; i < objects.Count; i++)
                    {
                        var obj = objects[i];
                        if (obj.StartTime >= halfWindowEnd)
                            break;

                        if (halfGroups.Count == 0 || Math.Abs(halfGroups[^1].time - obj.StartTime) > tolerance)
                        {
                            var cols = new List<int> { obj.Column };
                            halfGroups.Add((obj.StartTime, cols));
                        }
                        else
                        {
                            halfGroups[^1].cols.Add(obj.Column);
                        }
                    }

                    if (halfGroups.Count >= 3)
                    {
                        // 构建按时间排序的列序列；若同一时间点有多个不同列，标记为不确定（使用 int.MinValue）
                        var seq = new List<int>(halfGroups.Count);
                        foreach (var g in halfGroups)
                        {
                            var distinct = g.cols.Distinct().ToList();
                            if (distinct.Count == 1)
                                seq.Add(distinct[0]);
                            else
                                seq.Add(int.MinValue);
                        }

                        int incRun = 1;
                        int decRun = 1;
                        int prev = seq[0];

                        for (int i = 1; i < seq.Count; i++)
                        {
                            int cur = seq[i];
                            if (cur == int.MinValue || prev == int.MinValue)
                            {
                                // 不确定点：重置计数器并继续
                                incRun = decRun = 1;
                                prev = cur;
                                continue;
                            }

                            if (cur > prev)
                            {
                                incRun++;
                                decRun = 1;
                            }
                            else if (cur < prev)
                            {
                                decRun++;
                                incRun = 1;
                            }
                            else
                            {
                                incRun = decRun = 1;
                            }

                            if (incRun >= 3 || decRun >= 3)
                                return true;

                            prev = cur;
                        }
                    }
                }
            }

            // Jack/Jump/Cut/Cross: 窗口内出现 1/4 及以上（含 1/4）单调变化则跳过
            if (patternType == KeyPatternType.Jack
                || patternType == KeyPatternType.Jump
                || patternType == KeyPatternType.Cut
                || patternType == KeyPatternType.Cross)
            {
                var grouped = new List<(double time, double avgCol)>();
                int monoIndex = lowerBoundByTime(objects, windowStart);
                double windowEndLocal = windowDuration > 0 ? windowStart + windowDuration : windowStart + beatLength;

                for (int i = monoIndex; i < objects.Count; i++)
                {
                    var obj = objects[i];
                    if (obj.StartTime >= windowEndLocal)
                        break;

                    if (!isQuarterOrFiner(obj.StartTime))
                        continue;

                    if (grouped.Count == 0 || Math.Abs(grouped[^1].time - obj.StartTime) > tolerance)
                        grouped.Add((obj.StartTime, obj.Column));
                    else
                    {
                        var last = grouped[^1];
                        grouped[^1] = (last.time, (last.avgCol + obj.Column) / 2.0);
                    }
                }

                if (grouped.Count >= 3)
                {
                    int incRun = 1;
                    int decRun = 1;

                    for (int i = 1; i < grouped.Count; i++)
                    {
                        if (grouped[i].avgCol > grouped[i - 1].avgCol)
                        {
                            incRun++;
                            decRun = 1;
                        }
                        else if (grouped[i].avgCol < grouped[i - 1].avgCol)
                        {
                            decRun++;
                            incRun = 1;
                        }
                        else
                        {
                            incRun = 1;
                            decRun = 1;
                        }

                        if (incRun >= 3 || decRun >= 3)
                            return true;
                    }
                }
            }

            return false;
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

        internal static bool HasDenseBurstBetweenQuarterNotes(List<ManiaHitObject> windowObjects,
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
                            countQuarter++;
                        else
                            countOther++;

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
                int mid = left + ((right - left) / 2);
                if (objects[mid].StartTime < time)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        internal static bool HasObjectsInColumnRange(List<ManiaHitObject> objects,
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
            List<ManiaHitObject> windowObjects;

            if (windowObjectsPool.Count > 0)
            {
                windowObjects = windowObjectsPool.Pop();
                windowObjects.Clear();
                if (windowObjects.Capacity < count)
                    windowObjects.Capacity = count;
            }
            else
            {
                windowObjects = new List<ManiaHitObject>(count);
            }

            for (int i = startIndex; i < endIndex; i++)
                windowObjects.Add(objects[i]);

            return windowObjects;
        }

        private static void returnWindowObjectsToPool(List<ManiaHitObject> list)
        {
            if (list == null)
                return;

            // keep pool bounded to avoid unbounded memory usage
            if (windowObjectsPool.Count < 64)
            {
                list.Clear();
                windowObjectsPool.Push(list);
            }
        }

        internal static HashSet<int> GetColumnsAtTime(List<ManiaHitObject> objects, double time, double tolerance)
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

        internal static List<Note> GetNotesAtTime(List<ManiaHitObject> objects, double time, double tolerance)
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
                TryAddNoteFromCandidates(beatmap, availableColumns, null, rng, time);
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

        internal static double GetActiveBeatFraction(List<ManiaHitObject> windowObjects, ManiaBeatmap beatmap, double defaultFraction)
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
                if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key > bestFraction))
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

        internal static bool TryGetCutJackBaseFraction(double activeFraction, out double baseFraction)
        {
            const double eps = 1e-6;

            if (Math.Abs(activeFraction - (1.0 / 3.0)) < eps)
            {
                baseFraction = 1.0 / 3.0;
                return true;
            }

            if (Math.Abs(activeFraction - 1.0) < eps
                || Math.Abs(activeFraction - (1.0 / 2.0)) < eps
                || Math.Abs(activeFraction - (1.0 / 4.0)) < eps)
            {
                baseFraction = 1.0 / 4.0;
                return true;
            }

            baseFraction = 0;
            return false;
        }

        internal static List<int> BuildNearestCandidates(IReadOnlyList<int> columns, int index)
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

        internal static bool TryAddNoteFromOrderedCandidates(ManiaBeatmap beatmap, IReadOnlyList<int> candidates, double time)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                int column = candidates[i];

                if (IsHoldOccupyingColumn(beatmap, column, time))
                    continue;

                if (HasNoteAtTime(beatmap, column, time))
                    return false;

                beatmap.HitObjects.Add(new Note { Column = column, StartTime = time });
                return true;
            }

            return false;
        }

        internal static bool TryAddNoteFromCandidates(ManiaBeatmap beatmap,
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

            if (HasNoteAtTime(beatmap, column, time))
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
                    ? PickWeightedIndex(weights, rng)
                    : rng.Next(candidates.Count);

                int column = candidates[picked];
                if (!IsHoldOccupyingColumn(beatmap, column, time))
                    return picked;

                candidates.RemoveAt(picked);
                weights?.RemoveAt(picked);
            }

            return -1;
        }

        // Placement helpers (shared rule: hold occupied -> reselect, note exists -> skip).

        internal static bool HasNoteAtTime(ManiaBeatmap beatmap, int column, double time, ManiaHitObject? ignore = null, double tolerance = 0.5)
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

                if (HasNoteAtTime(beatmap, hold.Column, newStart, hold))
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

            if (HasNoteAtTime(beatmap, obj.Column, targetTime, obj))
                return false;

            if (IsHoldOccupyingColumn(beatmap, obj.Column, targetTime, obj))
            {
                holdConflict = true;
                return false;
            }

            obj.StartTime = targetTime;
            return true;
        }

        internal static bool IsHoldOccupyingColumn(ManiaBeatmap beatmap, int column, double time, ManiaHitObject? ignore = null, double tolerance = 0.5)
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

        internal static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        internal static double DistanceToNearest(HashSet<int> occupied, int col)
        {
            double best = double.MaxValue;
            foreach (int o in occupied)
                best = Math.Min(best, Math.Abs(col - o));
            return best == double.MaxValue ? 0 : best;
        }

        internal static double GetMedian(HashSet<int> values)
        {
            var ordered = values.OrderBy(v => v).ToList();
            int count = ordered.Count;

            if (count == 0)
                return 0;

            if (count % 2 == 1)
                return ordered[count / 2];

            return (ordered[(count / 2) - 1] + ordered[count / 2]) / 2.0;
        }

        internal static double GetMedianFromList(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
                return 0;

            var ordered = values.OrderBy(v => v).ToList();
            int count = ordered.Count;

            if (count % 2 == 1)
                return ordered[count / 2];

            return (ordered[(count / 2) - 1] + ordered[count / 2]) / 2.0;
        }

        internal static int PickWeightedIndex(IReadOnlyList<double> weights, Random rng)
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
