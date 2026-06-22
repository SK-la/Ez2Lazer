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
        /// 当前正在进行的预加载任务（用于取消）。
        /// </summary>
        private CancellationTokenSource? currentPreloadCts;

        /// <summary>
        /// 缓存：BeatmapInfo.ID → 预加载数据（跨局复用，只要谱面没变就不重加载）。
        /// </summary>
        private readonly Dictionary<Guid, List<EzScoreRaceState>> preloadedCache = new Dictionary<Guid, List<EzScoreRaceState>>();

        private readonly Dictionary<Guid, CancellationTokenSource> preloadingBeatmaps = new Dictionary<Guid, CancellationTokenSource>();

        protected override void LoadComplete()
        {
            base.LoadComplete();
            currentBeatmap.BindValueChanged(onBeatmapChanged, true);
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> e)
        {
            var beatmapInfo = e.NewValue?.BeatmapInfo;

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
                publishStates(cached);
                Logger.Log($"[EzScoreRaceService] Cache hit for beatmap {beatmapInfo.ID}, {cached.Count} states", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            if (preloadingBeatmaps.ContainsKey(beatmapInfo.ID))
                return;

            startPreload(beatmapInfo, e.NewValue!);
        }

        private void startPreload(BeatmapInfo beatmapInfo, WorkingBeatmap workingBeatmap)
        {
            cancelCurrentPreload();

            currentPreloadCts = new CancellationTokenSource();
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

                    preloadedCache[beatmapInfo.ID] = ezScoreRaceStates;
                    preloadingBeatmaps.Remove(beatmapInfo.ID);

                    // 如果当前谱面就是刚预加载的谱面，立即发布到字典
                    if (currentBeatmap.Value?.BeatmapInfo.ID == beatmapInfo.ID)
                        publishStates(ezScoreRaceStates);

                    Logger.Log($"[EzScoreRaceService] Preloaded {ezScoreRaceStates.Count} ghost states for beatmap {beatmapInfo.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                });
            }
            catch (OperationCanceledException)
            {
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
                preloadedCache.Remove(beatmapId.Value);
                if (currentBeatmap.Value != null)
                    startPreload(currentBeatmap.Value.BeatmapInfo, currentBeatmap.Value);
            }
        }

        private void cancelCurrentPreload()
        {
            if (currentPreloadCts != null)
            {
                currentPreloadCts.Cancel();
                currentPreloadCts.Dispose();
                currentPreloadCts = null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                cancelCurrentPreload();

                foreach (var cts in preloadingBeatmaps.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                preloadingBeatmaps.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
