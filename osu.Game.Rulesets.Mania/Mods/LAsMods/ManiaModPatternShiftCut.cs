// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftCut : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Cut;
        protected override string PatternName => "Cut";
        protected override string PatternAcronym => "PSC";

        protected override int DefaultLevel => 4;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 2;
        protected override int DefaultWindowProcessOffset => 1;
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
            if (!ManiaKeyPatternHelp.TryGetCutJackBaseFraction(activeBeatFraction, out double baseFraction))
                return;

            double mainFraction = baseFraction;
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = Math.Max(1, beatLength * mainFraction);

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

            applyCutPattern(beatmap, windowObjects, prevColumns, nextColumns, t, minK, maxK, rng);
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

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;
            double quarter = beatLength / 4.0;

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            // 只要窗口内存在高于 1/4 的细分（非 1/1,1/2,1/3,1/4），Cut 直接不处理
            foreach (var obj in windowObjects)
            {
                bool isOn1To4 = isOnDivisionLocal(obj.StartTime, 1)
                                || isOnDivisionLocal(obj.StartTime, 2)
                                || isOnDivisionLocal(obj.StartTime, 3)
                                || isOnDivisionLocal(obj.StartTime, 4);
                if (!isOn1To4)
                    return;
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

            // 对于已经位于 1/4 网格的 note：若其前后 quarter 时间在该列存在 note，则尝试横向移动到最近的无冲突列。

            foreach (var obj in windowObjects)
            {
                // 只处理位于 1/4 网格的 note（在本函数里 windowObjects 都是粗网格）
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
                double ct = baseAligned + (off * targetInterval);
                // 保证在窗口范围内（加上小容差）
                if (ct < windowStart - TIME_TOLERANCE || ct > windowEnd + TIME_TOLERANCE)
                    continue;

                // 避免整拍位置
                bool onBeat = false;

                if (beatLength > 0)
                {
                    double modBeat = ct % beatLength;
                    onBeat = modBeat <= TIME_TOLERANCE || Math.Abs(beatLength - modBeat) <= TIME_TOLERANCE;
                }

                if (onBeat)
                    continue;

                // 预筛列：检查前后 quarter 是否为空
                var filteredTry = new List<int>();
                double prevTimeTry = ct - quarter;
                double nextTimeTry = ct + quarter;

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
                    break;
                }

                // 否则记住第一个非整拍对齐点作为备选
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

                for (int i = 0; i < count && localCandidates0.Count > 0; i++)
                {
                    int pickedIndex = rng.Next(localCandidates0.Count);
                    int col = localCandidates0[pickedIndex];

                    bool adjacent = false;

                    foreach (int ec in existingColsAtTimeLocal)
                    {
                        if (Math.Abs(ec - col) <= 1)
                        {
                            adjacent = true;
                            break;
                        }
                    }

                    if (adjacent)
                    {
                        localCandidates0.RemoveAt(pickedIndex);
                        continue;
                    }

                    ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { col }, null, rng, candidateTime);

                    // 移除已选列及其邻列
                    for (int rem = localCandidates0.Count - 1; rem >= 0; rem--)
                    {
                        if (Math.Abs(localCandidates0[rem] - col) <= 1)
                            localCandidates0.RemoveAt(rem);
                    }
                }

                return;
            }

            double median = ManiaKeyPatternHelp.GetMedianFromList(available);
            var weights = new List<double>(available.Count);

            for (int i = 0; i < available.Count; i++)
            {
                int column = available[i];
                weights.Add(Math.Max(0.001, Math.Abs(column - median)));
            }

            // 过滤出在 candidateTime 前后 quarter 无 note 的列
            var filtered = new List<int>();

            foreach (int col in available)
            {
                // 前后1/4时间点
                double prevTime = candidateTime - quarter;
                double nextTime = candidateTime + quarter;

                if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, prevTime, null, TIME_TOLERANCE))
                    continue;

                if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, nextTime, null, TIME_TOLERANCE))
                    continue;

                // 避免 hold 占用
                if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, col, candidateTime, null, TIME_TOLERANCE))
                    continue;

                filtered.Add(col);
            }

            var useCandidates = filtered.Count > 0 ? filtered : available;

            // 如果使用了 weights 且 useCandidates 是 available 的子集，需要为 useCandidates 构建对应的 weights 列表
            List<double>? weightsForUse;

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

            // 避免与同一时间已有 note 相邻，并避免在多次选择中产生相邻列
            var globalObjects = beatmap.HitObjects.ToList();
            var existingColsAtTime = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjects, candidateTime, TIME_TOLERANCE);

            var localCandidates = new List<int>(useCandidates);
            var localWeights = new List<double>(weightsForUse);

            for (int i = 0; i < count && localCandidates.Count > 0; i++)
            {
                int pickedIndex = -1;

                // 用权重或随机选择，若选中列与已有列相邻则移除并重试
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

                    // 移除当前候选及其权重，继续尝试
                    localCandidates.RemoveAt(idx);
                    localWeights?.RemoveAt(idx);
                }

                if (pickedIndex < 0)
                    break;

                int chosenCol = localCandidates[pickedIndex];
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { chosenCol }, new List<double> { 1.0 }, rng, candidateTime);

                // 移除已选列及其邻列，防止后续选到相邻列
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
