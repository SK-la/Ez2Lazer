// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
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

            // 去除重叠并降低密度
            CleanOverlapNotes(beatmap);
            // Enforce hold-release gap and convert too-short holds to notes.
            EnforceHoldReleaseGap(beatmap);
            // 调整/转换可能再次引入过小间距，做一次最终兜底。
            EnforceMinimumGaps(beatmap);
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
            return getMinimumGapAtTime(beatmap, startTime, beat);
        }

        private static double getMinimumGapAtTime(ManiaBeatmap beatmap, double time, int beat = 8)
        {
            int safeBeatDivisor = Math.Max(1, beat);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;

            if (beatLength <= 0)
                return 30;

            double beatGap = beatLength / safeBeatDivisor;

            // 保留“按节拍”判定，同时使用 30ms 作为保守下限，避免高速段阈值过小。
            return Math.Max(30, beatGap);
        }

        /// <summary>
        /// 中位去除高速note，长按
        /// </summary>
        /// <param name="beatmap"></param>
        internal static void EnforceMinimumGaps(ManiaBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            int targetKeys = beatmap.TotalColumns;

            var byColumn = beatmap.HitObjects.ToList().GroupBy(o => Math.Clamp(o.Column, 0, Math.Max(0, targetKeys - 1)));
            var survivors = new List<ManiaHitObject>();

            foreach (var colGroup in byColumn)
            {
                var list = colGroup.OrderBy(o => o.StartTime).ToList();
                if (list.Count == 0)
                    continue;

                var kept = new List<ManiaHitObject> { list[0] };

                for (int i = 1; i < list.Count; i++)
                {
                    var prev = kept.Last();
                    var current = list[i];

                    double prevEnd = prev is HoldNote prevHold ? prevHold.EndTime : prev.StartTime;
                    double requiredGap = getMinimumGapAtTime(beatmap, prevEnd);
                    double actualGap = current.StartTime - prevEnd;

                    if (actualGap < requiredGap)
                        continue;

                    kept.Add(current);
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
                    double minGapMs = getMinimumGapAtTime(beatmap, next.StartTime, beat);
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
