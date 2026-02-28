// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.LAsMods
{
    public static class ManiaNoteCleanupTool
    {
        /// <summary>
        /// 统一格式化铺面，去除重叠、高分等无法正常游玩的内容。
        /// <para></para>缝隙检测为 30ms ~ 1/8 beatLength
        /// </summary>
        /// <param name="beatmap"></param>
        /// <param name="seed"></param>
        public static void CleanupBeatmap(ManiaBeatmap beatmap, int? seed = null)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            // 1) 先解决列冲突（考虑长按）
            // if (beatmap.TotalColumns > 0)
            // {
            //     int usedSeed = seed ?? KrrConversionHelper.ComputeSeedFromBeatmap(beatmap);
            //     var rng = new Random(usedSeed);
            //     var resolved = KrrConversionHelper.ResolveFinalConflicts(beatmap.HitObjects.ToList(), beatmap.TotalColumns, gap, rng);
            //     beatmap.HitObjects.Clear();
            //     beatmap.HitObjects.AddRange(resolved);
            // }

            // 2) 去除重叠并降低密度
            CleanOverlapNotes(beatmap);
            // 3) 在同一列中强制最小间隙
            EnforceMinimumGaps(beatmap);
            // 4) Enforce hold-release gap and convert too-short holds to notes.
            EnforceHoldReleaseGap(beatmap);
        }

        /// <summary>
        /// 清理重叠note，重叠LN
        /// </summary>
        /// <param name="beatmap"></param>
        internal static void CleanOverlapNotes(ManiaBeatmap beatmap)
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

        internal static double GetMinimumGapMs(ManiaBeatmap beatmap, int beat = 8)
        {
            if (beatmap.HitObjects.Count == 0)
                return 0;

            double startTime = beatmap.HitObjects.Min(h => h.StartTime);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(startTime).BeatLength;
            double gap = beatLength / beat;
            return Math.Clamp(gap, 30, beatLength / beat * 2);
        }

        /// <summary>
        /// 中位去除高速note，长按
        /// </summary>
        /// <param name="beatmap"></param>
        internal static void EnforceMinimumGaps(ManiaBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            double minGapMs = GetMinimumGapMs(beatmap);
            int targetKeys = beatmap.TotalColumns;

            var byColumn = beatmap.HitObjects.ToList().GroupBy(o => Math.Clamp(o.Column, 0, Math.Max(0, targetKeys - 1)));
            var survivors = new List<ManiaHitObject>();

            foreach (var colGroup in byColumn)
            {
                var list = colGroup.OrderBy(o => o.StartTime).ToList();
                if (list.Count == 0) continue;

                // 第一步：预先移除极度密集的音符（> 1/8 拍）作为快速预处理
                var denseFiltered = new List<ManiaHitObject> { list[0] };

                for (int i = 1; i < list.Count; i++)
                {
                    var prev = denseFiltered.Last();
                    var curr = list[i];

                    if (curr.StartTime - prev.StartTime < minGapMs)
                        continue; // 丢弃当前音符（过于密集）

                    denseFiltered.Add(curr);
                }

                // 第二步：应用最小间隙规则，包含向前查看的三连处理
                var kept = new List<ManiaHitObject> { denseFiltered[0] };
                int idx = 0;

                while (++idx < denseFiltered.Count)
                {
                    var prev = kept.Last();
                    var cur = denseFiltered[idx];

                    double prevEnd = prev is HoldNote ph ? ph.EndTime : prev.StartTime;
                    double gap = cur.StartTime - prevEnd;

                    if (gap >= minGapMs)
                    {
                        kept.Add(cur);
                        continue;
                    }

                    // 间隙过小：尝试向前查看并删除中间音符
                    if (idx + 1 < denseFiltered.Count)
                    {
                        var next = denseFiltered[idx + 1];
                        double nextGap = next.StartTime - prevEnd;

                        if (nextGap >= minGapMs)
                        {
                            // 删除当前（中间）音符，前进到下一个
                            idx++; // will move to next in outer loop
                            kept.Add(next);
                        }
                    }
                }

                survivors.AddRange(kept);
            }

            // 用筛选后的音符替换地图中的对象，并保留顺序
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(survivors.OrderBy(o => o.StartTime).ThenBy(o => o.Column));
        }

        /// <summary>
        /// 截断过短的 反键缝隙，默认最大允许 1/8 beat
        /// <para>面尾缩短避让下一个note</para>面过短则降低为米
        /// </summary>
        /// <param name="beatmap"></param>
        /// <param name="beat"></param>
        internal static void EnforceHoldReleaseGap(ManiaBeatmap beatmap, int beat = 8)
        {
            double minGapMs = GetMinimumGapMs(beatmap, beat);
            if (beatmap.HitObjects.Count == 0)
                return;

            var groups = beatmap.HitObjects.GroupBy(o => o.Column).ToList();

            foreach (var group in groups)
            {
                var list = group.OrderBy(o => o.StartTime).ToList();

                for (int i = 0; i < list.Count - 1; i++)
                {
                    if (list[i] is not HoldNote hold)
                        continue;

                    var next = list[i + 1];
                    double gap = next.StartTime - hold.EndTime;

                    if (gap >= minGapMs)
                        continue;

                    // 反键缝隙微调
                    double newEnd = next.StartTime - minGapMs;

                    if (newEnd <= hold.EndTime)
                    {
                        hold.EndTime = newEnd;
                    }

                    // 长按过短就转米
                    if (hold.EndTime - hold.StartTime < minGapMs)
                    {
                        var note = new Note { StartTime = hold.StartTime, Column = hold.Column, Samples = hold.Samples.ToList() };
                        beatmap.HitObjects.Remove(hold);
                        beatmap.HitObjects.Add(note);
                        list[i] = note;
                    }
                }
            }
        }
    }
}
