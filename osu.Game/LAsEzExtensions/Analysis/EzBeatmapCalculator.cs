// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public class EzBeatmapCalculator
    {
        public static (double averageKps, double maxKps, List<double> kpsList) GetKps(IBeatmap beatmap)
        {
            var kpsList = new List<double>();
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, kpsList);

            double interval = 4 * 60000 / beatmap.BeatmapInfo.BPM;
            double songEndTime = hitObjects[^1].StartTime;
            const double start_time = 0;

            // 预处理HitObjects按StartTime排序（假设原列表已排序，可省略）
            // 使用二分查找优化区间查询
            for (double currentTime = start_time; currentTime < songEndTime; currentTime += interval)
            {
                double endTime = currentTime + interval;
                int startIdx = findFirstIndexGreaterOrEqual(hitObjects, currentTime);
                int endIdx = findFirstIndexGreaterOrEqual(hitObjects, endTime);
                int hits = endIdx - startIdx;

                kpsList.Add(hits / (interval / 1000));
            }

            if (kpsList.Count == 0)
                return (0, 0, kpsList);

            return (kpsList.Average(), kpsList.Max(), kpsList);
        }

        private static int findFirstIndexGreaterOrEqual(IReadOnlyList<HitObject> hitObjects, double targetTime)
        {
            int low = 0, high = hitObjects.Count;

            while (low < high)
            {
                int mid = (low + high) / 2;
                if (hitObjects[mid].StartTime < targetTime)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        public static Dictionary<int, int> GetColumnNoteCounts(IBeatmap beatmap)
        {
            var counts = new Dictionary<int, int>();

            foreach (var obj in beatmap.HitObjects.OfType<IHasColumn>())
            {
                if (obj is IHasDuration) continue;

                counts[obj.Column] = counts.TryGetValue(obj.Column, out int c) ? c + 1 : 1;
            }

            return counts;
        }

        public static string GetScratch(IBeatmap beatmap, int keyCount)
        {
            // 旧路径：直接从 IBeatmap 现算。
            // 注意：选歌面板快速滚动时这个路径非常重（会再次算 KPS/列统计），
            // 新代码优先走 GetScratchFromPrecomputed()。
            var columnCounts = GetColumnNoteCounts(beatmap);
            var (_, maxKps, kpsList) = GetKps(beatmap);
            return GetScratchFromPrecomputed(columnCounts, maxKps, kpsList, keyCount);
        }

        /// <summary>
        /// 复用外部已经计算好的列统计与 KPS 数据，生成 Scratch 标签。
        /// 用于选歌面板：避免重复遍历 HitObjects / 重复计算 KPS。
        /// </summary>
        // TODO: 计算比较粗糙，后续可优化。
        public static string GetScratchFromPrecomputed(Dictionary<int, int> columnCounts, double maxKps, List<double> kpsList, int keyCount)
        {
            if (keyCount <= 0)
                return "[?K]";

            // 将列统计映射为固定长度数组，方便计算 empty 列。
            int[] countsByColumn = new int[keyCount];

            foreach (var (column, count) in columnCounts)
            {
                if ((uint)column < (uint)keyCount)
                    countsByColumn[column] = count;
            }

            int totalNotes = countsByColumn.Sum();
            if (totalNotes == 0)
                return $"[{keyCount}K]";

            int firstCount = countsByColumn[0];
            int lastCount = countsByColumn[^1];

            bool isFirstHigh = checkHighSpeed(maxKps, kpsList);
            bool isLastHigh = isFirstHigh;

            // 去掉两侧列，计算“中间列”平均/最大。
            var middleCounts = keyCount > 2 ? countsByColumn.Skip(1).Take(keyCount - 2).ToArray() : Array.Empty<int>();
            double averageNotes = middleCounts.Length > 0 ? middleCounts.Average() : 0;
            int maxNotesInMiddle = middleCounts.Length > 0 ? middleCounts.Max() : 0;

            bool isFirstColumnLow = averageNotes > 0 && (firstCount < averageNotes * 0.3 || (maxNotesInMiddle > 0 && firstCount < maxNotesInMiddle / 3.0));
            bool isLastColumnLow = averageNotes > 0 && (lastCount < averageNotes * 0.3 || (maxNotesInMiddle > 0 && lastCount < maxNotesInMiddle / 3.0));

            string result = $"[{keyCount}K]";

            if (keyCount == 6 || keyCount == 8)
            {
                if (isFirstHigh || isLastHigh)
                    result = $"[{keyCount - 1}K1S]";
                else if (isFirstColumnLow || isLastColumnLow)
                    result = $"[{keyCount - 1}+1K]";
            }
            else if (keyCount >= 7)
            {
                if (isFirstHigh || isLastHigh)
                    result = $"[{keyCount - 2}K2S]";
                else if (isFirstColumnLow || isLastColumnLow)
                    result = $"[{keyCount - 2}+2K]";
            }

            int emptyColumns = countsByColumn.Count(c => c == 0);
            if (emptyColumns > 0)
                result = $"[{keyCount - 1}K_{emptyColumns}Empty]";

            return result;
        }

        private static bool checkHighSpeed(double maxKps, List<double> kpsList)
        {
            double threshold = maxKps / 4;

            foreach (double kps in kpsList)
            {
                if (kps > threshold)
                    return true;
            }

            return false;
        }
    }
}
