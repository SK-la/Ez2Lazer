// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
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
using osu.Game.LAsEzExtensions.Analysis.Persistence;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using System.Collections.Concurrent;

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
        // 另外 warmup 需要“永远不阻塞可见项”，因此 warmup 使用独立的低优先级调度器。
        private readonly ThreadedTaskScheduler highPriorityScheduler = new ThreadedTaskScheduler(1, nameof(EzBeatmapManiaAnalysisCache));
        private readonly ThreadedTaskScheduler lowPriorityScheduler = new ThreadedTaskScheduler(1, $"{nameof(EzBeatmapManiaAnalysisCache)} (Warmup)");

        private readonly ManualResetEventSlim highPriorityIdleEvent = new ManualResetEventSlim(true);
        private int highPriorityWorkCount;

        private static readonly AsyncLocal<int> low_priority_scope_depth = new AsyncLocal<int>();

        private readonly WeakList<BindableManiaBeatmapAnalysis> trackedBindables = new WeakList<BindableManiaBeatmapAnalysis>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private EzManiaAnalysisPersistentStore persistentStore { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private int modsRevision;

        // When persistence is disabled, keep only a bounded amount of in-memory results.
        // When persistence is enabled, also limit cache size to prevent memory growth during extensive browsing.
        // This prevents unbounded memory growth when scrolling through many beatmaps.
        private const int max_in_memory_entries_without_persistence = 128;
        private readonly ConcurrentQueue<ManiaAnalysisCacheLookup> nonPersistentCacheInsertionOrder = new ConcurrentQueue<ManiaAnalysisCacheLookup>();

        // 与 SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE 保持一致（150ms）。
        private const int mod_settings_debounce = 150;

        // xxy_sr 错误日志节流：每秒最多记录一次
        private DateTimeOffset lastXxySrErrorLogTime = DateTimeOffset.MinValue;
        private readonly object logLock = new object();

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

        /// <summary>
        /// Marks the current async flow as low-priority (warmup). Any cache misses triggered within the returned scope
        /// will be executed on the low-priority scheduler.
        /// </summary>
        public IDisposable BeginLowPriorityScope()
        {
            low_priority_scope_depth.Value++;
            return new InvokeOnDisposal(() => low_priority_scope_depth.Value--);
        }

        /// <summary>
        /// Blocks until there are no pending/running high-priority (visible) computations.
        /// Intended for warmup to avoid competing with visible content.
        /// </summary>
        public void WaitForHighPriorityIdle(CancellationToken cancellationToken = default)
        {
            highPriorityIdleEvent.Wait(cancellationToken);
        }

        /// <summary>
        /// Warm up the persistent store only (no-mod baseline) without populating the in-memory cache.
        /// Intended for startup warmup to avoid retaining a large set of results in memory.
        /// </summary>
        public Task WarmupPersistentOnlyAsync(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            if (!EzManiaAnalysisPersistentStore.Enabled)
                return Task.CompletedTask;

            // Only mania is supported.
            if (beatmapInfo.Ruleset is not RulesetInfo rulesetInfo || rulesetInfo.OnlineID != 3)
                return Task.CompletedTask;

            // Always run as low-priority and never compete with visible computations.
            return Task.Factory.StartNew(() =>
            {
                highPriorityIdleEvent.Wait(cancellationToken);

                // No mods: only baseline is persisted.
                var lookup = new ManiaAnalysisCacheLookup(beatmapInfo, rulesetInfo, mods: null);

                // computeAnalysis() will early-return persisted values and will store baseline results when computed.
                computeAnalysis(lookup, cancellationToken);
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, lowPriorityScheduler);
        }

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

            var lookup = new ManiaAnalysisCacheLookup(localBeatmapInfo, localRulesetInfo, mods);

            return getAndMaybeEvictAsync(lookup, cancellationToken, computationDelay);
        }

        private async Task<ManiaBeatmapAnalysisResult?> getAndMaybeEvictAsync(ManiaAnalysisCacheLookup lookup, CancellationToken cancellationToken, int computationDelay)
        {
            var result = await GetAsync(lookup, cancellationToken, computationDelay).ConfigureAwait(false);

            // 即使在持久化模式下，也要限制内存缓存大小，避免内存无限增长
            // 持久化模式主要用于预计算，但内存缓存仍需限制以应对大量谱面浏览
            if (result != null)
            {
                nonPersistentCacheInsertionOrder.Enqueue(lookup);

                // 持久化模式下使用更小的缓存限制（因为主要依赖SQLite）
                int maxEntries = EzManiaAnalysisPersistentStore.Enabled ? 48 : max_in_memory_entries_without_persistence;

                while (CacheCount > maxEntries && nonPersistentCacheInsertionOrder.TryDequeue(out var toRemove))
                    TryRemove(toRemove);
            }

            return result;
        }

        protected override Task<ManiaBeatmapAnalysisResult?> ComputeValueAsync(ManiaAnalysisCacheLookup lookup, CancellationToken token = default)
        {
            bool isLowPriority = low_priority_scope_depth.Value > 0;
            var scheduler = isLowPriority ? lowPriorityScheduler : highPriorityScheduler;

            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                // Warmup: never compete with visible/high-priority computations.
                if (isLowPriority)
                    highPriorityIdleEvent.Wait(token);

                if (!isLowPriority)
                    enterHighPriorityWork();

                try
                {
                    return computeAnalysis(lookup, token);
                }
                finally
                {
                    if (!isLowPriority)
                        exitHighPriorityWork();
                }
            }, token, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, scheduler);
        }

        private void enterHighPriorityWork()
        {
            if (Interlocked.Increment(ref highPriorityWorkCount) == 1)
                highPriorityIdleEvent.Reset();
        }

        private void exitHighPriorityWork()
        {
            if (Interlocked.Decrement(ref highPriorityWorkCount) == 0)
                highPriorityIdleEvent.Set();
        }

        private ManiaBeatmapAnalysisResult? computeAnalysis(in ManiaAnalysisCacheLookup lookup, CancellationToken cancellationToken)
        {
            try
            {
                if (lookup.Ruleset.OnlineID != 3)
                    return null;

                // 持久化仅对 no-mod 生效：
                // - 避免 mod 组合/设置导致存储爆炸。
                // - 与官方 star 的“基础值可预处理、modded 按需计算”体验对齐。
                if (lookup.OrderedMods.Length == 0 && persistentStore.TryGet(lookup.BeatmapInfo, out var persisted))
                    return persisted;

                var rulesetInstance = lookup.Ruleset.CreateInstance();
                if (rulesetInstance is not ILegacyRuleset legacyRuleset)
                    return null;

                int keyCount = legacyRuleset.GetKeyCount(lookup.BeatmapInfo, lookup.OrderedMods);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);

                // 同一次 playable beatmap 里顺带计算 xxy_SR（只在其他数据成功但 xxy_sr 计算出错时记录日志）。
                double? xxySr = null;

                if (playableBeatmap.HitObjects.Count > 0)
                {
                    if (XxySrCalculatorBridge.TryCalculate(playableBeatmap, out double sr))
                    {
                        if (double.IsNaN(sr) || double.IsInfinity(sr))
                        {
                            logXxySrError($"xxy_SR returned invalid value (NaN/Infinity). beatmapId={lookup.BeatmapInfo.ID} ruleset={lookup.Ruleset.ShortName}");
                        }
                        else
                        {
                            xxySr = sr;

                            if (sr < 0 || sr > 1000)
                            {
                                string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                                logXxySrError($"xxy_SR abnormal value: {sr}. hitobjects={playableBeatmap.HitObjects.Count} beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}");
                            }
                        }
                    }
                    else
                    {
                        logXxySrError($"xxy_SR calculation failed. beatmapId={lookup.BeatmapInfo.ID} ruleset={lookup.Ruleset.ShortName}");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                string scratchText = EzBeatmapCalculator.GetScratchFromPrecomputed(columnCounts, maxKps, kpsList, keyCount);

                var analysis = new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
                    holdNoteCounts,
                    scratchText,
                    xxySr);

                if (lookup.OrderedMods.Length == 0)
                    persistentStore.Store(lookup.BeatmapInfo, analysis);

                return analysis;
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
                    // 只重算仍“活跃”的 bindable：离屏/回收的面板会取消 token。
                    // 这样可以确保计算预算优先服务当前可见内容。
                    if (b.CancellationToken.IsCancellationRequested)
                        continue;

                    var localBeatmapInfo = b.BeatmapInfo as BeatmapInfo;
                    if (localBeatmapInfo == null)
                        continue;

                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, b.CancellationToken);
                    linkedCancellationSources.Add(linkedSource);

                    updateBindable(b, localBeatmapInfo, currentRuleset.Value, currentMods.Value, linkedSource.Token);
                }
            }
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
            // bindable 已失效（离屏/回收）时，不再触发计算/写回。
            if (cancellationToken.IsCancellationRequested)
                return;

            // Persistent mode: decouple computation from UI cancellation so results can be cached/persisted even if the item is rapidly scrolled off-screen.
            // Non-persistent mode: only compute for visible items to prevent unbounded in-memory growth.
            var computeToken = EzManiaAnalysisPersistentStore.Enabled ? CancellationToken.None : cancellationToken;

            GetAnalysisAsync(beatmapInfo, rulesetInfo, mods, computeToken, computationDelay)
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

        private void logXxySrError(string message)
        {
            lock (logLock)
            {
                if ((DateTimeOffset.Now - lastXxySrErrorLogTime).TotalSeconds > 1)
                {
                    Logger.Log(message, "xxy_sr", LogLevel.Error);
                    lastXxySrErrorLogTime = DateTimeOffset.Now;
                }
            }
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
                => BeatmapInfo.ID.Equals(other.BeatmapInfo.ID)
                   && string.Equals(BeatmapInfo.Hash, other.BeatmapInfo.Hash, StringComparison.Ordinal)
                   && Ruleset.Equals(other.Ruleset)
                   && OrderedMods.SequenceEqual(other.OrderedMods);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(BeatmapInfo.ID);
                hashCode.Add(BeatmapInfo.Hash);
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
