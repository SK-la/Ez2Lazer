// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public enum KeyPatternType
    {
        Cut,
        Dump,
        Cross,
        Jack,
        Beat,
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
                                                KeyPatternSettings? settings = null)
        {
            var objects = beatmap.HitObjects
                                 .OrderBy(h => h.StartTime)
                                 .ToList();

            if (objects.Count == 0)
                return;

            double currentTime = objects.Min(h => h.StartTime);
            double endTime = objects.Max(h => h is HoldNote hold ? hold.EndTime : h.StartTime);

            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);
            int intervalQuarterBeatsSafe = Math.Max(1, intervalQuarterBeats);

            while (currentTime <= endTime)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(currentTime).BeatLength;
                double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;
                double stepDuration = windowDuration * (intervalQuarterBeatsSafe / 4.0);

                double windowEnd = currentTime + windowDuration;

                var windowObjects = objects.Where(h => h.StartTime >= currentTime && h.StartTime < windowEnd)
                                           .ToList();

                if (windowObjects.Count > 0)
                {
                    if (incrementalMode)
                        applyIncrementalPattern(windowObjects, patternType, beatmap, currentTime, windowEnd, windowDuration, settings);
                    else
                        applyPattern(windowObjects, patternType, beatmap);
                }

                if (stepDuration <= 0)
                    break;

                currentTime += stepDuration;
            }
        }

        public static void ProcessRollingWindowWithOscillator(ManiaBeatmap beatmap,
                                                              KeyPatternType patternType,
                                                              int level,
                                                              EzOscillator oscillator,
                                                              int seed,
                                                              int oscillationBeats,
                                                              int windowQuarterBeats = 2,
                                                              int intervalQuarterBeats = 4)
        {
            if (level <= 0)
                return;

            var objects = beatmap.HitObjects
                                 .OrderBy(h => h.StartTime)
                                 .ToList();

            if (objects.Count == 0)
                return;

            double currentTime = objects.Min(h => h.StartTime);
            double endTime = objects.Max(h => h is HoldNote hold ? hold.EndTime : h.StartTime);

            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);
            int intervalQuarterBeatsSafe = Math.Max(1, intervalQuarterBeats);

            while (currentTime <= endTime)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(currentTime).BeatLength;
                double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;
                double stepDuration = windowDuration * (intervalQuarterBeatsSafe / 4.0);

                double windowEnd = currentTime + windowDuration;

                var windowObjects = objects.Where(h => h.StartTime >= currentTime && h.StartTime < windowEnd)
                                           .ToList();

                if (windowObjects.Count > 0)
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

                        applyIncrementalPattern(windowObjects, patternType, beatmap, currentTime, windowEnd, windowDuration, settings, rng);
                    }
                }

                if (stepDuration <= 0)
                    break;

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

                case KeyPatternType.Beat:
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
                                                    double windowDuration,
                                                    KeyPatternSettings? settings)
        {
            var rng = new Random((int)Math.Round(windowStart));
            applyIncrementalPattern(windowObjects, patternType, beatmap, windowStart, windowEnd, windowDuration, settings, rng);
        }

        private static void applyIncrementalPattern(List<ManiaHitObject> windowObjects,
                                                    KeyPatternType patternType,
                                                    ManiaBeatmap beatmap,
                                                    double windowStart,
                                                    double windowEnd,
                                                    double windowDuration,
                                                    KeyPatternSettings? settings,
                                                    Random rng)
        {
            var patternSettings = settings ?? new KeyPatternSettings();

            double activeBeatFraction = getActiveBeatFraction(windowObjects, beatmap, 1.0 / 4);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(windowStart).BeatLength;

            double mainFraction = getPatternMainFraction(patternType, activeBeatFraction);
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;
            if (subStep <= 0)
                return;

            const double time_tolerance = 0.5;

            for (double t = windowStart + subStep; t < windowEnd; t += subStep)
            {
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
                        applyCutWeightedIncremental(beatmap, prevColumns, nextColumns, t, minK, maxK, rng);
                        break;

                    case KeyPatternType.Cross:
                        applyCrossWeightedIncremental(beatmap, prevColumns, nextColumns, t, minK, maxK, rng);
                        break;

                    case KeyPatternType.Dump:
                        applyDumpIncremental(beatmap, windowObjects, prevLine, nextLine, mainStep, subStep, t, time_tolerance, rng);
                        break;

                    case KeyPatternType.Jack:
                        applyJackIncremental(beatmap, prevColumns, nextColumns, t, minK, maxK, rng);
                        break;

                    case KeyPatternType.Beat:
                        applyBeatIncremental(beatmap, t, minK, maxK, rng);
                        break;

                    case KeyPatternType.Delay:
                        applyDelayPattern(windowObjects, beatmap, patternSettings.Level, rng);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(patternType), patternType, null);
                }
            }
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

                case KeyPatternType.Beat:
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
            var ordered = windowObjects.OrderBy(h => h.StartTime).ToList();
            if (ordered.Count < 2)
                return defaultFraction;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(ordered[0].StartTime).BeatLength;
            if (beatLength <= 0)
                return defaultFraction;

            var gaps = new List<double>();

            for (int i = 1; i < ordered.Count; i++)
            {
                double gap = ordered[i].StartTime - ordered[i - 1].StartTime;
                if (gap > 0)
                    gaps.Add(gap / beatLength);
            }

            if (gaps.Count == 0)
                return defaultFraction;

            double gapFraction = gaps.Min();
            double evenNearest = getNearestFraction(gapFraction, even_beat_fractions);
            double oddNearest = getNearestFraction(gapFraction, odd_beat_fractions);

            return Math.Abs(gapFraction - evenNearest) <= Math.Abs(gapFraction - oddNearest)
                ? evenNearest
                : oddNearest;
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

                case KeyPatternType.Beat:
                    return Math.Max(active, 1.0 / 2);

                case KeyPatternType.Delay:
                    return Math.Max(active, 1.0 / 4);

                case KeyPatternType.Dump:
                    return active;

                default:
                    return active;
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

        private static void applyDelayPattern(List<ManiaHitObject> windowObjects,
                                              ManiaBeatmap beatmap,
                                              int level,
                                              Random rng)
        {
            var groups = windowObjects.GroupBy(o => o.StartTime).ToList();

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

                var indexes = Enumerable.Range(0, noteCount).OrderBy(_ => rng.Next()).Take(count).ToList();

                foreach (int index in indexes)
                {
                    var obj = list[index];
                    double direction = rng.NextDouble() < 0.5 ? -1 : 1;
                    double offset = direction * offsetAmount;

                    if (TryApplyDelayOffset(beatmap, obj, offset, out bool holdConflict))
                        continue;

                    if (holdConflict)
                        TryApplyDelayOffset(beatmap, obj, -offset, out _);
                }
            }
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

        private static HashSet<int> getColumnsAtTime(List<ManiaHitObject> objects, double time, double tolerance)
        {
            var cols = new HashSet<int>();

            foreach (var obj in objects)
            {
                if (Math.Abs(obj.StartTime - time) <= tolerance)
                    cols.Add(obj.Column);
            }

            return cols;
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

        private static void applyCutWeightedIncremental(ManiaBeatmap beatmap,
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

            double median = getMedianFromList(available);
            var weights = available.Select(c => Math.Max(0.001, Math.Abs(c - median))).ToList();

            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, available, weights, rng, time);
        }

        private static void applyCrossWeightedIncremental(ManiaBeatmap beatmap,
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
            double maxDistance = available.Select(c => distanceToNearest(avoid, c)).DefaultIfEmpty(1).Max();
            var weights = available.Select(c =>
            {
                double dist = distanceToNearest(avoid, c);
                double medianBias = maxDistance - Math.Abs(c - median);
                return Math.Max(0.001, dist * 2.0 + medianBias);
            }).ToList();

            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, available, weights, rng, time);
        }

        private static void applyJackIncremental(ManiaBeatmap beatmap,
                                                 HashSet<int> prevColumns,
                                                 HashSet<int> nextColumns,
                                                 double time,
                                                 int minK,
                                                 int maxK,
                                                 Random rng)
        {
            var available = new HashSet<int>(prevColumns);
            available.UnionWith(nextColumns);

            if (available.Count == 0)
                return;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, available.Count);

            var list = available.ToList();
            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, list, null, rng, time);
        }

        private static void applyBeatIncremental(ManiaBeatmap beatmap, double time, int minK, int maxK, Random rng)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var columns = Enumerable.Range(0, totalColumns).ToList();

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, columns.Count);

            for (int i = 0; i < count; i++)
                tryAddNoteFromCandidates(beatmap, columns, null, rng, time);
        }

        private static void applyDumpIncremental(ManiaBeatmap beatmap,
                                                 List<ManiaHitObject> windowObjects,
                                                 double prevLine,
                                                 double nextLine,
                                                 double mainStep,
                                                 double subStep,
                                                 double currentTime,
                                                 double tolerance,
                                                 Random rng)
        {
            var prevCols = getColumnsAtTime(windowObjects, prevLine, tolerance).OrderBy(c => c).ToList();
            var nextCols = getColumnsAtTime(windowObjects, nextLine, tolerance).OrderBy(c => c).ToList();

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

            int maxSteps = (int)Math.Floor(mainStep / subStep) - 1;
            int steps = Math.Min(betweenCols.Count, Math.Max(1, maxSteps));

            for (int i = 0; i < steps; i++)
            {
                double time = prevLine + subStep * (i + 1);
                var candidates = buildNearestCandidates(betweenCols, i);
                tryAddNoteFromOrderedCandidates(beatmap, candidates, time);
            }
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
