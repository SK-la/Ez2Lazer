// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class KeyPatternSettings
    {
        public int MinK { get; set; } = 1;
        public int MaxK { get; set; } = 5;

        public int Level { get; set; }

        // 新增：用于控制预处理跳过判定的两个阈值
        public int FineCountThreshold { get; set; } = 2;
        public int QuarterLineDivisor { get; set; } = 2;

        // 以下为窗口/振荡相关参数，已合并入设置以便统一传递与配置
        public int OscillationBeats { get; set; } = 1;
        public int WindowProcessInterval { get; set; } = 1;
        public int WindowProcessOffset { get; set; } // 0-based
        public int MaxIterationsPerWindow { get; set; } = 1;
        public int WindowQuarterBeats { get; set; } = 2;

        public int IntervalQuarterBeats { get; set; } = 4;

        // 指定用于振荡器与随机的确定性种子（必须为非空）
        public int Seed { get; set; }
    }

    public static class ManiaKeyPatternHelp
    {
        internal readonly struct WindowContext
        {
            public readonly double BeatLength;
            public readonly double WindowStart;
            public readonly double WindowEnd;
            public readonly double WindowDuration;
            public readonly double StepDuration;
            public readonly int StartIndex;
            public readonly int EndIndex;
            public readonly int TotalColumns;
            public readonly double Tolerance;

            public WindowContext(double beatLength, double windowStart, double windowDuration, double stepDuration, int startIndex, int endIndex, int totalColumns, double tolerance)
            {
                BeatLength = beatLength;
                WindowStart = windowStart;
                WindowDuration = windowDuration;
                WindowEnd = windowStart + windowDuration;
                StepDuration = stepDuration;
                StartIndex = startIndex;
                EndIndex = endIndex;
                TotalColumns = totalColumns;
                Tolerance = tolerance;
            }
        }

        private static readonly double[] even_beat_fractions =
        {
            1.0, 1.0 / 2, 1.0 / 4, 1.0 / 8, 1.0 / 12, 1.0 / 16, 1.0 / 24, 1.0 / 32
        };

        private static readonly double[] odd_beat_fractions =
        {
            1.0, 1.0 / 3, 1.0 / 6, 1.0 / 9, 1.0 / 12
        };

        // Pools to reduce per-window allocations (thread-local to avoid cross-thread corruption)
        private static readonly ThreadLocal<Stack<List<ManiaHitObject>>> window_objects_pool =
            new ThreadLocal<Stack<List<ManiaHitObject>>>(() => new Stack<List<ManiaHitObject>>(32));

        // 可改为重载振荡器，预处理跳过检查剥离、前置（仅传入 beatmap），返回标记传入振荡处理器
        // 窗口重构：
        // 1、提前划分好 Beat 序号片段；
        // 2、无参步进跳过，记录跳过序号数组；
        // 3、有参步进处理，半拍切块，传入键型处理2次。参数：ManiaBeatmap, Type, PSSettings, lamda覆写
        // 4、apply方法内部实现具体逻辑，重复处理次数
        // 5、最终统一清洗
        public static void ProcessRollingWindowWithOscillator(ManiaBeatmap beatmap,
                                                              KeyPatternType patternType,
                                                              KeyPatternSettings psSettings,
                                                              IEzOscillator oscillator,
                                                              Action<List<ManiaHitObject>, ManiaBeatmap, double, double, KeyPatternSettings, Random, int> applyPattern)
        {
            if (psSettings.Level <= 0)
                return;

            var objects = beatmap.HitObjects.ToList();
            objects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            if (objects.Count == 0)
                return;

            double currentTime = getRedLineStart(beatmap, objects[0].StartTime);
            double endTime = getMaxEndTime(objects);

            // 使用首个 timing point 的 beatLength 作为固定参考，忽略中途的 timing point 变化（视觉变速不影响 note 对齐）
            double fixedBeatLength = beatmap.ControlPointInfo.TimingPointAt(objects[0].StartTime).BeatLength;

            int windowQuarterBeatsSafe = Math.Max(1, psSettings.WindowQuarterBeats);
            int intervalQuarterBeatsSafe = Math.Max(1, psSettings.IntervalQuarterBeats);

            int processIntervalSafe = Math.Clamp(psSettings.WindowProcessInterval, 1, 4);
            int processOffsetSafe = Math.Clamp(psSettings.WindowProcessOffset, 0, processIntervalSafe - 1);
            // 预先划分窗口（仅缓存窗口时间与 skip 决策），避免在主循环中重复计算节拍边界。
            double beatLength = fixedBeatLength;
            double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;
            double stepDuration = windowDuration * (intervalQuarterBeatsSafe / 4.0);

            if (stepDuration <= 0)
                return;

            var windowInfos = new List<(long index, double start, double end, bool skip)>();
            long totalWindows = Math.Max(0, (long)Math.Ceiling((endTime - currentTime) / stepDuration));

            for (long wi = 0; wi <= totalWindows; wi++)
            {
                double wStart = currentTime + wi * stepDuration;
                double wEnd = wStart + windowDuration;
                var ctx = new WindowContext(beatLength, wStart, windowDuration, stepDuration, 0, 0, beatmap.TotalColumns, 5.0);
                bool skip = shouldSkipDenseWindow(patternType, objects, ctx, psSettings);

                windowInfos.Add((wi, wStart, wEnd, skip));
            }

            // 处理窗口：在需要时再计算 startIndex/endIndex 与构建 windowObjects，以便 applyPattern 修改后可重建 objects
            for (int wi = 0; wi < windowInfos.Count; wi++)
            {
                var info = windowInfos[wi];
                if (info.skip)
                    continue;

                if (info.index % processIntervalSafe != processOffsetSafe)
                    continue;

                // 计算当前对象范围（使用最新的 objects 列表）
                int startIndex = lowerBoundByTime(objects, info.start);
                int endIndex = upperBoundByTime(objects, info.end);
                int windowCount = Math.Max(0, endIndex - startIndex);
                // 仅跳过空窗口，避免传入空列表
                if (windowCount <= 1)
                    continue;

                int oscillationBeatsSafe = Math.Max(1, psSettings.OscillationBeats);
                long beatIndex = beatLength > 0 ? (long)Math.Round(info.start / beatLength) : (long)Math.Round(info.start);
                long oscillationIndex = beatIndex / oscillationBeatsSafe;

                resetOscillator(oscillator, psSettings.Seed, oscillationIndex);
                double oscValue = oscillator.NextSigned();

                int windowSeed = unchecked(psSettings.Seed * 397) ^ (int)info.index;
                var rng = new Random(windowSeed);

                var windowObjects = buildWindowObjects(objects, startIndex, endIndex);

                try
                {
                    bool useLevelFallback = patternType == KeyPatternType.Jack
                                            || patternType == KeyPatternType.Chord
                                            || patternType == KeyPatternType.Bracket;

                    if (!useLevelFallback)
                    {
                        var settings = getPatternSettingsFromLevel(psSettings.Level, beatmap.TotalColumns, oscValue, patternType);
                        if (settings.MaxK > 0)
                            applyPattern(windowObjects, beatmap, info.start, info.end, settings, rng, psSettings.MaxIterationsPerWindow);
                    }
                    else
                    {
                        int beforeCount = beatmap.HitObjects.Count;

                        for (int current = psSettings.Level; current >= 1; current--)
                        {
                            var settings = getPatternSettingsFromLevel(current, beatmap.TotalColumns, oscValue, patternType);
                            if (settings.MaxK <= 0)
                                continue;

                            applyPattern(windowObjects, beatmap, info.start, info.end, settings, rng, psSettings.MaxIterationsPerWindow);

                            if (beatmap.HitObjects.Count > beforeCount)
                                break;
                        }
                    }
                }
                finally
                {
                    // 若 applyPattern 修改了 beatmap，则重建 objects 以便后续窗口使用最新数据
                    bool changed = beatmap.HitObjects.Count != objects.Count;

                    if (changed)
                    {
                        objects = beatmap.HitObjects.ToList();
                        objects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
                    }

                    returnWindowObjectsToPool(windowObjects);
                }
            }
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

        internal static bool HasDenseBurstBetweenQuarterNotes(List<ManiaHitObject> windowObjects,
                                                              double beatLength,
                                                              int totalColumns,
                                                              KeyPatternSettings psSettings)
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
                    int fineThreshold = psSettings.FineCountThreshold;
                    int quarterDiv = psSettings.QuarterLineDivisor;
                    int mixedThreshold = Math.Max(fineThreshold, totalColumns / Math.Max(1, quarterDiv));

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

        internal static double GetActiveBeatFraction(List<ManiaHitObject> windowObjects, ManiaBeatmap beatmap, double defaultFraction)
        {
            if (windowObjects.Count < 2)
                return defaultFraction;

            double startTime = beatmap.HitObjects.Min(h => h.StartTime);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(startTime).BeatLength;
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

        internal static bool TryGetCutJackBaseFraction(double activeFraction, out double baseFraction)
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

        /// <summary>
        ///     在给定的 quarter 时间点及其前后 quarter 时间范围内，寻找最接近原列且在这三个时间点上均无冲突的列。
        ///     返回找到的列索引，找不到则返回 null。
        /// </summary>
        internal static int? FindNearestAvailableColumnForQuarter(ManiaBeatmap beatmap,
                                                                  int originalCol,
                                                                  double qTime,
                                                                  double prevTime,
                                                                  double nextTime,
                                                                  int totalColumns,
                                                                  double tolerance)
        {
            if (totalColumns <= 0)
                return null;

            int chosen = -1;
            int bestOffset = int.MaxValue;

            for (int col = 0; col < totalColumns; col++)
            {
                if (HasNoteAtTime(beatmap, col, qTime, null, tolerance))
                    continue;

                if (IsHoldOccupyingColumn(beatmap, col, qTime, null, tolerance))
                    continue;

                bool pv = HasNoteAtTime(beatmap, col, prevTime, null, tolerance);
                bool nx = HasNoteAtTime(beatmap, col, nextTime, null, tolerance);

                if (!pv && !nx)
                {
                    int offsetCol = Math.Abs(col - originalCol);

                    if (offsetCol < bestOffset)
                    {
                        bestOffset = offsetCol;
                        chosen = col;
                    }
                }
            }

            return chosen >= 0 ? chosen : null;
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

            return (ordered[count / 2 - 1] + ordered[count / 2]) / 2.0;
        }

        internal static double GetMedianFromList(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
                return 0;

            var ordered = values.OrderBy(v => v).ToList();
            int count = ordered.Count;

            if (count % 2 == 1)
                return ordered[count / 2];

            return (ordered[count / 2 - 1] + ordered[count / 2]) / 2.0;
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

        private static bool shouldSkipDenseWindow(KeyPatternType patternType,
                                                  List<ManiaHitObject> objects,
                                                  WindowContext ctx,
                                                  KeyPatternSettings? psSettings)
        {
            if (patternType == KeyPatternType.Delay || patternType == KeyPatternType.Dump)
                return false;
            if (objects.Count == 0 || ctx.BeatLength <= 0)
                return false;

            bool isOnDivisionLocal(double time, int divisor)
            {
                if (divisor <= 0)
                    return false;

                double interval = ctx.BeatLength / divisor;
                if (interval <= 0)
                    return false;

                double mod = time % interval;
                return mod <= ctx.Tolerance || Math.Abs(interval - mod) <= ctx.Tolerance;
            }

            bool isOn1To4(double time) => isOnDivisionLocal(time, 1)
                                          || isOnDivisionLocal(time, 2)
                                          || isOnDivisionLocal(time, 3)
                                          || isOnDivisionLocal(time, 4);

            bool isQuarterOrFiner(double time) => isOnDivisionLocal(time, 4) || !isOn1To4(time);

            // 半窗口内：1/4 以上（不含 1/4）的 note 数量 >= 2 => 跳过
            // 检查前后两半拍：任意一半满足条件即跳过
            if (ctx.WindowDuration > 0)
            {
                double halfWindowMid = ctx.WindowStart + ctx.WindowDuration / 2.0;
                double halfStarts = ctx.WindowStart;
                double halfEnds = halfWindowMid;
                double secondHalfStarts = halfWindowMid;
                double secondHalfEnds = ctx.WindowStart + ctx.WindowDuration;

                bool checkHalf(double halfStart, double halfEnd)
                {
                    if (!(halfEnd > halfStart))
                        return false;

                    int halfIndexLocal = lowerBoundByTime(objects, halfStart);

                    int fineCountLocal = 0;
                    int fineThresholdLocal = psSettings?.FineCountThreshold ?? 2;

                    for (int i = halfIndexLocal; i < objects.Count; i++)
                    {
                        var obj = objects[i];
                        if (obj.StartTime >= halfEnd)
                            break;

                        if (!isOn1To4(obj.StartTime))
                        {
                            fineCountLocal++;
                            if (fineCountLocal >= fineThresholdLocal)
                                return true;
                        }
                    }

                    var halfGroups = new List<(double time, List<int> cols)>();

                    for (int i = halfIndexLocal; i < objects.Count; i++)
                    {
                        var obj = objects[i];
                        if (obj.StartTime >= halfEnd)
                            break;

                        if (halfGroups.Count == 0 || Math.Abs(halfGroups[^1].time - obj.StartTime) > ctx.Tolerance)
                        {
                            var cols = new List<int> { obj.Column };
                            halfGroups.Add((obj.StartTime, cols));
                        }
                        else
                            halfGroups[^1].cols.Add(obj.Column);
                    }

                    if (halfGroups.Count >= 3)
                    {
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
                        int prevLocal = seq[0];

                        for (int i = 1; i < seq.Count; i++)
                        {
                            int cur = seq[i];

                            if (cur == int.MinValue || prevLocal == int.MinValue)
                            {
                                incRun = decRun = 1;
                                prevLocal = cur;
                                continue;
                            }

                            if (cur > prevLocal)
                            {
                                incRun++;
                                decRun = 1;
                            }
                            else if (cur < prevLocal)
                            {
                                decRun++;
                                incRun = 1;
                            }
                            else
                                incRun = decRun = 1;

                            if (incRun >= 3 || decRun >= 3)
                                return true;

                            prevLocal = cur;
                        }
                    }

                    return false;
                }

                if (checkHalf(halfStarts, halfEnds) || checkHalf(secondHalfStarts, secondHalfEnds))
                    return true;
            }

            // Jack/Jump/Stream: 窗口内出现 1/4 及以上（含 1/4）单调变化则跳过
            if (patternType == KeyPatternType.Jack
                || patternType == KeyPatternType.Chord
                || patternType == KeyPatternType.Bracket)
            {
                var grouped = new List<(double time, double avgCol)>();
                int monoIndex = lowerBoundByTime(objects, ctx.WindowStart);
                double windowEndLocal = ctx.WindowDuration > 0 ? ctx.WindowStart + ctx.WindowDuration : ctx.WindowStart + ctx.BeatLength;

                for (int i = monoIndex; i < objects.Count; i++)
                {
                    var obj = objects[i];
                    if (obj.StartTime >= windowEndLocal)
                        break;

                    if (!isQuarterOrFiner(obj.StartTime))
                        continue;

                    if (grouped.Count == 0 || Math.Abs(grouped[^1].time - obj.StartTime) > ctx.Tolerance)
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

        // 返回第一个 StartTime > time 的位置（用于包含窗口 end 边界）
        private static int upperBoundByTime(List<ManiaHitObject> objects, double time)
        {
            int left = 0;
            int right = objects.Count;

            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (objects[mid].StartTime <= time)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static List<ManiaHitObject> buildWindowObjects(List<ManiaHitObject> objects, int startIndex, int endIndex)
        {
            int count = Math.Max(0, endIndex - startIndex);
            List<ManiaHitObject> windowObjects;

            var pool = window_objects_pool.Value;

            if (pool?.Count > 0)
            {
                windowObjects = pool.Pop();
                windowObjects.Clear();
                if (windowObjects.Capacity < count)
                    windowObjects.Capacity = count;
            }
            else
                windowObjects = new List<ManiaHitObject>(count);

            for (int i = startIndex; i < endIndex; i++)
                windowObjects.Add(objects[i]);

            return windowObjects;
        }

        private static void returnWindowObjectsToPool(List<ManiaHitObject> list)
        {
            var pool = window_objects_pool.Value;

            // keep pool bounded to avoid unbounded memory usage
            if (pool?.Count < 128)
            {
                list.Clear();
                pool.Push(list);
            }
        }

        private static void resetOscillator(IEzOscillator oscillator, int seed, long oscillationIndex)
        {
            oscillator.Reset(unchecked(seed + oscillationIndex));
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

                case KeyPatternType.Chord:
                    maxCount = Math.Clamp(level, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 2);
                    break;

                case KeyPatternType.Dump:
                    maxCount = Math.Clamp(level + 1, 1, totalColumns);
                    minCount = Math.Max(1, maxCount - 2);
                    break;

                case KeyPatternType.Bracket:
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
    }
}
