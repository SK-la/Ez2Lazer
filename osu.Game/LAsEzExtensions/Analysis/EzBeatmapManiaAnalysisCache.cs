// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.LAsEzExtensions.Analysis
{
    /// <summary>
    /// 仅用于 mania 的选歌面板分析缓存（KPS/每列 notes/scratch）。
    /// 参考 <see cref="BeatmapDifficultyCache"/> 的中心化缓存模式：
    /// - 单线程 <see cref="ThreadedTaskScheduler"/> 控制后台计算并发，避免拖动滚动条时造成卡顿。
    /// - 统一监听当前 ruleset/mods 及 mod 设置变化，批量更新所有已追踪的 bindable。
    /// - 缓存 key 包含 mod 设置（依赖 mod 的相等性/哈希语义），避免“切/调 mod 不重算”。
    /// </summary>
    public partial class EzBeatmapManiaAnalysisCache : MemoryCachingComponent<EzBeatmapManiaAnalysisCache.ManiaAnalysisCacheLookup, ManiaBeatmapAnalysisResult?>
    {
        // 太多同时更新会导致卡顿；这里保持与 star cache 一样的策略：只用一条后台线程。
        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(EzBeatmapManiaAnalysisCache));

        private readonly WeakList<BindableManiaBeatmapAnalysis> trackedBindables = new WeakList<BindableManiaBeatmapAnalysis>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private BeatmapStore beatmapStore { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        private ProgressNotification? warmupNotification;
        private long lastWarmupNotificationUpdate;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private int modsRevision;

        private bool warmupRequested;
        private CancellationTokenSource? warmupCancellationSource;
        private Task? warmupTask;
        private string? warmupSignature;

        // 最近一次“前台/交互请求”（例如可见面板绑定）的时间戳。用于避免 warmup 抢占计算队列。
        private long lastInteractiveRequest;

        // 与 SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE 保持一致（150ms）。
        private const int mod_settings_debounce = 150;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentRuleset.BindValueChanged(_ => Scheduler.AddOnce(updateTrackedBindables));

            currentMods.BindValueChanged(mods =>
            {
                Interlocked.Increment(ref modsRevision);
                modSettingChangeTracker?.Dispose();

                Scheduler.AddOnce(updateTrackedBindables);

                modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                modSettingChangeTracker.SettingChanged += _ =>
                {
                    Interlocked.Increment(ref modsRevision);
                    debouncedModSettingsChange?.Cancel();
                    debouncedModSettingsChange = Scheduler.AddDelayed(updateTrackedBindables, mod_settings_debounce);
                };
            }, true);
        }

        protected override bool CacheNullValues => false;

        public IBindable<ManiaBeatmapAnalysisResult> GetBindableAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;

            var bindable = new BindableManiaBeatmapAnalysis(beatmapInfo, cancellationToken)
            {
                Value = ManiaBeatmapAnalysisDefaults.EMPTY
            };

            if (localBeatmapInfo == null)
                return bindable;

            updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay);

            lock (bindableUpdateLock)
                trackedBindables.Add(bindable);

            // 第一次被 UI 触发时开启“全曲库预热”。
            warmupRequested = true;
            ensureWarmupRunning();

            return bindable;
        }

        public Task<ManiaBeatmapAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo,
                                                                  IRulesetInfo? rulesetInfo = null,
                                                                  IEnumerable<Mod>? mods = null,
                                                                  CancellationToken cancellationToken = default,
                                                                  int computationDelay = 0)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = (rulesetInfo ?? beatmapInfo.Ruleset) as RulesetInfo;

            if (localBeatmapInfo == null || localRulesetInfo == null)
                return Task.FromResult<ManiaBeatmapAnalysisResult?>(null);

            return GetAsync(new ManiaAnalysisCacheLookup(localBeatmapInfo, localRulesetInfo, mods), cancellationToken, computationDelay);
        }

        protected override Task<ManiaBeatmapAnalysisResult?> ComputeValueAsync(ManiaAnalysisCacheLookup lookup, CancellationToken token = default)
        {
            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeAnalysis(lookup, token);
            }, token, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        private ManiaBeatmapAnalysisResult? computeAnalysis(in ManiaAnalysisCacheLookup lookup, CancellationToken cancellationToken)
        {
            try
            {
                if (lookup.Ruleset.OnlineID != 3)
                    return null;

                var rulesetInstance = lookup.Ruleset.CreateInstance();
                if (rulesetInstance is not ILegacyRuleset legacyRuleset)
                    return null;

                int keyCount = legacyRuleset.GetKeyCount(lookup.BeatmapInfo, lookup.OrderedMods);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                var (averageKps, maxKps, kpsList, columnCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);

                // 同一次 playable beatmap 里顺带计算 xxy_SR（只在异常/失败时写 xxy_sr 日志）。
                double? xxySr = null;
                if (playableBeatmap.HitObjects.Count == 0)
                {
                    string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                    Logger.Log($"xxy_SR aborted: playableBeatmap has 0 hitobjects. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}", "xxy_sr", LogLevel.Error);
                }
                else if (XxySrCalculatorBridge.TryCalculate(playableBeatmap, out double sr))
                {
                    if (double.IsNaN(sr) || double.IsInfinity(sr))
                    {
                        Logger.Log($"xxy_SR returned invalid value (NaN/Infinity). beatmapId={lookup.BeatmapInfo.ID} ruleset={lookup.Ruleset.ShortName}", "xxy_sr", LogLevel.Error);
                    }
                    else
                    {
                        xxySr = sr;

                        if (sr < 0 || sr > 1000)
                        {
                            string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                            Logger.Log($"xxy_SR abnormal value: {sr}. hitobjects={playableBeatmap.HitObjects.Count} beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}", "xxy_sr", LogLevel.Error);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                string scratchText = EzBeatmapCalculator.GetScratchFromPrecomputed(columnCounts, maxKps, kpsList, keyCount);

                return new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
                    scratchText,
                    xxySr);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                // 忽略：选歌面板快速滚动/拖动时，失败不应影响 UI。
                return null;
            }
        }

        private void updateTrackedBindables()
        {
            lock (bindableUpdateLock)
            {
                cancelTrackedBindableUpdate();

                // 规则集变化到非 mania 时，不触发后台计算。
                // 由面板侧自行决定是否显示/清空。
                if (currentRuleset.Value.OnlineID != 3)
                    return;

                foreach (var b in trackedBindables)
                {
                    var localBeatmapInfo = b.BeatmapInfo as BeatmapInfo;
                    if (localBeatmapInfo == null)
                        continue;

                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, b.CancellationToken);
                    linkedCancellationSources.Add(linkedSource);

                    updateBindable(b, localBeatmapInfo, currentRuleset.Value, currentMods.Value, linkedSource.Token);
                }
            }

            // ruleset / mods 变化后，重启 warmup（若已请求）。
            ensureWarmupRunning();
        }

        private void cancelTrackedBindableUpdate()
        {
            lock (bindableUpdateLock)
            {
                trackedUpdateCancellationSource.Cancel();
                trackedUpdateCancellationSource = new CancellationTokenSource();

                foreach (var c in linkedCancellationSources)
                    c.Dispose();

                linkedCancellationSources.Clear();
            }
        }

        private void updateBindable(BindableManiaBeatmapAnalysis bindable,
                                    BeatmapInfo beatmapInfo,
                                    IRulesetInfo? rulesetInfo,
                                    IEnumerable<Mod>? mods,
                                    CancellationToken cancellationToken = default,
                                    int computationDelay = 0)
        {
            // 记录交互请求时间，用于 warmup 节流。
            lastInteractiveRequest = Environment.TickCount64;

            // 关键点：计算不应被单个 UI 项（DrawablePool / 离屏）取消，否则快速滚动会导致结果永远算不出来/无法填充缓存。
            // 这里将“计算”与“写回 bindable”解耦：
            // - 计算一律使用 CancellationToken.None，以便后台完成并写入 MemoryCachingComponent 的缓存。
            // - cancellationToken 仅用于控制是否将结果写回到 bindable（避免旧 Item 收到回调）。
            GetAnalysisAsync(beatmapInfo, rulesetInfo, mods, CancellationToken.None, computationDelay)
                .ContinueWith(task =>
                {
                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        var analysis = task.GetResultSafely();
                        if (analysis != null)
                            bindable.Value = analysis.Value;
                    });
                }, cancellationToken);
        }

        private void ensureWarmupRunning()
        {
            if (!warmupRequested)
                return;

            if (currentRuleset.Value.OnlineID != 3)
            {
                cancelWarmup();
                return;
            }

            string signature = getWarmupSignature(currentRuleset.Value, currentMods.Value);
            if (warmupSignature == signature && warmupTask is { IsCompleted: false })
                return;

            cancelWarmup();

            warmupSignature = signature;
            warmupCancellationSource = new CancellationTokenSource();
            var token = warmupCancellationSource.Token;

            // 在后台线程顺序遍历全曲库，逐个计算并填充缓存。
            warmupTask = Task.Run(() => runWarmup(signature, token), token);
        }

        private void cancelWarmup()
        {
            warmupSignature = null;

            // Mark any existing notification as stale/cancelled.
            if (warmupNotification != null)
            {
                var notification = warmupNotification;
                warmupNotification = null;

                Scheduler.Add(() =>
                {
                    notification.State = ProgressNotificationState.Cancelled;
                });
            }

            warmupCancellationSource?.Cancel();
            warmupCancellationSource?.Dispose();
            warmupCancellationSource = null;

            warmupTask = null;
        }

        private string getWarmupSignature(RulesetInfo ruleset, IReadOnlyList<Mod> mods)
        {
            // 需要随着 mod 以及其设置变化而变化。
            // - modsRevision 确保“设置变化但 hash 不变”时也会重启 warmup。
            // - GetHashCode() 作为额外近似来源，避免不必要的重复重启。
            string modsSig = mods.Count == 0
                ? "(none)"
                : string.Join("|", mods.OrderBy(m => m.Acronym).Select(m => $"{m.Acronym}:{m.GetHashCode()}"));

            int revision = Volatile.Read(ref modsRevision);
            return $"ruleset={ruleset.OnlineID};modsRev={revision};mods={modsSig}";
        }

        private void runWarmup(string signature, CancellationToken token)
        {
            try
            {
                // 获取全曲库快照（线程安全的 detached store）。
                var beatmapSets = beatmapStore.GetBeatmapSets(token).ToList();

                var beatmaps = beatmapSets
                              .SelectMany(s => s.Beatmaps)
                              // 仅预热 Mania 原生谱面（避免对非 mania 进行转换预热导致成本爆炸）。
                              .Where(b => !b.Hidden && b.Ruleset.OnlineID == 3)
                              .ToList();

                int totalCount = beatmaps.Count;

                Scheduler.Add(() =>
                {
                    warmupNotification = showWarmupProgressNotification(totalCount);
                    lastWarmupNotificationUpdate = Environment.TickCount64;
                });

                int processedCount = 0;

                foreach (var beatmap in beatmaps)
                {
                    token.ThrowIfCancellationRequested();

                    if (warmupSignature != signature)
                        return;

                    // 节流：只要用户还在频繁触发交互请求，就暂停 warmup。
                    long sinceInteractive = Environment.TickCount64 - lastInteractiveRequest;
                    if (sinceInteractive < 250)
                    {
                        int sleep = (int)Math.Clamp(250 - sinceInteractive, 1, 250);
                        Thread.Sleep(sleep);
                        continue;
                    }

                    // 触发一次计算并进入缓存；不使用 token 取消计算。
                    // 注意：GetAnalysisAsync 内部会通过 updateScheduler 串行执行，整体不会爆并发。
                    GetAnalysisAsync(beatmap, currentRuleset.Value, currentMods.Value, CancellationToken.None).GetAwaiter().GetResult();
                    processedCount++;

                    // 通知更新节流：避免每首歌都更新 UI。
                    if (processedCount == totalCount || processedCount % 25 == 0)
                    {
                        long now = Environment.TickCount64;
                        if (now - lastWarmupNotificationUpdate > 250)
                        {
                            lastWarmupNotificationUpdate = now;
                            Scheduler.Add(() => updateWarmupProgressNotification(warmupNotification, processedCount, totalCount));
                        }
                    }
                }

                Scheduler.Add(() => completeWarmupProgressNotification(warmupNotification, processedCount, totalCount));
                Scheduler.Add(() => warmupNotification = null);
            }
            catch (OperationCanceledException)
            {
                Scheduler.Add(() => completeWarmupProgressNotification(warmupNotification, 0, 0, cancelled: true));
                Scheduler.Add(() => warmupNotification = null);
            }
            catch (Exception ex)
            {
                // warmup 不应影响游戏运行，仅记录（避免刷屏）。
                Logger.Error(ex, "EzBeatmapManiaAnalysisCache warmup failed.");

                Scheduler.Add(() => completeWarmupProgressNotification(warmupNotification, 0, 0, failed: true));
                Scheduler.Add(() => warmupNotification = null);
            }
        }

        private ProgressNotification? showWarmupProgressNotification(int totalCount)
        {
            if (notificationOverlay == null)
                return null;

            // 太少不值得显示。
            if (totalCount < 10)
                return null;

            // 通知文案定义位置：
            // - Text：任务进行中在通知里显示的主文本（后续会在 updateWarmupProgressNotification() 动态追加 "(processed of total)"）。
            // - CompletionText：任务完成后显示的文本（completeWarmupProgressNotification() 会在前面加上处理数量）。
            var notification = new ProgressNotification
            {
                Text = "Precomputing mania analysis…",
                CompletionText = "mania analysis precomputed.",
                State = ProgressNotificationState.Active,
                Progress = 0
            };

            notificationOverlay.Post(notification);
            return notification;
        }

        private void updateWarmupProgressNotification(ProgressNotification? notification, int processedCount, int totalCount)
        {
            if (notification == null)
                return;

            if (totalCount <= 0)
                return;

            notification.Text = $"Precomputing mania analysis… ({processedCount} of {totalCount})";
            notification.Progress = (float)processedCount / totalCount;
        }

        private void completeWarmupProgressNotification(ProgressNotification? notification, int processedCount, int totalCount, bool cancelled = false, bool failed = false)
        {
            if (notification == null)
                return;

            if (failed)
            {
                notification.Text = "Precomputing mania analysis failed. Check logs.";
                notification.State = ProgressNotificationState.Cancelled;
                return;
            }

            if (cancelled)
            {
                notification.Text = "Precomputing mania analysis cancelled.";
                notification.State = ProgressNotificationState.Cancelled;
                return;
            }

            if (totalCount > 0 && processedCount >= totalCount)
            {
                notification.CompletionText = $"{processedCount} {notification.CompletionText}";
                notification.Progress = 1;
                notification.State = ProgressNotificationState.Completed;
            }
            else
            {
                notification.Text = $"{processedCount} of {totalCount} {notification.CompletionText}";
                notification.State = ProgressNotificationState.Cancelled;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            updateScheduler.Dispose();
        }

        public readonly struct ManiaAnalysisCacheLookup : IEquatable<ManiaAnalysisCacheLookup>
        {
            public readonly BeatmapInfo BeatmapInfo;
            public readonly RulesetInfo Ruleset;
            public readonly Mod[] OrderedMods;

            public ManiaAnalysisCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IEnumerable<Mod>? mods)
            {
                BeatmapInfo = beatmapInfo;
                Ruleset = ruleset;
                OrderedMods = mods?.OrderBy(m => m.Acronym).Select(mod => mod.DeepClone()).ToArray() ?? Array.Empty<Mod>();
            }

            public bool Equals(ManiaAnalysisCacheLookup other)
                => BeatmapInfo.Equals(other.BeatmapInfo)
                   && Ruleset.Equals(other.Ruleset)
                   && OrderedMods.SequenceEqual(other.OrderedMods);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(BeatmapInfo.ID);
                hashCode.Add(Ruleset.ShortName);

                foreach (var mod in OrderedMods)
                    hashCode.Add(mod);

                return hashCode.ToHashCode();
            }
        }

        private class BindableManiaBeatmapAnalysis : Bindable<ManiaBeatmapAnalysisResult>
        {
            public readonly IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;

            public BindableManiaBeatmapAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken)
            {
                BeatmapInfo = beatmapInfo;
                CancellationToken = cancellationToken;
            }
        }

        private class PlayableCachedWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private IBeatmap? playable;

            public PlayableCachedWorkingBeatmap(IWorkingBeatmap working)
            {
                this.working = working;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => working.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => working.BeatmapLoaded;
            bool IWorkingBeatmap.TrackLoaded => working.TrackLoaded;
            IBeatmap IWorkingBeatmap.Beatmap => working.Beatmap;
            Texture IWorkingBeatmap.GetBackground() => working.GetBackground();
            Texture IWorkingBeatmap.GetPanelBackground() => working.GetPanelBackground();
            Waveform IWorkingBeatmap.Waveform => working.Waveform;
            Storyboard IWorkingBeatmap.Storyboard => working.Storyboard;
            ISkin IWorkingBeatmap.Skin => working.Skin;
            Track IWorkingBeatmap.Track => working.Track;
            Track IWorkingBeatmap.LoadTrack() => working.LoadTrack();
            Stream IWorkingBeatmap.GetStream(string storagePath) => working.GetStream(storagePath);
            void IWorkingBeatmap.BeginAsyncLoad() => working.BeginAsyncLoad();
            void IWorkingBeatmap.CancelAsyncLoad() => working.CancelAsyncLoad();
            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double offsetFromPreviewPoint) => working.PrepareTrackForPreview(looping, offsetFromPreviewPoint);
        }
    }
}
