// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Analysis
{
    public static class OptimizedBeatmapCalculator
    {
        public const int DEFAULT_KPS_GRAPH_POINTS = 256;

        // 防止在极端 BPM/Mod 情况下分配过大集合导致 OOM。
        private const int max_estimated_intervals = 100_000;

        public static List<double> DownsampleToFixedCount(IReadOnlyList<double> source, int targetCount)
        {
            if (source.Count == 0)
                return new List<double>();

            if (targetCount <= 0)
                return new List<double>();

            if (source.Count <= targetCount)
            {
                if (source is List<double> list)
                    return list;

                return [with(capacity: source.Count), .. source];
            }

            // Uniform re-sampling by index. Keeps endpoints stable.
            var result = new List<double>(targetCount);

            if (targetCount == 1)
            {
                result.Add(source[0]);
                return result;
            }

            int lastIndex = source.Count - 1;

            for (int i = 0; i < targetCount; i++)
            {
                int index = (int)((long)i * lastIndex / (targetCount - 1));
                result.Add(source[index]);
            }

            return result;
        }

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

            // 预分配List容量以避免频繁扩容（防御性处理 NaN/Infinity/负数）
            int estimatedIntervals = (int)((songEndTime / interval) + 1);
            if (double.IsNaN(songEndTime) || double.IsInfinity(songEndTime) || double.IsNaN(interval) || double.IsInfinity(interval))
                estimatedIntervals = 0;
            estimatedIntervals = Math.Max(0, estimatedIntervals);

            // Diagnostic logging: 当估算的桶数异常时记录关键信息以便追踪问题来源（bpm/interval/songEndTime/objects）
            const int log_estimated_threshold = 10_000;

            if (estimatedIntervals > log_estimated_threshold)
            {
                Logger.Log(
                    $"[OptimizedBeatmapCalculator] large estimatedIntervals={estimatedIntervals} bpm={beatmap.BeatmapInfo.BPM} interval={interval} songEndTime={songEndTime} hitObjects={hitObjects.Count}",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }

            if (estimatedIntervals > max_estimated_intervals)
                estimatedIntervals = max_estimated_intervals;

            var kpsList = new List<double>(estimatedIntervals);
            int hitCount = hitObjects.Count;

            double currentTime = 0;
            int currentIndex = 0;

            while (currentTime < songEndTime)
            {
                double endTime = currentTime + interval;

                int startIdx = currentIndex;
                while (startIdx < hitCount && hitObjects[startIdx].StartTime < currentTime)
                    startIdx++;

                int endIdx = startIdx;
                while (endIdx < hitCount && hitObjects[endIdx].StartTime < endTime)
                    endIdx++;

                int hits = endIdx - startIdx;
                double kps = hits / (interval / 1000.0); // 转换为每秒
                kpsList.Add(kps);
                // 防护：在极端情况下限制桶数，避免无限迭代导致 OOM
                if (kpsList.Count >= max_estimated_intervals)
                    break;

                currentTime += interval;
                currentIndex = startIdx; // 下次从这个位置开始
            }

            if (kpsList.Count == 0)
                return (0, 0, kpsList);

            double sum = 0;
            double max = kpsList[0];

            foreach (double kps in kpsList.AsSpan())
            {
                sum += kps;
                if (kps > max)
                    max = kps;
            }

            return (sum / kpsList.Count, max, kpsList);
        }

        /// <summary>
        /// Counts notes per column, including holds.
        /// </summary>
        public static Dictionary<int, int> GetColumnNoteCountsOptimized(IBeatmap beatmap)
        {
            var counts = new Dictionary<int, int>();
            var hitObjects = beatmap.HitObjects;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                if (hitObjects[i] is IHasColumn columnObj)
                    counts.Increment(columnObj.Column);
            }

            return counts;
        }

        /// <summary>
        /// 计算粗略 KPS 曲线并返回平均/最大值。使用固定较少的桶（bucket）以快速得到可展示的基线数据。
        /// </summary>
        public static List<double> GetKpsCoarse(IBeatmap beatmap, int buckets = 64)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return new List<double>();

            double songStart = hitObjects[0].StartTime;
            double songEnd = hitObjects[^1].StartTime;
            double duration = Math.Max(1, songEnd - songStart);

            long[] bucketsCounts = new long[buckets];

            foreach (var obj in hitObjects)
            {
                int idx = (int)((obj.StartTime - songStart) * buckets / duration);
                if (idx < 0) idx = 0;
                if (idx >= buckets) idx = buckets - 1;
                bucketsCounts[idx]++;
            }

            var kpsList = new List<double>(buckets);
            double intervalMs = duration / buckets;
            for (int i = 0; i < buckets; i++)
                kpsList.Add(bucketsCounts[i] / (intervalMs / 1000.0));

            // double avg = kpsList.Sum() / kpsList.Count;
            // double max = kpsList.Max();

            return kpsList;
        }

        /// <summary>
        /// 一次性计算所有需要的数据，避免重复遍历。
        /// 可通过 <paramref name="onlyKps"/> 跳过列统计，仅保留 KPS 相关结果。
        /// </summary>
        public static (
            double averageKps,
            double maxKps,
            List<double> kpsList,
            Dictionary<int, int> columnCounts,
            Dictionary<int, int> holdNoteCounts
            ) GetAllDataOptimized(IBeatmap beatmap, bool onlyKps = false)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, new List<double>(), new Dictionary<int, int>(), new Dictionary<int, int>());

            // 一次遍历完成KPS和列统计
            double bpm = beatmap.BeatmapInfo.BPM;
            double interval = 240000.0 / bpm;
            double songEndTime = hitObjects[^1].StartTime;

            int estimatedIntervals = (int)((songEndTime / interval) + 1);
            if (double.IsNaN(songEndTime) || double.IsInfinity(songEndTime) || double.IsNaN(interval) || double.IsInfinity(interval))
                estimatedIntervals = 0;
            estimatedIntervals = Math.Max(0, estimatedIntervals);
            if (estimatedIntervals > max_estimated_intervals)
                estimatedIntervals = max_estimated_intervals;

            int sampledPointCount = Math.Min(DEFAULT_KPS_GRAPH_POINTS, estimatedIntervals);
            var kpsList = new List<double>(sampledPointCount);
            var columnCounts = new Dictionary<int, int>();
            var holdNoteCounts = new Dictionary<int, int>();

            int[]? sampledIndices = null;

            if (estimatedIntervals > DEFAULT_KPS_GRAPH_POINTS)
            {
                sampledIndices = new int[DEFAULT_KPS_GRAPH_POINTS];
                int lastIndex = estimatedIntervals - 1;

                for (int i = 0; i < DEFAULT_KPS_GRAPH_POINTS; i++)
                    sampledIndices[i] = (int)((long)i * lastIndex / (DEFAULT_KPS_GRAPH_POINTS - 1));
            }

            int hitCount = hitObjects.Count;

            if (!onlyKps)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    if (hitObjects[i] is IHasColumn columnObj)
                    {
                        columnCounts.Increment(columnObj.Column);

                        if (hitObjects[i] is IHasDuration)
                            holdNoteCounts.Increment(columnObj.Column);
                    }
                }
            }

            // KPS计算
            double currentTime = 0;
            int currentIndex = 0;
            int intervalIndex = 0;
            int sampledIndex = 0;
            double sum = 0;
            double max = 0;

            while (currentTime < songEndTime)
            {
                double endTime = currentTime + interval;

                int startIdx = currentIndex;
                while (startIdx < hitCount && hitObjects[startIdx].StartTime < currentTime)
                    startIdx++;

                int endIdx = startIdx;
                while (endIdx < hitCount && hitObjects[endIdx].StartTime < endTime)
                    endIdx++;

                int hits = endIdx - startIdx;
                double kps = hits / (interval / 1000.0);

                sum += kps;
                if (intervalIndex == 0 || kps > max)
                    max = kps;

                if (sampledIndices == null)
                {
                    kpsList.Add(kps);
                }
                else if (sampledIndex < sampledIndices.Length && intervalIndex == sampledIndices[sampledIndex])
                {
                    kpsList.Add(kps);
                    sampledIndex++;
                }

                // 防护：在极端情况下限制桶数，避免无限迭代导致 OOM
                intervalIndex++;

                if (intervalIndex >= max_estimated_intervals)
                    break;

                currentTime += interval;
                currentIndex = startIdx;
            }

            if (intervalIndex == 0)
                return (0, 0, kpsList, columnCounts, holdNoteCounts);

            double average = sum / intervalIndex;

            return (average, max, kpsList, columnCounts, holdNoteCounts);
        }
    }
}
