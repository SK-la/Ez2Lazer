using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.LAsEzExtensions
{
    public static class OptimizedBeatmapCalculator
    {
        /// <summary>
        /// 高性能KPS计算，结合了缓存和优化的算法
        /// </summary>
        public static (double averageKps, double maxKps, List<double> kpsList) GetKpsOptimized(IBeatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, new List<double>());

            // 使用更高效的interval计算
            double bpm = beatmap.BeatmapInfo.BPM;
            double interval = 240000.0 / bpm; // 4拍的时间间隔（毫秒）
            double songEndTime = hitObjects[^1].StartTime;

            // 预分配List容量以避免频繁扩容
            int estimatedIntervals = (int)((songEndTime / interval) + 1);
            var kpsList = new List<double>(estimatedIntervals);

            // 缓存hitObjects数组以提高访问性能
            var hitObjectsArray = hitObjects as HitObject[] ?? hitObjects.ToArray();

            double currentTime = 0;
            int currentIndex = 0;

            while (currentTime < songEndTime)
            {
                double endTime = currentTime + interval;

                // 优化的区间计算：从上一个位置开始搜索
                int startIdx = currentIndex;
                while (startIdx < hitObjectsArray.Length && hitObjectsArray[startIdx].StartTime < currentTime)
                    startIdx++;

                int endIdx = startIdx;
                while (endIdx < hitObjectsArray.Length && hitObjectsArray[endIdx].StartTime < endTime)
                    endIdx++;

                int hits = endIdx - startIdx;
                double kps = hits / (interval / 1000.0); // 转换为每秒
                kpsList.Add(kps);

                currentTime += interval;
                currentIndex = startIdx; // 下次从这个位置开始
            }

            if (kpsList.Count == 0)
                return (0, 0, kpsList);

            // 使用LINQ的高性能版本
            double average = kpsList.Sum() / kpsList.Count;
            double max = kpsList.Max();

            return (average, max, kpsList);
        }

        /// <summary>
        /// 高性能列音符计数，使用数组代替Dictionary以提高性能
        /// </summary>
        public static Dictionary<int, int> GetColumnNoteCountsOptimized(IBeatmap beatmap)
        {
            // 预过滤非Duration类型的Column对象
            var columnObjects = new List<IHasColumn>(beatmap.HitObjects.Count);

            foreach (var obj in beatmap.HitObjects)
            {
                if (obj is IHasColumn columnObj && !(obj is IHasDuration))
                {
                    columnObjects.Add(columnObj);
                }
            }

            if (columnObjects.Count == 0)
                return new Dictionary<int, int>();

            // 找出列的范围以使用数组优化
            int minColumn = columnObjects[0].Column;
            int maxColumn = minColumn;

            for (int i = 1; i < columnObjects.Count; i++)
            {
                int column = columnObjects[i].Column;
                if (column < minColumn) minColumn = column;
                if (column > maxColumn) maxColumn = column;
            }

            int columnRange = maxColumn - minColumn + 1;

            // 如果列范围较小，使用数组；否则使用Dictionary
            if (columnRange <= 32) // 合理的阈值
            {
                int[] countsArray = new int[columnRange];

                foreach (var obj in columnObjects)
                {
                    countsArray[obj.Column - minColumn]++;
                }

                var result = new Dictionary<int, int>();

                for (int i = 0; i < columnRange; i++)
                {
                    if (countsArray[i] > 0)
                    {
                        result[i + minColumn] = countsArray[i];
                    }
                }

                return result;
            }
            else
            {
                // 列范围太大，使用Dictionary
                var counts = new Dictionary<int, int>();

                foreach (var obj in columnObjects)
                {
                    counts[obj.Column] = counts.GetValueOrDefault(obj.Column) + 1;
                }

                return counts;
            }
        }

        /// <summary>
        /// 一次性计算所有需要的数据，避免重复遍历
        /// </summary>
        public static (
            double averageKps,
            double maxKps,
            List<double> kpsList,
            Dictionary<int, int> columnCounts
            ) GetAllDataOptimized(IBeatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, new List<double>(), new Dictionary<int, int>());

            // 一次遍历完成KPS和列统计
            double bpm = beatmap.BeatmapInfo.BPM;
            double interval = 240000.0 / bpm;
            double songEndTime = hitObjects[^1].StartTime;

            int estimatedIntervals = (int)((songEndTime / interval) + 1);
            var kpsList = new List<double>(estimatedIntervals);
            var columnCounts = new Dictionary<int, int>();

            // 同时处理KPS和列统计
            var hitObjectsArray = hitObjects as HitObject[] ?? hitObjects.ToArray();

            // 预处理列统计
            foreach (var obj in hitObjectsArray)
            {
                if (obj is IHasColumn columnObj && !(obj is IHasDuration))
                {
                    columnCounts[columnObj.Column] = columnCounts.GetValueOrDefault(columnObj.Column) + 1;
                }
            }

            // KPS计算
            double currentTime = 0;
            int currentIndex = 0;

            while (currentTime < songEndTime)
            {
                double endTime = currentTime + interval;

                int startIdx = currentIndex;
                while (startIdx < hitObjectsArray.Length && hitObjectsArray[startIdx].StartTime < currentTime)
                    startIdx++;

                int endIdx = startIdx;
                while (endIdx < hitObjectsArray.Length && hitObjectsArray[endIdx].StartTime < endTime)
                    endIdx++;

                int hits = endIdx - startIdx;
                kpsList.Add(hits / (interval / 1000.0));

                currentTime += interval;
                currentIndex = startIdx;
            }

            if (kpsList.Count == 0)
                return (0, 0, kpsList, columnCounts);

            double average = kpsList.Sum() / kpsList.Count;
            double max = kpsList.Max();

            return (average, max, kpsList, columnCounts);
        }
    }
}
