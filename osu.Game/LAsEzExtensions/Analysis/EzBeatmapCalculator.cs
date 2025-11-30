// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
            // 预处理：获取列分组数据和KPS数据
            var columnGroups = beatmap.HitObjects.OfType<IHasColumn>()
                                      .Where(h => !(h is IHasDuration))
                                      .GroupBy(h => h.Column)
                                      .ToDictionary(g => g.Key, g => g.Count());

            if (columnGroups.Count == 0)
                return $"[{keyCount}K]";

            var (_, maxKps, kpsList) = GetKps(beatmap);
            var sorted = columnGroups.OrderBy(kv => kv.Key).ToList();

            // 列统计优化
            int firstCol = sorted.First().Key;
            int lastCol = sorted.Last().Key;
            bool isFirstHigh = checkHighSpeed(beatmap, firstCol, maxKps, kpsList);
            bool isLastHigh = checkHighSpeed(beatmap, lastCol, maxKps, kpsList);

            var remainingColumns = sorted.Skip(1).Take(sorted.Count - 2).ToList();

            double averageNotes = remainingColumns.Any() ? remainingColumns.Average(c => c.Value) : 0;
            int maxNotesInRemainingColumns = remainingColumns.Any() ? remainingColumns.Max(c => c.Value) : 0;

            bool isFirstColumnLow = firstCol < averageNotes * 0.3 || firstCol < maxNotesInRemainingColumns / 3;
            bool isLastColumnLow = lastCol < averageNotes * 0.3 || lastCol < maxNotesInRemainingColumns / 3;

            string result = $"[{keyCount}K]";

            if (keyCount == 6 || keyCount == 8)
            {
                if (isFirstHigh || isLastHigh)
                {
                    result = $"[{keyCount - 1}K1S]";
                }
                else if (isFirstColumnLow || isLastColumnLow)
                {
                    result = $"[{keyCount - 1}+1K]";
                }
            }
            else if (keyCount >= 7)
            {
                if (isFirstHigh || isLastHigh)
                {
                    result = $"[{keyCount - 2}K2S]";
                }
                else if (isFirstColumnLow || isLastColumnLow)
                {
                    result = $"[{keyCount - 2}+2K]";
                }
            }

            int emptyColumns = sorted.Count(c => c.Value == 0);

            if (emptyColumns > 0)
            {
                result = $"[{keyCount - 1}K_{emptyColumns}Null]";
            }

            return result;
        }

        private static bool checkHighSpeed(IBeatmap beatmap, int column, double maxKps, List<double> kpsList)
        {
            // 使用预处理的分组数据优化（需调整参数传递）
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
