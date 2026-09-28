// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.EzOsuGame.WarmUp;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Play
{
    /// <summary>
    /// Aggregates Ez-side <see cref="PlayerLoader"/> preparation work (quick rotation pool, skin warm-up, score race ghosts).
    /// </summary>
    /// <remarks>
    /// 皮肤预热不再有任何写死的 EzPro 路径表：
    /// <list type="bullet">
    /// <item>规则集侧：进入 PlayerLoader 就按当前皮肤产出 gameplay 组件离屏加载（见 <see cref="IEzGameplayWarmUpSource"/>），与 Player 异步加载并行。</item>
    /// <item>组件侧：HUD 等组件各自实现 <see cref="IEzGameplayWarmUp"/>，在自己的 BDL 里登记到 <see cref="EzGameplayWarmUpService"/>。</item>
    /// </list>
    /// <see cref="CanStartPlayer"/> 会等这两部分都完成（含超时兜底），因此转圈会一直转到当前皮肤创建完再进局。
    /// 重试（Player 退出回 PlayerLoader）不会再开新会话：同一皮肤的纹理已经在 store 缓存里，无需重跑。
    /// </remarks>
    public partial class EzPlayerLoaderStartGate : CompositeDrawable, IEzScoreRacePlayerStartGate
    {
        /// <summary>预热等待上限；坏皮肤/坏资源时超时放行，避免永久卡在转圈。</summary>
        private const double warm_up_timeout_ms = 5000;

        private readonly EzScoreRaceService? scoreRaceService;

        private bool loaderActive;
        private PlayerLoader? activeLoader;

        /// <summary>是否已开始等待预热。默认 <c>true</c>，仅在进入 PlayerLoader 时置 false。</summary>
        private bool warmUpFinished = true;

        private Task? warmUpCompletionTask;
        private double warmUpStartTime;
        private CancellationTokenSource? warmUpCts;
        private EzGameplayWarmUpHost? warmUpHost;

        public EzPlayerLoaderStartGate(EzScoreRaceService? scoreRaceService = null)
        {
            this.scoreRaceService = scoreRaceService;

            // 只作为预热宿主存在，自身不参与布局/输入。
            Size = Vector2.Zero;
            AlwaysPresent = true;
        }

        public bool CanStartPlayer =>
            warmUpFinished
            && (scoreRaceService?.CanStartPlayer ?? true)
            && !blocksQuickRotationPool();

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private EzGameplayWarmUpService warmUpService { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        /// <summary>
        /// 当前选择中的 mods。用它取共享谱面：选歌侧（标题栏 SR、轮换基线等）也是拿这一份去转换的，
        /// 键一致才能命中缓存，而不是自己再转一遍。
        /// </summary>
        [Resolved]
        private IBindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            game.ScreenStack.ScreenPushed += onScreenChanged;
            game.ScreenStack.ScreenExited += onScreenChanged;
            skinManager.SourceChanged += onSkinSourceChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                game.ScreenStack.ScreenPushed -= onScreenChanged;
                game.ScreenStack.ScreenExited -= onScreenChanged;
                skinManager.SourceChanged -= onSkinSourceChanged;
                cancelWarmUp();
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// 皮肤在预热期间才就绪（脚本皮肤是异步加载的，进场时可能只拿到 loading fallback）时重来一遍：
        /// 否则预热会花在旧皮肤上，真正要用的皮肤还得进局现解码。
        /// </summary>
        /// <remarks>
        /// 皮肤启用 / 脚本重载的回调可能不在 update 线程（脚本皮肤是在后台加载完才写 <c>CurrentSkin</c> 的），
        /// 所以这里只做一次调度，真正的摘宿主 / 建树都留在 update 线程上。
        /// </remarks>
        private void onSkinSourceChanged() => Schedule(restartWarmUpForCurrentSkin);

        private void restartWarmUpForCurrentSkin()
        {
            if (warmUpFinished || warmUpCts == null)
                return;

            try
            {
                startRulesetWarmUp(warmUpCts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzPlayerLoaderStartGate] Failed to restart ruleset warm-up after skin change: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
            }
        }

        private void onScreenChanged(IScreen last, IScreen next)
        {
            if (next is PlayerLoader)
            {
                // 关键时序：ScreenPushed 早于 PlayerLoader（连同其 Player 树）的加载。
                // 必须在这里同步开会话——Player 树是在后台线程加载的，HUD 组件会在自己的 BDL 里登记，
                // 若把开会话推迟到 Update 里，后台加载可能抢先登记、随后被 BeginSession 清掉，预热就漏了。
                beginWarmUpSession();
                Schedule(() => beginLoaderPreparation(next as PlayerLoader));
            }
            else if (last is PlayerLoader)
                Schedule(endLoaderPreparation);
        }

        /// <summary>
        /// 开一次进图预热会话：重置等待状态、结束上一局残留登记。
        /// </summary>
        private void beginWarmUpSession()
        {
            cancelWarmUp();

            warmUpCts = new CancellationTokenSource();
            warmUpService.BeginSession();

            warmUpFinished = false;
            warmUpStartTime = Time.Current;
            warmUpCompletionTask = null;
        }

        private void beginLoaderPreparation(PlayerLoader? loader)
        {
            loaderActive = true;
            activeLoader = loader;

            var session = EzQuickRotationCoordinator.Session;

            if (session.IsActive)
                session.StartPoolBuild(beatmaps);

            try
            {
                // 规则集侧预热与 Player 异步加载并行开始：这样当前皮肤 gameplay 组件的解码
                // 有机会在 Player 自己创建它们之前完成，命中同一份纹理缓存。
                if (warmUpCts != null)
                    startRulesetWarmUp(warmUpCts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzPlayerLoaderStartGate] Failed to start ruleset warm-up: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                completeWarmUp("ruleset warm-up could not start");
            }
        }

        private void endLoaderPreparation()
        {
            loaderActive = false;
            activeLoader = null;
            cancelWarmUp();
        }

        private void startRulesetWarmUp(CancellationToken token)
        {
            // 重来（皮肤变更）时先把上一个宿主摘掉，避免两棵预热树同时解码。
            disposeWarmUpHost();

            Drawable? warmUpDrawable = createRulesetWarmUpDrawable();

            if (warmUpDrawable == null)
                return;

            warmUpHost = new EzGameplayWarmUpHost(warmUpDrawable);
            warmUpService.Register(warmUpHost);

            // 宿主必须真正被更新才会让 note 系列走完 Scheduler.AddOnce 里的纹理创建。
            _ = loadWarmUpHostAsync(warmUpHost, token);
        }

        private async Task loadWarmUpHostAsync(EzGameplayWarmUpHost host, CancellationToken token)
        {
            try
            {
                await LoadComponentAsync(host, AddInternal, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzPlayerLoaderStartGate] Ruleset warm-up host load failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                warmUpService.Unregister(host);
                host.ForceComplete();
                host.Expire();
            }
        }

        private Drawable? createRulesetWarmUpDrawable()
        {
            IWorkingBeatmap? working = workingBeatmap.Value;

            if (working is null or DummyWorkingBeatmap || working.BeatmapInfo?.Ruleset is not { } rulesetInfo)
                return null;

            if (rulesetInfo.CreateInstance() is not IEzGameplayWarmUpSource source)
                return null;

            Skin? currentSkin = skinManager.CurrentSkin.Value;

            if (currentSkin == null)
                return null;

            try
            {
                return source.CreateWarmUpDrawable(currentSkin, working.BeatmapInfo, selectedMods.Value);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzPlayerLoaderStartGate] Failed to create ruleset warm-up drawable: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (warmUpFinished)
                return;

            if (Time.Current - warmUpStartTime > warm_up_timeout_ms)
            {
                completeWarmUp("timed out");
                return;
            }

            if (warmUpCompletionTask != null)
            {
                if (warmUpCompletionTask.IsCompleted)
                {
                    if (warmUpCompletionTask.Exception != null)
                        Logger.Log($"[EzPlayerLoaderStartGate] Texture warm-up task faulted: {warmUpCompletionTask.Exception.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);

                    completeWarmUp(null);
                }

                return;
            }

            // 等 Player 树加载到 Ready 再收口：此时 Player 子树（含皮肤布局里的 HUD 组件）的
            // BDL 已经跑完，它们在自己的 BDL 里登记的自述预热都已进入服务。
            // WarmUpAllAsync 还会继续重复快照，兜住更晚才登记进来的组件。
            if (activeLoader?.CurrentPlayer?.LoadState != LoadState.Ready)
                return;

            warmUpCompletionTask = warmUpService.WarmUpAllAsync(warmUpCts!.Token);
        }

        private void completeWarmUp(string? reason)
        {
            if (warmUpFinished)
                return;

            if (reason != null)
                Logger.Log($"[EzPlayerLoaderStartGate] Texture warm-up released early ({reason}).", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

            // 取消未完成的等待并释放预热宿主；纹理本身已进入 store 缓存，不受影响。
            cancelWarmUp();
        }

        private void cancelWarmUp()
        {
            warmUpFinished = true;
            warmUpCompletionTask = null;

            warmUpCts?.Cancel();
            warmUpCts?.Dispose();
            warmUpCts = null;

            warmUpService.EndSession();
            disposeWarmUpHost();
        }

        private void disposeWarmUpHost()
        {
            if (warmUpHost == null)
                return;

            warmUpService.Unregister(warmUpHost);

            // 宿主被换掉/销毁时立刻放行它自己那份等待，否则还在等它的 WarmUpAllAsync 只能干等到取消或超时。
            warmUpHost.ForceComplete();
            warmUpHost.Expire();
            warmUpHost = null;
        }

        private bool blocksQuickRotationPool()
        {
            if (!loaderActive)
                return false;

            var session = EzQuickRotationCoordinator.Session;
            return session.IsActive && !session.IsPoolReady;
        }
    }
}
