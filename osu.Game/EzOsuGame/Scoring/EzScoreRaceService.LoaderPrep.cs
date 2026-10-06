// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Scoring
{
    public partial class EzScoreRaceService : IEzScoreRacePlayerStartGate
    {
        /// <inheritdoc/>
        public bool CanStartPlayer => !isBlockingPlayerLoaderStart();

        private bool loaderPreparationActive;
        private bool loaderPreparationPending;

        /// <summary>
        /// 本轮 loading 是否已按运行时注册完成过补建裁决，避免每帧重复触发。
        /// </summary>
        private bool demandResolvedFromConsumers;

        /// <summary>
        /// 角逐工作是否应进行：服务开启，且「静态预测」（<see cref="demand"/>）或
        /// 「运行时实际注册」（<see cref="hasConsumers"/>）任一表明存在消费者。
        ///
        /// 取或的原因：预测为真时不必等注册，构建可与 <see cref="Player"/> 装载并行；
        /// 预测漏判（规则集漂移等）时由运行时注册补救。预测为真但实际无消费者时不取消，
        /// 避免注册晚到被误杀；其代价仅为 loading 内一次查询与一次构建，不进入局内。
        /// </summary>
        private bool shouldPerformScoreRaceWork => ShouldPerformScoreRaceWork(isServiceActive, demand, hasConsumers);

        /// <summary>
        /// 无消费者时与「服务未加载」不可区分，是这套门控的硬不变量。
        /// </summary>
        internal static bool ShouldPerformScoreRaceWork(bool serviceActive, bool predicted, bool hasConsumers)
            => serviceActive && (predicted || hasConsumers);

        /// <summary>
        /// 需求重算后的唯一副作用：需求消失才回到静默态。
        /// 需求出现必须返回 false，否则「判定」会变成「启动构建」，破坏上面的不变量。
        /// </summary>
        internal static bool ShouldEnterQuiescentState(bool previousDemand, bool currentDemand) => previousDemand && !currentDemand;

        /// <summary>
        /// 是否应在 loading 末尾按运行时注册补建。每轮 loading 只允许一次（<paramref name="alreadyResolved"/> 后不再重复），
        /// 且必须等 <see cref="Player"/> 完全装载（皮肤组件已定）才可下结论。
        /// 只决定「补建」，不决定「取消」—— 预测为真而注册为 0 时不撤销已有构建。
        /// </summary>
        internal static bool ShouldResolveDemandFromConsumers(bool alreadyResolved, bool hasConsumers, bool playerFullyLoaded)
            => !alreadyResolved && hasConsumers && playerFullyLoaded;

        /// <summary>
        /// 静态预测漏判的补救。角逐 HUD 在 <see cref="SkinnableContainer"/> 里是嵌套异步装载，
        /// 其注册落地时刻不保证早于进局，故不能在 <see cref="PlayerLoader"/> 进入时就下结论；
        /// 等 <see cref="Player"/> 装载完成（皮肤组件已定）后，若 <see cref="RegisterInterest"/> 已表明
        /// 存在消费者，则在 loading 内补做 Realm 查询与 timeline 构建。
        ///
        /// 只在 loading 期间生效：<see cref="loaderPreparationActive"/> 为假（已进游戏）时不会被调用，
        /// 因此进局后不会发生任何查询或构建；每轮 loading 只裁决一次。
        /// </summary>
        private void tryResolveDemandFromConsumers()
        {
            if (!ShouldResolveDemandFromConsumers(demandResolvedFromConsumers, hasConsumers, activePlayerLoader?.CurrentPlayer?.LoadState == LoadState.Ready))
                return;

            demandResolvedFromConsumers = true;

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            requestTimelineBuild(priority: true);
        }

        /// <summary>
        /// <see cref="PlayerLoader"/> 进入：预测存在消费者时立即拉取 ghost 元数据并全速构建 timeline，
        /// 直至 <see cref="CanStartPlayer"/> 为 true。
        ///
        /// 预测为假时不预先构建，但仍保持 loader 准备态，以便
        /// <see cref="tryResolveDemandFromConsumers"/> 在 Player 装载完成后按运行时注册补建。
        /// </summary>
        private void beginLoaderPreparation()
        {
            if (!isServiceActive)
                return;

            loaderPreparationActive = true;
            demandResolvedFromConsumers = false;

            if (!demand)
                return;

            loaderPreparationPending = true;

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            loaderPreparationPending = false;
            requestTimelineBuild(priority: true);
        }

        /// <summary>
        /// <see cref="PlayerLoader"/> 退出：结束 loader 门控；返回选歌时取消在途 build。
        /// </summary>
        private void endLoaderPreparation(bool advancingToPlayer)
        {
            loaderPreparationActive = false;
            loaderPreparationPending = false;
            demandResolvedFromConsumers = false;
            activePlayerLoader = null;

            if (!advancingToPlayer)
                cancelTimelineBuild();
        }

        private bool isBlockingPlayerLoaderStart()
        {
            if (!loaderPreparationActive)
                return false;

            // 必须在 StreamByClock 早退之前调用：该模式只是不阻塞进局，仍需要补建 timeline。
            tryResolveDemandFromConsumers();

            // StreamByClock：不阻塞进局；timeline 后台就绪后由 HUD 插值接管。
            if (feedMode.Value == EzReplayFeedMode.StreamByClock)
                return false;

            if (loaderPreparationPending)
                return true;

            if (!requiresGhostTimelinePreparation())
                return false;

            return !areAllGhostTimelinesReady();
        }

        private bool requiresGhostTimelinePreparation()
        {
            var beatmapInfo = currentBeatmap.Value?.BeatmapInfo;

            if (beatmapInfo == null || !EzScoreRaceRulesetSupport.SupportsGhostRace(beatmapInfo.Ruleset))
                return false;

            return states.Count > 0;
        }

        private bool areAllGhostTimelinesReady()
        {
            if (states.Count == 0)
                return true;

            if (IsTimelineBuildInProgress)
                return false;

            return states.Values.All(s => s.Timeline != null);
        }
    }
}
