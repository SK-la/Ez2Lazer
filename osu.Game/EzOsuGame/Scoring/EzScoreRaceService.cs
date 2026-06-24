// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 全局后台服务。对齐官方 <see cref="osu.Game.Online.Spectator.SpectatorClient"/> 的设计：
    ///
    /// - 订阅 <c>currentBeatmap</c> 变化，在选歌界面后台预加载 ghost 数据（Realm 查询 + timeline 构建）
    /// - 预加载完成后将 <c>Guid → EzScoreRaceState</c> 写入 <see cref="States"/> 字典
    /// - <see cref="EzScoreRaceTimelineScoreProcessor"/> 订阅字典，自动按时钟更新 bindable
    /// - HUD 组件直接订阅 processor bindable，无需中间层
    ///
    /// 以 <see cref="Component"/> 形式注册到 <see cref="OsuGame"/>，生命周期与进程一致。
    /// </summary>
    public partial class EzScoreRaceService : Component, IEzScoreRaceStateLookup
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

        /// <summary>
        /// 全局 ghost 状态字典。对齐官方 <c>SpectatorClient.WatchedUserStates</c>。
        /// Key = <c>ScoreInfo.ID.ToString()</c>。
        /// </summary>
        public IBindableDictionary<string, EzScoreRaceState> States => states;

        private readonly BindableDictionary<string, EzScoreRaceState> states = new BindableDictionary<string, EzScoreRaceState>();

        private Guid? currentBeatmapId;

        /// <summary>
        /// 当前正在进行的预加载任务（用于取消）。同一个实例也会作为 value 放入 <see cref="preloadingBeatmaps"/>，
        /// 因此取消/释放时必须先从字典移除，避免外部遍历到已释放的实例。
        /// </summary>
        private CancellationTokenSource? currentPreloadCts;

        /// <summary>
        /// <see cref="currentPreloadCts"/> 正在预加载的谱面 ID。用于把 <see cref="preloadingBeatmaps"/> 里的条目
        /// 与当前 CTS 对应起来，避免在取消/释放时遗留失效引用。
        /// </summary>
        private Guid? currentPreloadBeatmapId;

        /// <summary>
        /// 缓存：BeatmapInfo.ID → 预加载数据（跨局复用，只要谱面没变就不重加载）。
        /// 上限 <see cref="preloaded_cache_capacity"/> 首，超出按 LRU 淘汰最久未访问的谱面。
        /// 注意：必须与 <see cref="preloadedCacheLru"/> 同步维护，二者构成 LRU 的双向索引。
        /// </summary>
        private readonly Dictionary<Guid, List<EzScoreRaceState>> preloadedCache = new Dictionary<Guid, List<EzScoreRaceState>>();

        /// <summary>
        /// LRU 访问顺序链表：表头 = 最近一次命中/写入的谱面，表尾 = 最久未访问。
        /// 节点 value 是 <see cref="preloadedCache"/> 的 key。
        /// </summary>
        private readonly LinkedList<Guid> preloadedCacheLru = new LinkedList<Guid>();

        /// <summary>
        /// 最多缓存多少首谱面的 ghost 数据。超出按 LRU 淘汰。
        /// </summary>
        private const int preloaded_cache_capacity = 3;

        private readonly Dictionary<Guid, CancellationTokenSource> preloadingBeatmaps = new Dictionary<Guid, CancellationTokenSource>();

        protected override void LoadComplete()
        {
            base.LoadComplete();
            currentBeatmap.BindValueChanged(onBeatmapChanged, true);
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> e)
        {
            var beatmapInfo = e.NewValue.BeatmapInfo;

            if (beatmapInfo == null)
                return;

            // 谱面 ID 变化时清空字典。即使是同一谱面退出再进，也强制重建。
            // 这保证了每次进入游戏时 ghost 都是干净的，不会跨局串味。
            if (currentBeatmapId != beatmapInfo.ID)
            {
                currentBeatmapId = beatmapInfo.ID;
                states.Clear();
            }

            if (preloadedCache.TryGetValue(beatmapInfo.ID, out var cached))
            {
                touchPreloadedCacheLru(beatmapInfo.ID);
                publishStates(cached);
                Logger.Log($"[EzScoreRaceService] Cache hit for beatmap {beatmapInfo.ID}, {cached.Count} states", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            if (preloadingBeatmaps.ContainsKey(beatmapInfo.ID))
                return;

            startPreload(beatmapInfo, e.NewValue);
        }

        private void startPreload(BeatmapInfo beatmapInfo, WorkingBeatmap workingBeatmap)
        {
            cancelCurrentPreload();

            currentPreloadCts = new CancellationTokenSource();
            currentPreloadBeatmapId = beatmapInfo.ID;
            var token = currentPreloadCts.Token;
            preloadingBeatmaps[beatmapInfo.ID] = currentPreloadCts;

            Schedule(() => performPreloadAsync(beatmapInfo, workingBeatmap, token));
        }

        private async void performPreloadAsync(BeatmapInfo beatmapInfo, WorkingBeatmap workingBeatmap, CancellationToken token)
        {
            try
            {
                var ezScoreRaceStates = await Task.Run(() => buildPreloadedStates(beatmapInfo, workingBeatmap, token), token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    return;

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    storePreloadedCache(beatmapInfo.ID, ezScoreRaceStates);
                    preloadingBeatmaps.Remove(beatmapInfo.ID);

                    // 如果当前谱面就是刚预加载的谱面，立即发布到字典
                    if (currentBeatmap.Value?.BeatmapInfo.ID == beatmapInfo.ID)
                        publishStates(ezScoreRaceStates);

                    Logger.Log($"[EzScoreRaceService] Preloaded {ezScoreRaceStates.Count} ghost states for beatmap {beatmapInfo.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                });
            }
            catch (OperationCanceledException)
            {
                // 用户切到别的谱面/Dispose 触发的取消。当前 preload 已被 cancelCurrentPreload 从 preloadingBeatmaps 移除，
                // 这里只记一行 debug 方便排查"为啥这首谱面的 ghost 永远不出现"。
                Logger.Log($"[EzScoreRaceService] Preload cancelled for beatmap {beatmapInfo.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[EzScoreRaceService] Failed to preload ghost states for beatmap {beatmapInfo.ID}", Ez2ConfigManager.LOGGER_NAME);
                Schedule(() =>
                {
                    preloadingBeatmaps.Remove(beatmapInfo.ID);
                });
            }
        }

        /// <summary>
        /// 将 preloaded states 发布到 BindableDictionary，触发所有订阅者的字典变化事件。
        /// 对齐官方 SpectatorClient 在 ISpectatorClient.UserBeganPlaying 时写入 WatchedUserStates。
        /// </summary>
        private void publishStates(List<EzScoreRaceState> statesToPublish)
        {
            states.Clear();

            foreach (var state in statesToPublish)
                states[state.ScoreInfo.ID.ToString()] = state;
        }

        private List<EzScoreRaceState> buildPreloadedStates(BeatmapInfo beatmapInfo, WorkingBeatmap workingBeatmap, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!EzScoreRaceRulesetSupport.SupportsGhostRace(workingBeatmap.BeatmapInfo.Ruleset))
                return new List<EzScoreRaceState>();

            var rulesetInfo = workingBeatmap.BeatmapInfo.Ruleset;
            var allLocalScores = EzLocalScoreQueries.GetLocalScoresWithReplay(realm, beatmapInfo, rulesetInfo);
            token.ThrowIfCancellationRequested();

            var ghostScores = EzLocalScoreQueries.GetTopByTotalScore(allLocalScores, 10);
            token.ThrowIfCancellationRequested();

            var environment = GameplayEnvironment.FromLive(GlobalConfigStore.EzConfig);
            var sharedBeatmap = workingBeatmap.GetPlayableBeatmap(rulesetInfo, Array.Empty<Mod>());

            var result = new List<EzScoreRaceState>();

            foreach (var scoreInfo in ghostScores)
            {
                token.ThrowIfCancellationRequested();

                var timeline = EzScoreTimelineBuilder.TryBuild(
                    scoreManager,
                    beatmaps,
                    scoreInfo,
                    sharedBeatmap,
                    cache: null,
                    environment,
                    token
                );

                result.Add(new EzScoreRaceState(scoreInfo, timeline));
            }

            return result;
        }

        /// <summary>
        /// 通知服务当前谱面 Mod 发生了变化。清空字典并强制重新预加载。
        /// </summary>
        public void NotifyModsChanged()
        {
            var beatmapId = currentBeatmap.Value?.BeatmapInfo.ID;

            if (beatmapId.HasValue)
            {
                states.Clear();
                evictPreloadedCache(beatmapId.Value);
                if (currentBeatmap.Value != null)
                    startPreload(currentBeatmap.Value.BeatmapInfo, currentBeatmap.Value);
            }
        }

        /// <summary>
        /// 命中/读取缓存时把对应谱面移到 LRU 链表头。必须与 <see cref="preloadedCache"/> 同步维护。
        /// </summary>
        private void touchPreloadedCacheLru(Guid beatmapId)
        {
            if (preloadedCacheLru.First is { Value: var headId } && headId == beatmapId)
                return;

            if (preloadedCacheLru.Last is { Value: var tailId } && tailId == beatmapId)
            {
                preloadedCacheLru.RemoveLast();
                preloadedCacheLru.AddFirst(beatmapId);
                return;
            }

            // 一般不会到这：要么链表只有 1 节点命中首/尾，要么确实存在于中间。
            // 中间情况少见（preloaded_cache_capacity 很小），为简洁起见 remove+add 到头。
            for (var node = preloadedCacheLru.First; node != null; node = node.Next)
            {
                if (node.Value != beatmapId)
                    continue;

                preloadedCacheLru.Remove(node);
                preloadedCacheLru.AddFirst(node);
                return;
            }
        }

        /// <summary>
        /// 写入缓存并维护 LRU 容量（超过 <see cref="preloaded_cache_capacity"/> 时淘汰表尾）。
        /// </summary>
        private void storePreloadedCache(Guid beatmapId, List<EzScoreRaceState> statesToStore)
        {
            preloadedCache[beatmapId] = statesToStore;
            touchPreloadedCacheLru(beatmapId);

            while (preloadedCacheLru.Count > preloaded_cache_capacity)
            {
                var evictedId = preloadedCacheLru.Last!.Value;
                preloadedCacheLru.RemoveLast();
                preloadedCache.Remove(evictedId);
                Logger.Log($"[EzScoreRaceService] LRU evict beatmap {evictedId} (cache capacity {preloaded_cache_capacity})", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        /// <summary>
        /// 从缓存里强制移除指定谱面（用于 NotifyModsChanged 触发强制重加载）。
        /// </summary>
        private void evictPreloadedCache(Guid beatmapId)
        {
            preloadedCache.Remove(beatmapId);

            for (var node = preloadedCacheLru.First; node != null; node = node.Next)
            {
                if (node.Value != beatmapId)
                    continue;

                preloadedCacheLru.Remove(node);
                return;
            }
        }

        private void cancelCurrentPreload()
        {
            var cts = currentPreloadCts;
            if (cts == null)
                return;

            currentPreloadCts = null;

            // 先从字典移除该条目，再 Cancel/Dispose，避免外面遍历字典时撞到已释放的实例。
            if (currentPreloadBeatmapId.HasValue)
            {
                preloadingBeatmaps.Remove(currentPreloadBeatmapId.Value);
                currentPreloadBeatmapId = null;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 二次释放保护：理论上不应到这里，但即便发生也不应让取消逻辑反过来抛出。
            }
            finally
            {
                cts.Dispose();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // cancelCurrentPreload 内部已同步把 currentPreloadCts 从 preloadingBeatmaps 中移除，
                // 并对 Cancel/Dispose 做了二次释放保护。
                cancelCurrentPreload();

                // 正常情况下 preloadingBeatmaps 此时应为空（仅 currentPreloadCts 会作为 value 入字典，
                // 且它已被上面清掉）。保留遍历只是为了兜底万一未来加入并行预加载时不会再次抛 ObjectDisposedException。
                foreach (var cts in preloadingBeatmaps.Values)
                {
                    try
                    {
                        cts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }

                preloadingBeatmaps.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
