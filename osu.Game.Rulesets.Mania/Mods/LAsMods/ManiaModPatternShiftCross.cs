// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftCross : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Cross;
        protected override string PatternName => "Cross";
        protected override string PatternAcronym => "PSX";

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
            double mainFraction = Math.Max(activeBeatFraction, 1.0 / 8.0);
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

            double lineOffset = Math.Floor((t - windowStart) / mainStep) * mainStep;
            double prevLine = windowStart + lineOffset;
            double nextLine = prevLine + mainStep;

            var prevColumns = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, prevLine, TIME_TOLERANCE);
            var nextColumns = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, nextLine, TIME_TOLERANCE);

            applyCrossPattern(beatmap, windowObjects, prevColumns, nextColumns, t, minK, maxK, rng);
        }

        private static void applyCrossPattern(ManiaBeatmap beatmap,
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

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;
            double quarter = beatLength / 4.0;

            // 决定放置使用 1/2 还是 1/3 网格：优先选窗口内已有 note 所处的细分
            int preferredDivisor = 2; // 默认 1/2

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            // 只要窗口内存在高于 1/4 的细分（非 1/1,1/2,1/3,1/4），Cross 直接不处理
            foreach (var obj in windowObjects)
            {
                bool isOn1To4 = isOnDivisionLocal(obj.StartTime, 1)
                                || isOnDivisionLocal(obj.StartTime, 2)
                                || isOnDivisionLocal(obj.StartTime, 3)
                                || isOnDivisionLocal(obj.StartTime, 4);
                if (!isOn1To4)
                    return;
            }

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

            double targetInterval = beatLength / Math.Max(1, preferredDivisor);
            double baseAligned = Math.Round(time / targetInterval) * targetInterval;
            double windowStart = windowObjects.Count > 0 ? windowObjects[0].StartTime : time;
            double windowEnd = windowObjects.Count > 0 ? windowObjects[^1].StartTime : time;

            // 对于已经位于 1/4 网格的 note：若其前后 quarter 时间在该列存在 note，则尝试横向移动到最近的无冲突列。

            foreach (var obj in windowObjects)
            {
                double qTime = Math.Round(obj.StartTime / quarter) * quarter;
                double prevTime = qTime - quarter;
                double nextTime = qTime + quarter;

                bool conflictPrev = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, prevTime, null, TIME_TOLERANCE);
                bool conflictNext = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, nextTime, null, TIME_TOLERANCE);

                if (!conflictPrev && !conflictNext)
                    continue;

                int? chosen = ManiaKeyPatternHelp.FindNearestAvailableColumnForQuarter(beatmap, obj.Column, qTime, prevTime, nextTime, totalColumns, TIME_TOLERANCE);
                if (chosen.HasValue)
                    obj.Column = chosen.Value;
            }

            double? chosenCandidate = null;
            int[] tries = { 0, -1, 1, -2, 2 };

            foreach (int off in tries)
            {
                double ct = baseAligned + off * targetInterval;
                if (ct < windowStart - TIME_TOLERANCE || ct > windowEnd + TIME_TOLERANCE)
                    continue;

                bool onBeat = false;

                if (beatLength > 0)
                {
                    double modBeat = ct % beatLength;
                    onBeat = modBeat <= TIME_TOLERANCE || Math.Abs(beatLength - modBeat) <= TIME_TOLERANCE;
                }

                if (onBeat)
                    continue;

                double prevTimeTry = ct - quarter;
                double nextTimeTry = ct + quarter;
                var filteredTry = new List<int>();

                foreach (int col in available)
                {
                    if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, prevTimeTry, null, TIME_TOLERANCE))
                        continue;
                    if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, nextTimeTry, null, TIME_TOLERANCE))
                        continue;
                    if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, col, ct, null, TIME_TOLERANCE))
                        continue;

                    filteredTry.Add(col);
                }

                if (filteredTry.Count > 0)
                {
                    chosenCandidate = ct;
                    available = filteredTry;
                    break;
                }

                if (chosenCandidate == null && !onBeat)
                    chosenCandidate = ct;
            }

            double candidateTime = chosenCandidate ?? baseAligned;

            // 强制只使用 1/2 或 1/3 对齐（避免出现 1/4/1/8 等更细分位置）
            double halfInterval = beatLength / 2.0;
            double thirdInterval = beatLength / 3.0;

            double snapTo(double t, double interval) => Math.Round(t / interval) * interval;
            double snappedHalf = snapTo(candidateTime, halfInterval);
            double snappedThird = snapTo(candidateTime, thirdInterval);

            bool isOnBeat(double t)
            {
                if (beatLength <= 0)
                    return false;

                double mod = t % beatLength;
                return mod <= TIME_TOLERANCE || Math.Abs(beatLength - mod) <= TIME_TOLERANCE;
            }

            bool isWithinWindow(double t) => t >= windowStart - TIME_TOLERANCE && t <= windowEnd + TIME_TOLERANCE;

            // 选择与原 candidateTime 距离更近且不在整拍上的对齐；若都不合格则不处理
            double distHalf = Math.Abs(candidateTime - snappedHalf);
            double distThird = Math.Abs(candidateTime - snappedThird);

            double? snappedCandidate = null;

            if (distHalf <= distThird)
            {
                if (!isOnBeat(snappedHalf) && isWithinWindow(snappedHalf))
                    snappedCandidate = snappedHalf;
                else if (!isOnBeat(snappedThird) && isWithinWindow(snappedThird))
                    snappedCandidate = snappedThird;
            }
            else
            {
                if (!isOnBeat(snappedThird) && isWithinWindow(snappedThird))
                    snappedCandidate = snappedThird;
                else if (!isOnBeat(snappedHalf) && isWithinWindow(snappedHalf))
                    snappedCandidate = snappedHalf;
            }

            if (snappedCandidate == null)
                return;

            candidateTime = snappedCandidate.Value;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, available.Count);

            if (avoid.Count == 0)
            {
                // 即使没有前后列限制，也要避免与同一时间已有 note 相邻，并避免彼此相邻
                var globalObjectsLocal = beatmap.HitObjects.ToList();
                var existingColsAtTimeLocal = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjectsLocal, candidateTime, TIME_TOLERANCE);

                var localCandidates0 = new List<int>(available);
                List<double>? localWeights0 = null;

                for (int i = 0; i < count && localCandidates0.Count > 0; i++)
                {
                    int pickedIndex = -1;

                    while (localCandidates0.Count > 0)
                    {
                        int idx = localWeights0 != null ? ManiaKeyPatternHelp.PickWeightedIndex(localWeights0, rng) : rng.Next(localCandidates0.Count);
                        int col = localCandidates0[idx];

                        bool adjacent = false;

                        foreach (int ec in existingColsAtTimeLocal)
                        {
                            if (Math.Abs(ec - col) <= 1)
                            {
                                adjacent = true;
                                break;
                            }
                        }

                        if (!adjacent)
                        {
                            pickedIndex = idx;
                            break;
                        }

                        localCandidates0.RemoveAt(idx);
                        localWeights0?.RemoveAt(idx);
                    }

                    if (pickedIndex < 0)
                        break;

                    int chosen = localCandidates0[pickedIndex];
                    ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { chosen }, null, rng, candidateTime);

                    for (int rem = localCandidates0.Count - 1; rem >= 0; rem--)
                    {
                        if (Math.Abs(localCandidates0[rem] - chosen) <= 1)
                        {
                            localCandidates0.RemoveAt(rem);
                            localWeights0?.RemoveAt(rem);
                        }
                    }
                }

                return;
            }

            double median = ManiaKeyPatternHelp.GetMedian(avoid);
            double maxDistance = 1;

            for (int i = 0; i < available.Count; i++)
            {
                double dist = ManiaKeyPatternHelp.DistanceToNearest(avoid, available[i]);
                if (dist > maxDistance)
                    maxDistance = dist;
            }

            var weights = new List<double>(available.Count);

            for (int i = 0; i < available.Count; i++)
            {
                int column = available[i];
                double dist = ManiaKeyPatternHelp.DistanceToNearest(avoid, column);
                double medianBias = maxDistance - Math.Abs(column - median);
                weights.Add(Math.Max(0.001, dist * 2.0 + medianBias));
            }

            // 构建 weightsForUse 对齐到 available（已经可能被替换为 filteredTry）
            var weightsForUse = weights;

            // 避免与同一时间已有 note 相邻，并避免在多次选择中产生相邻列
            var globalObjects = beatmap.HitObjects.ToList();
            var existingColsAtTime = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjects, candidateTime, TIME_TOLERANCE);

            var localCandidates = new List<int>(available);
            var localWeights = new List<double>(weightsForUse);

            for (int i = 0; i < count && localCandidates.Count > 0; i++)
            {
                int pickedIndex = -1;

                while (localCandidates.Count > 0)
                {
                    int idx = localWeights != null ? ManiaKeyPatternHelp.PickWeightedIndex(localWeights, rng) : rng.Next(localCandidates.Count);
                    int col = localCandidates[idx];

                    bool adjacentToExisting = false;

                    foreach (int ec in existingColsAtTime)
                    {
                        if (Math.Abs(ec - col) <= 1)
                        {
                            adjacentToExisting = true;
                            break;
                        }
                    }

                    if (!adjacentToExisting)
                    {
                        pickedIndex = idx;
                        break;
                    }

                    localCandidates.RemoveAt(idx);
                    localWeights?.RemoveAt(idx);
                }

                if (pickedIndex < 0)
                    break;

                int chosenCol = localCandidates[pickedIndex];
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { chosenCol }, new List<double> { 1.0 }, rng, candidateTime);

                for (int rem = localCandidates.Count - 1; rem >= 0; rem--)
                {
                    if (Math.Abs(localCandidates[rem] - chosenCol) <= 1)
                    {
                        localCandidates.RemoveAt(rem);
                        localWeights?.RemoveAt(rem);
                    }
                }
            }
        }
    }
}
