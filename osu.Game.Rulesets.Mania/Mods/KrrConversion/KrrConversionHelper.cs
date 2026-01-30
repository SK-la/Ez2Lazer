// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public class KrrConversionHelper
    {
        public static readonly double[] BEAT_SPEED_VALUES = { 0.125, 0.25, 0.5, 0.75, 1, 2, 3, 4, 999 };

        public static double ComputeConvertTime(int beatSpeedIndex, double bpm)
        {
            double speed = BEAT_SPEED_VALUES[Math.Clamp(beatSpeedIndex, 0, BEAT_SPEED_VALUES.Length - 1)];
            return Math.Max(1, speed * 60000 / bpm * 4 - 10);
        }

        public static int ComputeSeedFromBeatmap(ManiaBeatmap beatmap)
        {
            try
            {
                int val = beatmap.HitObjects.Count ^ beatmap.TotalColumns;
                return Math.Abs(val) + 1;
            }
            catch
            {
                return 114514;
            }
        }

        public static int InferOriginalKeys(ManiaBeatmap beatmap, int fallback)
        {
            double cs = beatmap.BeatmapInfo.Difficulty.CircleSize;
            if (cs > 0)
                return Math.Max(1, (int)Math.Round(cs));

            return fallback;
        }

        public static ManiaHitObject CloneWithColumn(ManiaHitObject src, int col)
        {
            if (src is HoldNote hold)
            {
                var clone = new HoldNote
                {
                    StartTime = hold.StartTime,
                    EndTime = hold.EndTime,
                    Column = col,
                    Samples = hold.Samples.ToList(),
                    PlaySlidingSamples = hold.PlaySlidingSamples,
                };

                if (hold.NodeSamples != null)
                {
                    clone.NodeSamples = hold.NodeSamples
                                            .Select(list => (IList<HitSampleInfo>)list.ToList())
                                            .ToList();
                }

                return clone;
            }

            return new Note
            {
                StartTime = src.StartTime,
                Column = col,
                Samples = src.Samples.ToList()
            };
        }

        // ---------------- 通用 LN/DP/N2N 方法 ----------------

        // Beta 分布随机数生成
        public static int GenerateRandom(double dBar, double uBar, double mBar, int pBar, Random r, Oscillator? osc = null)
        {
            if (pBar <= 0) return (int)mBar;

            if (pBar >= 100) pBar = 100;

            double p = pBar / 100.0;
            double d = mBar - (mBar - dBar) * p;
            double u = mBar + (uBar - mBar) * p;

            d = Math.Max(d, dBar);
            u = Math.Min(u, uBar);

            if (d >= u) return (int)mBar;

            double betaRandom = (r.NextDouble() + r.NextDouble() + r.NextDouble() + r.NextDouble() + r.NextDouble()) / 5.0;

            if (osc != null)
            {
                double bias = (osc.Next() - 0.5) * 0.04;
                betaRandom = Math.Clamp(betaRandom + bias, 0.0, 1.0);
            }

            double range = u - d;
            double mRelative = (mBar - d) / range;
            double result = betaRandom <= 0.5
                ? d + mRelative * (betaRandom / 0.5) * range
                : d + (mRelative + (1 - mRelative) * ((betaRandom - 0.5) / 0.5)) * range;

            return (int)Math.Round(result);
        }

        // 百分比筛选
        public static List<(int index, double start, int length)> MarkByPercentagePerGroup(List<(int index, double start, int length)> list, double percentage, Random r, Oscillator osc)
        {
            if (percentage >= 100) return list;
            if (percentage <= 0) return new List<(int, double, int)>();

            var grouped = list.GroupBy(x => Math.Round(x.start, 3)).ToList();
            var outList = new List<(int index, double start, int length)>();

            foreach (var g in grouped)
            {
                var items = g.ToList();
                int keep = (int)Math.Round(items.Count * (percentage / 100.0));
                keep = Math.Max(0, Math.Min(keep, items.Count));

                if (keep == items.Count) outList.AddRange(items);
                else if (keep > 0)
                {
                    var scored = items.Select(it => new { it.index, it.start, it.length, score = r.NextDouble() * 0.85 + osc.Next() * 0.15 })
                                      .OrderByDescending(x => x.score).Take(keep)
                                      .Select(x => (x.index, x.start, x.length));
                    outList.AddRange(scored);
                }
            }

            return outList;
        }

        // 对齐方法
        public static void ApplyAlignmentToCandidates(List<(int index, double start, int length)> list, ManiaBeatmap beatmap, int alignmentIndex)
        {
            var alignMap = new Dictionary<int, double>
            {
                { 1, 1.0 / 8 },
                { 2, 1.0 / 7 },
                { 3, 1.0 / 6 },
                { 4, 1.0 / 5 },
                { 5, 1.0 / 4 },
                { 6, 1.0 / 3 },
                { 7, 1.0 / 2 },
                { 8, 1.0 }
            };

            if (!alignMap.TryGetValue(alignmentIndex, out double alignValue)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var ho = beatmap.HitObjects[item.index];
                double beatLen = beatmap.ControlPointInfo.TimingPointAt(ho.StartTime).BeatLength;
                double denom = beatLen * alignValue;
                if (denom <= 0.5) continue;

                int aligned = (int)(Math.Round(item.length / denom) * denom);
                if (aligned < 30) aligned = item.length;
                list[i] = (item.index, item.start, aligned);
            }
        }

        /// <summary>
        /// 通用的最终冲突解决方法。
        /// 保留原分布优先：优先使用对象已有列；若无法放置，尝试其他空闲列；
        /// 若仍无法放置，则截断占位的 HoldNote（或在必要时将其转换为短按），使出现场景留下至少 <paramref name="minGapMs"/> 的间隔；
        /// 若无法修改占位 Hold，则会把当前对象（若为 Hold）降级为短按。
        /// </summary>
        public static List<ManiaHitObject> ResolveFinalConflicts(List<ManiaHitObject> objects, int targetKeys, double minGapMs, Random rng)
        {
            var ordered = objects.OrderBy(o => o.StartTime).ToList();
            var result = new List<ManiaHitObject>();

            double[] occupiedUntil = new double[targetKeys];
            int[] occupyingIndex = new int[targetKeys];

            for (int i = 0; i < targetKeys; i++)
            {
                occupiedUntil[i] = double.NegativeInfinity;
                occupyingIndex[i] = -1;
            }

            const double min_hold_ms = 30.0; // 最小保留的长按长度

            for (int i = 0; i < ordered.Count; i++)
            {
                var obj = ordered[i];

                int col = Math.Clamp(obj.Column, 0, targetKeys - 1);

                // 如果列当前空闲，直接放置（保留列）
                if (occupiedUntil[col] <= obj.StartTime)
                {
                    var placed = CloneWithColumn(obj, col);
                    // 若是 Hold，更新占用时间
                    if (placed is HoldNote ph)
                        occupiedUntil[col] = Math.Max(occupiedUntil[col], ph.EndTime);
                    else
                        occupiedUntil[col] = Math.Max(occupiedUntil[col], placed.StartTime);

                    occupyingIndex[col] = result.Count;
                    result.Add(placed);
                    continue;
                }

                // 列被占用：尝试截断占位的 Hold 来腾出空间
                int occIdx = occupyingIndex[col];
                bool freed = false;

                if (occIdx >= 0 && occIdx < result.Count && result[occIdx] is HoldNote occHold && occHold.EndTime > obj.StartTime)
                {
                    double newEnd = obj.StartTime - minGapMs;

                    if (newEnd <= occHold.StartTime + min_hold_ms)
                    {
                        // 截断后太短，转换为短按
                        var note = new Note { StartTime = occHold.StartTime, Column = col, Samples = occHold.Samples.ToList() };
                        result[occIdx] = note;
                        occupiedUntil[col] = note.StartTime;
                    }
                    else
                    {
                        occHold.EndTime = newEnd;
                        result[occIdx] = occHold;
                        occupiedUntil[col] = newEnd;
                    }

                    // 检查是否已腾出足够空间
                    if (occupiedUntil[col] <= obj.StartTime - minGapMs)
                    {
                        var placed = CloneWithColumn(obj, col);
                        // 如果 obj 是 Hold 但与占用仍有冲突，降级为 Note
                        if (placed is HoldNote ph2 && occupiedUntil[col] > placed.StartTime)
                            placed = new Note { StartTime = ph2.StartTime, Column = col, Samples = ph2.Samples.ToList() };

                        occupyingIndex[col] = result.Count;
                        if (placed is HoldNote ph3)
                            occupiedUntil[col] = Math.Max(occupiedUntil[col], ph3.EndTime);
                        else
                            occupiedUntil[col] = Math.Max(occupiedUntil[col], placed.StartTime);

                        result.Add(placed);
                        freed = true;
                    }
                }

                if (!freed)
                {
                    // 无法腾出空间：若当前对象为 Hold，则降级为 Note 并尝试放置（可能仍有冲突，最终由后续规则处理）
                    if (obj is HoldNote hobj)
                    {
                        var note = new Note { StartTime = hobj.StartTime, Column = col, Samples = hobj.Samples.ToList() };
                        // 即使与前一个占用冲突，也加入（后续 EnforceMinimumGaps 会清理）
                        occupyingIndex[col] = result.Count;
                        occupiedUntil[col] = Math.Max(occupiedUntil[col], note.StartTime);
                        result.Add(note);
                    }
                    else
                    {
                        // 对于普通 Note，直接加入（后续清理会处理过近的间隙）
                        occupyingIndex[col] = result.Count;
                        occupiedUntil[col] = Math.Max(occupiedUntil[col], obj.StartTime);
                        result.Add(CloneWithColumn(obj, col));
                    }
                }
            }

            return result.OrderBy(o => o.StartTime).ToList();
        }

        /// <summary>
        /// 在同一列中强制最小间隙：如果两个对象间隙小于 <paramref name="minGapMs"/>，则按规则删除/降级。
        /// 规则：
        /// - 若相邻两音符间隙小于阈值，删除后者；
        /// - 若三连中中间音符删除后能使两端间隙满足阈值，则删除中间音符；
        /// - Hold 的结束时间用于计算前后间隙；当需要时，会将过短的 Hold 降级为短按。
        /// </summary>
        public static List<ManiaHitObject> EnforceMinimumGaps(List<ManiaHitObject> objects, int targetKeys, double minGapMs)
        {
            var byColumn = objects.GroupBy(o => Math.Clamp(o.Column, 0, targetKeys - 1));
            var survivors = new List<ManiaHitObject>();

            foreach (var colGroup in byColumn)
            {
                var list = colGroup.OrderBy(o => o.StartTime).ToList();
                if (list.Count == 0) continue;

                int idx = 0;
                // keep the first one always
                var kept = new List<ManiaHitObject> { list[0] };

                while (++idx < list.Count)
                {
                    var prev = kept.Last();
                    var cur = list[idx];

                    double prevEnd = prev is HoldNote ph ? ph.EndTime : prev.StartTime;
                    double gap = cur.StartTime - prevEnd;

                    if (gap >= minGapMs)
                    {
                        kept.Add(cur);
                        continue;
                    }

                    // gap is too small
                    // try lookahead: if next exists and next.Start - prevEnd >= minGapMs, drop cur (middle)
                    if (idx + 1 < list.Count)
                    {
                        var next = list[idx + 1];
                        double nextGap = next.StartTime - prevEnd;

                        if (nextGap >= minGapMs)
                        {
                            // drop cur, move to next (do not add cur)
                            idx++; // advance to next in outer loop
                            kept.Add(next);
                            continue;
                        }
                    }

                    // otherwise drop cur (the later one). If cur is Hold and prevEnd < cur.EndTime, consider downgrading cur to Note
                    if (cur is HoldNote hcur)
                    {
                        // if hold length would become too short relative to its own length when pushed, downgrade
                        double holdLen = hcur.EndTime - hcur.StartTime;

                        if (holdLen < minGapMs)
                        {
                            var note = new Note { StartTime = hcur.StartTime, Column = hcur.Column, Samples = hcur.Samples.ToList() };
                            // check gap between prevEnd and this note
                            double noteGap = note.StartTime - prevEnd;

                            if (noteGap >= minGapMs)
                            {
                                kept.Add(note);
                            }
                        }
                    }

                    // default: drop cur (do nothing)
                }

                survivors.AddRange(kept);
            }

            // include objects that are in columns with no grouping (shouldn't happen) and preserve ordering
            return survivors.OrderBy(o => o.StartTime).ThenBy(o => o.Column).ToList();
        }

        /// <summary>
        /// 检查在集合中是否已存在与给定列与时间相近的音符（用于避免重复添加）。
        /// </summary>
        public static bool ExistsAt(IEnumerable<ManiaHitObject> objects, int column, double startTime, double epsilonMs = 1.0)
        {
            int col = Math.Clamp(column, 0, int.MaxValue);

            foreach (var o in objects)
            {
                if (o.Column != col) continue;

                if (Math.Abs(o.StartTime - startTime) <= epsilonMs) return true;
            }

            return false;
        }
    }
}
