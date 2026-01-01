using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public static class ManiaBeatmapAnalysisCache
    {
        // 选歌界面快速滚动/拖动时会瞬间触发大量面板 PrepareForUse。
        // 这里做三件事：
        // 1) 全局共享缓存，避免同一谱面被多个面板重复解析/计算。
        // 2) 并发限流，避免线程池被打爆导致 UI 卡死。
        // 3) 同一 key 的计算去重（in-flight 去重）。
        private static readonly SemaphoreSlim concurrencyLimiter = new SemaphoreSlim(2, 2);

        private static readonly ConcurrentDictionary<string, ManiaBeatmapAnalysisResult> resultCache = new ConcurrentDictionary<string, ManiaBeatmapAnalysisResult>();

        // Lazy<Task<>> 用于去重同一 key 的并发计算。
        private static readonly ConcurrentDictionary<string, Lazy<Task<ManiaBeatmapAnalysisResult>>> inFlight =
            new ConcurrentDictionary<string, Lazy<Task<ManiaBeatmapAnalysisResult>>>();

        public static string CreateCacheKey(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
            => $"{beatmapInfo.Hash}_{ruleset.OnlineID}_{string.Join(",", mods.Select(m => m.Acronym))}";

        public static bool TryGet(string cacheKey, out ManiaBeatmapAnalysisResult result)
            => resultCache.TryGetValue(cacheKey, out result);

        public static Task<ManiaBeatmapAnalysisResult> GetOrComputeAsync(
            BeatmapManager beatmapManager,
            BeatmapInfo beatmapInfo,
            RulesetInfo ruleset,
            IReadOnlyList<Mod> mods,
            int keyCount)
        {
            string cacheKey = CreateCacheKey(beatmapInfo, ruleset, mods);

            if (TryGet(cacheKey, out var cached))
                return Task.FromResult(cached);

            var lazyTask = inFlight.GetOrAdd(cacheKey, _ => new Lazy<Task<ManiaBeatmapAnalysisResult>>(() => computeAsync(
                beatmapManager,
                beatmapInfo,
                ruleset,
                mods,
                keyCount,
                cacheKey)));

            return lazyTask.Value;
        }

        private static async Task<ManiaBeatmapAnalysisResult> computeAsync(
            BeatmapManager beatmapManager,
            BeatmapInfo beatmapInfo,
            RulesetInfo ruleset,
            IReadOnlyList<Mod> mods,
            int keyCount,
            string cacheKey)
        {
            bool acquired = false;

            try
            {
                // 这里不要使用面板的 CancellationToken：
                // 同一 key 的计算可能被多个面板共享，单个面板被回收/取消不应导致共享计算被取消，
                // 否则会出现“某次滑动取消后，这张谱面永远算不出来”的问题。
                await concurrencyLimiter.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                acquired = true;

                var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset, mods, CancellationToken.None);

                var (averageKps, maxKps, kpsList, columnCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);

                // 复用已算出的 columnCounts/kpsList，避免 GetScratch() 再次遍历/计算。
                string scratchText = EzBeatmapCalculator.GetScratchFromPrecomputed(columnCounts, maxKps, kpsList, keyCount);

                var result = new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
                    scratchText);

                resultCache[cacheKey] = result;
                return result;
            }
            finally
            {
                if (acquired)
                    concurrencyLimiter.Release();

                // 清理 in-flight，避免字典无限增长。
                inFlight.TryRemove(cacheKey, out _);
            }
        }
    }

    public readonly record struct ManiaBeatmapAnalysisResult(
        double AverageKps,
        double MaxKps,
        List<double> KpsList,
        Dictionary<int, int> ColumnCounts,
        string ScratchText);
}
