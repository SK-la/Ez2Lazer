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
        Stair,
        Cross,
        Stack,
        Beat
    }

    public class KeyPatternSettings
    {
        public int MinK { get; set; } = 1;
        public int MaxK { get; set; } = 4;
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

        public static void CleanOverlaps(ManiaBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            var toRemove = new HashSet<ManiaHitObject>();

            foreach (var group in beatmap.HitObjects.GroupBy(h => h.Column))
            {
                double currentEnd = double.MinValue;
                ManiaHitObject? current = null;

                foreach (var obj in group.OrderBy(h => h.StartTime))
                {
                    double objEnd = obj is HoldNote hold ? hold.EndTime : obj.StartTime;

                    if (current != null && obj.StartTime < currentEnd)
                    {
                        toRemove.Add(obj);
                        continue;
                    }

                    current = obj;
                    currentEnd = Math.Max(currentEnd, objEnd);
                }
            }

            if (toRemove.Count == 0)
                return;

            foreach (var obj in toRemove)
                beatmap.HitObjects.Remove(obj);
        }

        public static void SimplifyDenseNotes(ManiaBeatmap beatmap, int maxNotesPerWindow, int windowQuarterBeats = 2)
        {
            if (beatmap.HitObjects.Count == 0 || maxNotesPerWindow <= 0)
                return;

            var objects = beatmap.HitObjects
                                 .OrderBy(h => h.StartTime)
                                 .ToList();

            var toRemove = new HashSet<ManiaHitObject>();
            var window = new Queue<ManiaHitObject>();
            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);

            foreach (var obj in objects)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(obj.StartTime).BeatLength;
                double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;

                while (window.Count > 0 && obj.StartTime - window.Peek().StartTime > windowDuration)
                    window.Dequeue();

                window.Enqueue(obj);

                while (window.Count > maxNotesPerWindow)
                {
                    var remove = window.Last();
                    toRemove.Add(remove);
                    window = new Queue<ManiaHitObject>(window.Where(o => o != remove));
                }
            }

            if (toRemove.Count == 0)
                return;

            foreach (var obj in toRemove)
                beatmap.HitObjects.Remove(obj);
        }

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

        private static void applyPattern(List<ManiaHitObject> windowObjects, KeyPatternType patternType, ManiaBeatmap beatmap)
        {
            switch (patternType)
            {
                case KeyPatternType.Cut:
                    // TODO: 切
                    break;

                case KeyPatternType.Stair:
                    // TODO: 楼梯
                    break;

                case KeyPatternType.Cross:
                    // TODO: 叉
                    break;

                case KeyPatternType.Stack:
                    // TODO: 叠
                    break;

                case KeyPatternType.Beat:
                    // TODO: 拍
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
            var patternSettings = settings ?? new KeyPatternSettings();

            double activeBeatFraction = getActiveBeatFraction(windowObjects, beatmap, 1.0 / 4);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(windowStart).BeatLength;

            double mainFraction = getPatternMainFraction(patternType, activeBeatFraction);
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;
            if (subStep <= 0)
                return;

            const double time_tolerance = 0.5;
            var rng = new Random((int)Math.Round(windowStart));

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

                    case KeyPatternType.Stair:
                        applyStairIncremental(beatmap, windowObjects, prevLine, nextLine, mainStep, subStep, t, time_tolerance, rng);
                        break;

                    case KeyPatternType.Stack:
                        applyStackIncremental(beatmap, prevColumns, nextColumns, t, minK, maxK, rng);
                        break;

                    case KeyPatternType.Beat:
                        applyBeatIncremental(beatmap, t, minK, maxK, rng);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(patternType), patternType, null);
                }
            }
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

                case KeyPatternType.Stack:
                    return Math.Max(active, 1.0 / 4);

                case KeyPatternType.Beat:
                    return Math.Max(active, 1.0 / 2);

                case KeyPatternType.Stair:
                    return active;

                default:
                    return active;
            }
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

            shuffle(availableColumns, rng);

            for (int i = 0; i < count; i++)
                beatmap.HitObjects.Add(new Note { Column = availableColumns[i], StartTime = time });
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
                shuffle(available, rng);
                for (int i = 0; i < count; i++)
                    beatmap.HitObjects.Add(new Note { Column = available[i], StartTime = time });
                return;
            }

            double median = getMedianFromList(available);
            var weights = available.Select(c => Math.Max(0.001, Math.Abs(c - median))).ToList();

            for (int i = 0; i < count; i++)
            {
                int picked = pickWeightedIndex(weights, rng);
                beatmap.HitObjects.Add(new Note { Column = available[picked], StartTime = time });

                available.RemoveAt(picked);
                weights.RemoveAt(picked);
            }
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
                shuffle(available, rng);
                for (int i = 0; i < count; i++)
                    beatmap.HitObjects.Add(new Note { Column = available[i], StartTime = time });
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
            {
                int picked = pickWeightedIndex(weights, rng);
                beatmap.HitObjects.Add(new Note { Column = available[picked], StartTime = time });

                available.RemoveAt(picked);
                weights.RemoveAt(picked);
            }
        }

        private static void applyStackIncremental(ManiaBeatmap beatmap,
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
            shuffle(list, rng);

            for (int i = 0; i < count; i++)
                beatmap.HitObjects.Add(new Note { Column = list[i], StartTime = time });
        }

        private static void applyBeatIncremental(ManiaBeatmap beatmap, double time, int minK, int maxK, Random rng)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var columns = Enumerable.Range(0, totalColumns).ToList();

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, columns.Count);

            shuffle(columns, rng);

            for (int i = 0; i < count; i++)
                beatmap.HitObjects.Add(new Note { Column = columns[i], StartTime = time });
        }

        private static void applyStairIncremental(ManiaBeatmap beatmap,
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
                int column = betweenCols[i];
                beatmap.HitObjects.Add(new Note { Column = column, StartTime = time });
            }
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
