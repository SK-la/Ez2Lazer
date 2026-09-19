// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 全局 ghost 角逐服务。
    ///
    /// 消费者判定 = 静态预测（<see cref="demand"/>：皮肤层 / Ez 布局层是否配置了角逐 HUD）取或
    /// 运行时真相（<see cref="ConsumerInterestCount"/>：实际装载的角逐 HUD）。
    /// - 预测为真时在 <see cref="PlayerLoader"/> 进入即开始查询 / 构建，与 <see cref="Player"/> 装载并行
    /// - 预测漏判（如规则集漂移）时，由 <see cref="tryResolveDemandFromConsumers"/> 在该轮 loading 末尾补建
    /// - 无消费者时与「服务未加载」不可区分：不查 Realm、不构建、不阻塞进局、States 恒为空
    /// - 查询与构建只发生在 <see cref="PlayerLoader"/> 内；进游戏后除 HUD 卸载释放（cancel + 清 States）外，
    ///   不发生任何角逐工作
    /// - 可通过实验性开关 <see cref="Ez2Setting.EzScoreRaceServiceEnabled"/> 整服务 no-op
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

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private EzLayoutLayer ezLayoutLayer { get; set; } = null!;

        /// <summary>Mod 过滤策略（HUD ModFilterSetting 绑定到此）。</summary>
        public Bindable<EzScoreModFilter> ModFilter { get; } = new Bindable<EzScoreModFilter>(EzScoreModFilter.Any);

        /// <summary>ghost 条目上限（HUD MaxEntriesSetting 绑定到此）。</summary>
        public BindableNumber<int> MaxEntries { get; } = new BindableNumber<int>(5)
        {
            MinValue = 1,
            MaxValue = 10,
        };

        public IBindableDictionary<string, EzScoreRaceState> States => states;

        /// <summary>进局后 ghost timeline 是否仍在后台构建。</summary>
        public bool IsTimelineBuildInProgress { get; private set; }

        /// <summary>
        /// 服务总开关（实验设置 <see cref="Ez2Setting.EzScoreRaceServiceEnabled"/>）的只读视图。
        /// false 时整个角逐功能应为 0 影响：服务不做任何查询/构建/屏幕绑定，HUD 消费者也应完全 inert。
        /// </summary>
        public IBindable<bool> Enabled => serviceEnabled;

        /// <summary>
        /// 当前已装载的角逐 HUD 消费者数量。为 0 表示场景里没有任何消费者。
        /// 这是「消费者真相」，用于校正静态布局预测（见 <see cref="demand"/>）在规则集漂移等情形下的漏判。
        /// </summary>
        public int ConsumerInterestCount { get; private set; }

        /// <summary>
        /// 角逐 HUD 装载时注册。服务关闭时不计入，保证 0 影响。
        ///
        /// 注意：0→1 **不触发**任何 Realm 查询或 timeline 构建。查询与构建只由
        /// <see cref="beginLoaderPreparation"/> 在 <see cref="PlayerLoader"/> 内发起；
        /// 此处仅抬高 <see cref="ConsumerInterestCount"/>，供 loading 末尾的裁决读取。
        /// </summary>
        public void RegisterInterest()
        {
            if (!isServiceActive)
                return;

            ConsumerInterestCount++;
        }

        /// <summary>
        /// 角逐 HUD 卸载时注销；归零后取消在途构建并清空 States。
        ///
        /// 这是进局后**唯一**允许的角逐工作，且只减不增（cancel + clear，O(1)），不会触发查询或 replay 仿真。
        /// </summary>
        public void UnregisterInterest()
        {
            if (ConsumerInterestCount <= 0)
                return;

            ConsumerInterestCount--;

            if (ConsumerInterestCount > 0)
                return;

            enterQuiescentState();
        }

        private bool hasConsumers => ConsumerInterestCount > 0;

        private readonly BindableDictionary<string, EzScoreRaceState> states = new BindableDictionary<string, EzScoreRaceState>();

        private readonly IEzScoreTimelineCache timelineCache = EzScoreTimelineBuilder.CreateSessionCache();

        /// <summary>metadata 缓存：queryKey → ghost 元数据列表（timeline 可能 null 或部分就绪）。</summary>
        private readonly Dictionary<string, List<EzScoreRaceState>> metadataCache = new Dictionary<string, List<EzScoreRaceState>>();

        private readonly LinkedList<string> metadataCacheLru = new LinkedList<string>();

        private const int metadata_cache_capacity = 3;

        private string? activeQueryKey;
        private Guid? activeBeatmapId;

        private CancellationTokenSource? timelineBuildCts;
        private Task? timelineBuildTask;
        private int timelineBuildVersion;

        private Bindable<bool> serviceEnabled = new Bindable<bool>(true);
        private Bindable<EzReplayFeedMode> feedMode = new Bindable<EzReplayFeedMode>(EzReplayFeedMode.BatchAllEvents);

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager config)
        {
            serviceEnabled = config.GetBindable<bool>(Ez2Setting.EzScoreRaceServiceEnabled);
            feedMode = config.GetBindable<EzReplayFeedMode>(Ez2Setting.EzScoreRaceFeedMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            serviceEnabled.BindValueChanged(onServiceEnabledChanged, true);

            ModFilter.BindValueChanged(_ => onQueryContextChanged());
            MaxEntries.BindValueChanged(_ => onQueryContextChanged());

            subscribeScreenHooks();
            recomputeDemand();

            currentBeatmap.BindValueChanged(onBeatmapChanged, true);
        }

        private bool isServiceActive => serviceEnabled.Value;

        /// <summary>
        /// 静态预测：皮肤层与 Ez 布局层是否配置了角逐 HUD。进局前即可得，用于让构建与 Player 装载并行。
        ///
        /// 预测为假不构成最终结论 —— 实际装载的角逐 HUD 会在 loading 末尾以
        /// <see cref="ConsumerInterestCount"/> 校正（见 <c>tryResolveDemandFromConsumers</c>）。
        /// </summary>
        private bool demand;

        private Skin? demandSkin;
        private RulesetInfo? demandRuleset;

        private const int code_default_cache_capacity = 64;

        private readonly Dictionary<(Skin, GlobalSkinnableContainers, string?), bool> codeDefaultCache = new Dictionary<(Skin, GlobalSkinnableContainers, string?), bool>();

        // loaderPreparationActive / shouldPerformScoreRaceWork — see EzScoreRaceService.LoaderPrep.cs

        /// <summary>
        /// 重新判定消费者需求。只在选歌入口与初始加载调用。
        ///
        /// 本方法**只判定、不启动任何工作**：
        /// - 需求由 true 变 false 时回到静默态（取消在途构建、清空 States）；
        /// - 需求由 false 变 true 时仅置位，不查询、不构建。
        ///   真正的查询与构建仍由 <see cref="beginLoaderPreparation"/> 在 <see cref="PlayerLoader"/> 触发。
        /// 这样才能保证「无消费者 ⇒ 与未加载服务不可区分」。
        /// </summary>
        private void recomputeDemand(bool force = false)
        {
            var skin = skins.CurrentSkin.Value;
            var ruleset = getSelectedRuleset();

            if (!force && ReferenceEquals(demandSkin, skin) && sameRuleset(ruleset, demandRuleset))
                return;

            demandSkin = skin;
            demandRuleset = ruleset;

            bool previous = demand;
            demand = computeDemandSafely(skin, ruleset);

            // 只处理「需求消失」这一个方向；需求出现不驱动任何查询/构建。
            if (ShouldEnterQuiescentState(previous, demand))
                enterQuiescentState();
        }

        private static bool sameRuleset(RulesetInfo? a, RulesetInfo? b)
            => ReferenceEquals(a, b) || (a != null && a.Equals(b));

        /// <summary>
        /// 布局扫描涉及反射与反序列化，任何异常都按「无消费者」处理，不让它冒泡进屏幕回调。
        /// </summary>
        private bool computeDemandSafely(Skin? skin, RulesetInfo? ruleset)
        {
            try
            {
                return computeDemand(skin, ruleset);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[EzScoreRaceService] Demand computation failed; treating as no consumers", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// 遍历全部 <see cref="GlobalSkinnableContainers"/>，检查皮肤层与 Ez 布局层是否配置了角逐 HUD。
        /// global（ruleset 为 null）与规则集专属布局在运行时是两个容器，都要查。
        ///
        /// 已保存的布局只比对 <see cref="SerialisedDrawableInfo.Type"/>，不构造任何组件；
        /// 只有「用户未保存布局、由皮肤代码提供默认」这一种情况才需要真正构造（按皮肤实例缓存）。
        /// </summary>
        private bool computeDemand(Skin? skin, RulesetInfo? ruleset)
        {
            foreach (GlobalSkinnableContainers target in Enum.GetValues<GlobalSkinnableContainers>())
            {
                var globalLookup = new GlobalSkinnableContainerLookup(target);
                var rulesetLookup = new GlobalSkinnableContainerLookup(target, ruleset);

                if (skin != null && (hasSavedScoreRaceHud(skin, globalLookup) || hasSavedScoreRaceHud(skin, rulesetLookup)))
                    return true;

                var ezLayout = ezLayoutLayer.Store.Load(target);

                if (hasSavedScoreRaceHud(ezLayout, globalLookup) || hasSavedScoreRaceHud(ezLayout, rulesetLookup))
                    return true;

                if (codeDefaultProvidesScoreRaceHud(skin, target, ruleset))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 用户保存的布局（<see cref="Skin.LayoutInfos"/>）里是否有角逐 HUD。
        /// </summary>
        private static bool hasSavedScoreRaceHud(Skin skin, GlobalSkinnableContainerLookup lookup)
            => hasSavedScoreRaceHud(skin.LayoutInfos.TryGetValue(lookup.Lookup, out var layoutInfo) ? layoutInfo : null, lookup);

        /// <summary>
        /// 指定布局在 <paramref name="lookup"/> 这一维度上是否有角逐 HUD；该维度没有条目时返回 false。
        /// </summary>
        private static bool hasSavedScoreRaceHud(SkinLayoutInfo? layoutInfo, GlobalSkinnableContainerLookup lookup)
            => layoutInfo != null
               && layoutInfo.TryGetDrawableInfo(lookup.Ruleset, out var infos)
               && ContainsScoreRaceHudType(infos);

        /// <summary>
        /// 皮肤代码默认是否提供角逐 HUD。用户保存过布局时其完全取代代码默认
        /// （与 <see cref="SkinnableContainer"/> 的加载顺序一致），此时该布局已被上面的 Type 扫描覆盖，无需构造。
        ///
        /// 代码默认是皮肤类型 + 规则集的静态属性，故按 (皮肤, target, 规则集) 缓存。缓存跨皮肤实例保留并有容量上限，
        /// 避免来回切皮肤时反复构造默认组件。
        /// </summary>
        private bool codeDefaultProvidesScoreRaceHud(Skin? skin, GlobalSkinnableContainers target, RulesetInfo? ruleset)
        {
            if (skin == null || hasUserSave(skin, target, ruleset))
                return false;

            var key = (skin, target, ruleset?.ShortName);

            if (codeDefaultCache.TryGetValue(key, out bool cached))
                return cached;

            bool result = ContainsScoreRaceHud(GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(target, ruleset)));

            if (codeDefaultCache.Count >= code_default_cache_capacity)
                codeDefaultCache.Clear();

            codeDefaultCache[key] = result;
            return result;
        }

        private static bool hasUserSave(Skin skin, GlobalSkinnableContainers target, RulesetInfo? ruleset)
            => skin.LayoutInfos.TryGetValue(target, out var layoutInfo) && layoutInfo.TryGetDrawableInfo(ruleset, out _);

        /// <summary>
        /// 与 <see cref="SkinnableContainer"/> 的组件加载同序：用户保存的布局优先，其次皮肤代码默认。
        /// </summary>
        internal static Drawable? GetSkinComponentsContainer(Skin? skin, GlobalSkinnableContainerLookup lookup)
        {
            if (skin == null)
                return null;

            return skin.GetDrawableComponent(new UserSkinComponentLookup(lookup))
                   ?? skin.GetDrawableComponent(lookup);
        }

        /// <summary>
        /// 递归检查容器（含用户嵌套的 Children）是否包含任一角逐 HUD 组件。
        /// osu.Framework 的容器实现协变的 IEnumerable&lt;Drawable&gt;，因此可统一枚举。
        /// </summary>
        internal static bool ContainsScoreRaceHud(Drawable? drawable)
        {
            if (drawable == null)
                return false;

            if (drawable is EzHUDScoreRaceLeaderboard || drawable is EzHUDScoreCompareBars)
                return true;

            if (drawable is IEnumerable<Drawable> children)
            {
                foreach (var child in children)
                {
                    if (ContainsScoreRaceHud(child))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 递归比对序列化类型（含嵌套 <see cref="SerialisedDrawableInfo.Children"/>），不构造任何实例。
        /// </summary>
        internal static bool ContainsScoreRaceHudType(IEnumerable<SerialisedDrawableInfo>? infos)
        {
            if (infos == null)
                return false;

            foreach (var info in infos)
            {
                if (IsScoreRaceHudType(info.Type))
                    return true;

                if (ContainsScoreRaceHudType(info.Children))
                    return true;
            }

            return false;
        }

        internal static bool IsScoreRaceHudType(Type type)
            => typeof(EzHUDScoreRaceLeaderboard).IsAssignableFrom(type) || typeof(EzHUDScoreCompareBars).IsAssignableFrom(type);

        /// <summary>
        /// 当前屏幕的规则集。取屏幕自身的 bindable 值而不订阅它，避免把规则集变化变成新的重扫触发源。
        /// </summary>
        private RulesetInfo? getSelectedRuleset() => boundModsScreen?.Ruleset.Value;

        private void onServiceEnabledChanged(ValueChangedEvent<bool> e)
        {
            if (!e.NewValue)
            {
                loaderPreparationActive = false;
                loaderPreparationPending = false;
                enterQuiescentState();
                // 关闭时同时断开屏幕 mod 绑定，确保切屏零残留处理。
                unbindScreenMods();
                return;
            }

            // 重新启用：补绑当前屏幕的 mod / 规则集（关闭期间屏幕钩子不做绑定）。
            bindModsFromScreen(game.ScreenStack.CurrentScreen as OsuScreen);
            recomputeDemand();

            if (!shouldPerformScoreRaceWork)
                return;

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            if (!areAllGhostTimelinesReady())
                requestTimelineBuild(priority: true);
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> e)
        {
            if (!isServiceActive)
                return;

            var beatmapInfo = e.NewValue.BeatmapInfo;

            if (beatmapInfo == null)
                return;

            if (activeBeatmapId != beatmapInfo.ID)
            {
                activeBeatmapId = beatmapInfo.ID;
                cancelTimelineBuild();
            }

            if (!shouldPerformScoreRaceWork)
                return;

            refreshMetadata(e.NewValue);

            if (!areAllGhostTimelinesReady())
                requestTimelineBuild(priority: true);
        }

        private void onQueryContextChanged()
        {
            if (!isServiceActive || !shouldPerformScoreRaceWork)
                return;

            cancelTimelineBuild();

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            if (!areAllGhostTimelinesReady())
                requestTimelineBuild(priority: true);
        }

        /// <summary>
        /// 通知 Mod 组合变化（与 <see cref="ModFilter"/> 变更等效，并 evict 当前谱面 metadata 缓存）。
        /// </summary>
        public void NotifyModsChanged()
        {
            if (activeQueryKey != null)
                evictMetadataCache(activeQueryKey);

            onQueryContextChanged();
        }

        private void refreshMetadata(WorkingBeatmap workingBeatmap)
        {
            if (!isServiceActive || !shouldPerformScoreRaceWork)
                return;

            var beatmapInfo = workingBeatmap.BeatmapInfo;

            if (beatmapInfo == null)
                return;

            if (!EzScoreRaceRulesetSupport.SupportsGhostRace(beatmapInfo.Ruleset))
            {
                publishStatesDiff(Array.Empty<EzScoreRaceState>());
                return;
            }

            string queryKey = buildQueryKey(beatmapInfo.ID);
            activeQueryKey = queryKey;

            if (metadataCache.TryGetValue(queryKey, out var cached))
            {
                touchMetadataCacheLru(queryKey);
                publishStatesDiff(cached);
                return;
            }

            var rulesetInfo = beatmapInfo.Ruleset;
            var allLocalScores = EzLocalScoreQueries.GetLocalScoresWithReplay(realm, beatmapInfo, rulesetInfo);
            var ghostScores = EzLocalScoreQueries.SelectGhostCandidates(
                allLocalScores,
                getCurrentMods(),
                ModFilter.Value,
                MaxEntries.Value);

            var metadataStates = ghostScores
                                 .Select(s => new EzScoreRaceState(s, timeline: null))
                                 .ToList();

            storeMetadataCache(queryKey, metadataStates);
            publishStatesDiff(metadataStates);
        }

        private void requestTimelineBuild(bool priority)
        {
            if (!isServiceActive || !shouldPerformScoreRaceWork)
                return;

            var workingBeatmap = currentBeatmap.Value;
            var beatmapInfo = workingBeatmap?.BeatmapInfo;

            if (beatmapInfo == null || !EzScoreRaceRulesetSupport.SupportsGhostRace(beatmapInfo.Ruleset))
                return;

            if (states.Count == 0)
                refreshMetadata(workingBeatmap!);

            cancelTimelineBuild();

            timelineBuildCts = new CancellationTokenSource();
            var token = timelineBuildCts.Token;
            int version = timelineBuildVersion;
            IsTimelineBuildInProgress = true;

            var previousBuild = timelineBuildTask;
            timelineBuildTask = executeTimelineBuildAsync(previousBuild, workingBeatmap!, beatmapInfo, token, version);
        }

        /// <summary>
        /// 串行 timeline 构建：先等待上一轮任务真正结束，再跑本轮，避免 cancel 后孤儿线程叠满 CPU。
        /// </summary>
        private async Task executeTimelineBuildAsync(Task? waitForPrevious, WorkingBeatmap workingBeatmap, BeatmapInfo beatmapInfo, CancellationToken token, int version)
        {
            if (waitForPrevious != null)
            {
                try
                {
                    await waitForPrevious.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[EzScoreRaceService] Previous timeline build faulted", Ez2ConfigManager.LOGGER_NAME);
                }
            }

            if (token.IsCancellationRequested || version != timelineBuildVersion)
            {
                scheduleTimelineBuildFinished(version);
                return;
            }

            try
            {
                var scoreInfos = states.Values.Select(s => s.ScoreInfo).ToList();

                if (scoreInfos.Count == 0)
                {
                    scheduleTimelineBuildFinished(version);
                    return;
                }

                var rulesetInfo = beatmapInfo.Ruleset;
                var results = new EzScoreTimeline?[scoreInfos.Count];

                // 同一谱面只转一次 playable，各 ghost 只读共享。
                IBeatmap? sharedPlayable = null;

                bool anyNeedsBuild = false;

                for (int i = 0; i < scoreInfos.Count; i++)
                {
                    if (states.TryGetValue(scoreInfos[i].ID.ToString(), out var existing) && existing.Timeline != null)
                    {
                        results[i] = existing.Timeline;
                        continue;
                    }

                    anyNeedsBuild = true;
                }

                if (anyNeedsBuild)
                    sharedPlayable = workingBeatmap.GetPlayableBeatmap(rulesetInfo, Array.Empty<Mod>());

                await Task.Run(() =>
                {
                    for (int i = 0; i < scoreInfos.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        if (version != timelineBuildVersion)
                            return;

                        if (results[i] != null)
                            continue;

                        try
                        {
                            results[i] = EzScoreTimelineBuilder.TryBuild(
                                scoreManager,
                                beatmaps,
                                scoreInfos[i],
                                sharedPlayable,
                                timelineCache,
                                token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // One bad ghost (e.g. leftover deleted-mod edge case) must not abort the whole batch.
                            results[i] = null;
                            Logger.Error(ex,
                                $"[EzScoreRaceService] Timeline build failed for score {scoreInfos[i].ID}",
                                Ez2ConfigManager.LOGGER_NAME);
                        }
                    }
                }, token).ConfigureAwait(false);

                if (token.IsCancellationRequested || version != timelineBuildVersion)
                    return;

                Schedule(() =>
                {
                    if (token.IsCancellationRequested || version != timelineBuildVersion)
                        return;

                    for (int i = 0; i < scoreInfos.Count; i++)
                    {
                        string id = scoreInfos[i].ID.ToString();

                        if (!states.TryGetValue(id, out var state))
                            continue;

                        state.Timeline = results[i];
                    }

                    if (activeQueryKey != null && metadataCache.TryGetValue(activeQueryKey, out var cached))
                    {
                        foreach (var cachedState in cached)
                        {
                            if (states.TryGetValue(cachedState.ScoreInfo.ID.ToString(), out var live))
                                cachedState.Timeline = live.Timeline;
                        }
                    }

                    scheduleTimelineBuildFinished(version);
                    Logger.Log($"[EzScoreRaceService] Timeline build complete for {scoreInfos.Count} ghosts", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                });
            }
            catch (OperationCanceledException)
            {
                scheduleTimelineBuildFinished(version);
                Logger.Log("[EzScoreRaceService] Timeline build cancelled", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                scheduleTimelineBuildFinished(version);
                Logger.Error(ex, "[EzScoreRaceService] Timeline build failed", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        private void scheduleTimelineBuildFinished(int version)
        {
            Schedule(() =>
            {
                if (version == timelineBuildVersion)
                    IsTimelineBuildInProgress = false;
            });
        }

        private void enterQuiescentState()
        {
            cancelTimelineBuild();
            activeQueryKey = null;
            metadataCache.Clear();
            metadataCacheLru.Clear();
            timelineCache.Clear();
            publishStatesDiff(Array.Empty<EzScoreRaceState>());
        }

        private void publishStatesDiff(IReadOnlyList<EzScoreRaceState> incoming)
        {
            var incomingIds = new HashSet<string>(incoming.Select(s => s.ScoreInfo.ID.ToString()));

            foreach (string key in states.Keys.ToList())
            {
                if (!incomingIds.Contains(key))
                    states.Remove(key);
            }

            foreach (var state in incoming)
            {
                string id = state.ScoreInfo.ID.ToString();

                if (states.TryGetValue(id, out var existing))
                {
                    if (ReferenceEquals(existing, state))
                        continue;

                    // 保留已有实例引用，避免 HUD processor 绑定失效。
                    if (state.Timeline != null)
                        existing.Timeline = state.Timeline;

                    continue;
                }

                states[id] = state;
            }
        }

        private string buildQueryKey(Guid beatmapId)
            => $"{beatmapId}|{ModFilter.Value}|{EzLocalScoreQueries.GetModFilterCacheFingerprint(ModFilter.Value, getCurrentMods())}|{MaxEntries.Value}";

        private void storeMetadataCache(string queryKey, List<EzScoreRaceState> statesToStore)
        {
            metadataCache[queryKey] = statesToStore;
            touchMetadataCacheLru(queryKey);

            while (metadataCacheLru.Count > metadata_cache_capacity)
            {
                string evictedKey = metadataCacheLru.Last!.Value;
                metadataCacheLru.RemoveLast();
                metadataCache.Remove(evictedKey);
            }
        }

        private void touchMetadataCacheLru(string queryKey)
        {
            if (metadataCacheLru.First is { Value: var headKey } && headKey == queryKey)
                return;

            for (var node = metadataCacheLru.First; node != null; node = node.Next)
            {
                if (node.Value != queryKey)
                    continue;

                metadataCacheLru.Remove(node);
                metadataCacheLru.AddFirst(node);
                return;
            }

            metadataCacheLru.AddFirst(queryKey);
        }

        private void evictMetadataCache(string queryKey)
        {
            metadataCache.Remove(queryKey);

            for (var node = metadataCacheLru.First; node != null; node = node.Next)
            {
                if (node.Value != queryKey)
                    continue;

                metadataCacheLru.Remove(node);
                return;
            }
        }

        private void cancelTimelineBuild()
        {
            timelineBuildVersion++;

            var cts = timelineBuildCts;
            timelineBuildCts = null;

            if (cts == null)
                return;

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

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                unsubscribeScreenHooks();
                cancelTimelineBuild();
                serviceEnabled.UnbindAll();
                feedMode.UnbindAll();
            }

            base.Dispose(isDisposing);
        }
    }
}
