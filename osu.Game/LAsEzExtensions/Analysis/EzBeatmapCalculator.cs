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

        /// <summary>
        /// 复用外部已经计算好的 列统计与 KPS 数据，生成 Scratch 标签。
        /// 用于选歌面板：避免重复遍历 HitObjects / 重复计算 KPS。
        /// keyCount 从 columnCounts 推断。
        /// </summary>
        public static string GetScratchFromPrecomputed(Dictionary<int, int> columnCounts, double maxKps, List<double> kpsList)
        {
            if (columnCounts.Count == 0)
                return "[?K] ";

            // 从 columnCounts 的最大 key + 1 推断列数
            int keyCount = 0;

            foreach (int k in columnCounts.Keys)
            {
                if (k >= keyCount)
                    keyCount = k + 1;
            }

            if (maxKps == 0) return $"[{keyCount}K] ";

            // 将列统计映射为固定长度数组，方便计算 empty 列。
            int[] countsByColumn = new int[keyCount];

            foreach (var (column, count) in columnCounts)
            {
                if ((uint)column < (uint)keyCount)
                    countsByColumn[column] = count;
            }

            var (isFirstLow, isFirstHigh, isLastLow, isLastHigh) = checkNotes(countsByColumn, keyCount);

            string result = $"[{keyCount}K] ";

            if (keyCount >= 6)
            {
                if (isFirstHigh || isLastHigh)
                    result = $"[{keyCount - 1}K1S] ";

                if (isFirstLow || isLastLow)
                    result = $"[{keyCount - 1}+1K] ";

                if (isFirstHigh && isLastHigh)
                    result = $"[{keyCount - 2}K2S] ";

                if (isFirstLow && isLastLow)
                    result = $"[{keyCount - 2}+2K] ";
            }

            int emptyColumns = countsByColumn.Count(c => c == 0);
            if (emptyColumns > 0)
                result = $"[{keyCount - emptyColumns}K_{emptyColumns}[]] ";

            return result;
        }

        private static (bool isFirstLow, bool isFirstHigh, bool isLastLow, bool isLastHigh) checkNotes(int[] countsByColumn, int keyCount)
        {
            bool isFirstLow = false;
            bool isFirstHigh = false;
            bool isLastLow = false;
            bool isLastHigh = false;

            if (keyCount >= 2)
            {
                int firstCount = countsByColumn[0];
                int secondCount = countsByColumn[1];
                isFirstLow = (firstCount > 0 && firstCount < secondCount / 2.0);
                isFirstHigh = firstCount > secondCount * 2;

                int lastCount = countsByColumn[^1];
                int secondLastCount = countsByColumn[^2];
                isLastLow = (lastCount > 0 && lastCount < secondLastCount / 2.0);
                isLastHigh = lastCount > secondLastCount * 2;
            }

            return (isFirstLow, isFirstHigh, isLastLow, isLastHigh);
        }

        // private static bool checkHighSpeed(double maxKps, List<double> kpsList)
        // {
        //     double threshold = maxKps / 4;

        //     foreach (double kps in kpsList)
        //     {
        //         if (kps > threshold)
        //             return true;
        //     }

        //     return false;
        // }
    }
}
