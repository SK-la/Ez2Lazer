// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 仅用于 mania 的选歌面板分析缓存（KPS/每列 notes/scratch）。
    /// 参考 <see cref="BeatmapDifficultyCache"/> 的中心化缓存模式：
    /// - 单线程 <see cref="ThreadedTaskScheduler"/> 控制后台计算并发，避免拖动滚动条时造成卡顿。
    /// - 默认本地优先（内存缓存/持久化）读取，避免快速切图时频繁重算。
    /// - 仅活动面板（由调用方显式传入 allowRecompute）执行实时重算并回写持久化。
    /// - 缓存 key 包含 mod 设置（依赖 mod 的相等性/哈希语义），避免"切/调 mod 不重算"。
    /// </summary>
    public partial class EzAnalysisCache : MemoryCachingComponent<EzAnalysisCacheLookup, EzAnalysisResult?>
    {
        private static int computeFailCount;

        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(EzAnalysisCache));
        private readonly WeakList<BindableManiaBeatmapAnalysis> trackedBindables = new WeakList<BindableManiaBeatmapAnalysis>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource computationsCancellationSource = new CancellationTokenSource();

        private readonly SemaphoreSlim persistenceReadGate = new SemaphoreSlim(4, 4);

        private readonly ConcurrentDictionary<EzAnalysisCacheLookup, Task<EzAnalysisResult?>> inflightComputations = new ConcurrentDictionary<EzAnalysisCacheLookup, Task<EzAnalysisResult?>>();

        private static readonly AsyncLocal<int> low_priority_scope_depth = new AsyncLocal<int>();

        /// <summary>
        /// 是否当前在低优先级预热流程中。
        /// </summary>
        private bool isLowPriorityFlow => low_priority_scope_depth.Value > 0;

        [Resolved]
        private EzAnalysisPersistentStore persistentStore { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private IBindable<bool> ezAnalysisEnabled = new BindableBool(true);
        private IBindable<bool> ezAnalysisSqliteEnabled = new BindableBool(true);

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private int modsRevision;

        // 限制内存缓存大小。
        // - 当持久化可用时，在内存中保持一个小的工作集（当前页面）。
        // - 当持久化不可用/禁用时，允许稍大的集合以减少重新计算。
        private const int max_in_memory_entries = 8;
        private readonly ConcurrentQueue<EzAnalysisCacheLookup> cacheInsertionOrder = new ConcurrentQueue<EzAnalysisCacheLookup>();

        // 我们通过保持自己的缓存键集合来避免修改官方的 MemoryCachingComponent。
        // 然后通过调用 Invalidate(key == oldest) 执行驱逐，这对基础缓存是 O(n) 的，
        private readonly ConcurrentDictionary<EzAnalysisCacheLookup, byte> cachedLookups = new ConcurrentDictionary<EzAnalysisCacheLookup, byte>();
        private int cachedLookupsCount;

        // 与 SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE 保持一致。
        private const int mod_settings_debounce = SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE + 10;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ezAnalysisEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisCacheEnabled);
            ezAnalysisSqliteEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled);

            ezAnalysisEnabled.BindValueChanged(v =>
            {
                if (!v.NewValue)
                {
                    disableAnalysisRuntime();
                    return;
                }

                Scheduler.AddOnce(updateTrackedBindables);
            }, true);

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
        /// 将当前异步流标记为低优先级（预热）。在返回的作用域内触发的任何缓存未命中
        /// 都将在低优先级调度程序上执行。
        /// </summary>
        public IDisposable BeginLowPriorityScope()
        {
            low_priority_scope_depth.Value++;
            return new InvokeOnDisposal(() => low_priority_scope_depth.Value--);
        }

        /// <summary>
        /// 仅预热持久化存储（无 mod 基线）而不填充内存缓存。
        /// 旨在启动时预热以避免在内存中保留大量结果集。
        /// </summary>
        public Task WarmupPersistentOnlyAsync(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            if (!ezAnalysisSqliteEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return Task.CompletedTask;

            // 仅支持 mania。
            if (beatmapInfo.Ruleset is not RulesetInfo rulesetInfo || rulesetInfo.OnlineID != 3)
                return Task.CompletedTask;

            // 始终以低优先级运行，从不与可见计算竞争。
            // 预热应通过 BeginLowPriorityScope 进行分类，以便其他代码可以正确区分预热流程（例如持久化读取门控）。
            // 此外，限制并发持久化读取以避免在预热期间淹没 SQLite。
            return Task.Factory.StartNew(() =>
            {
                // 将此异步流标记为低优先级以便在其他地方进行分类。
                using (BeginLowPriorityScope())
                {
                    var lookup = new EzAnalysisCacheLookup(beatmapInfo, rulesetInfo, mods: null);

                    // 首先，门控并探测持久化存储以避免淹没读取器。
                    bool persistedExists = false;

                    if (EzAnalysisPersistentStore.Enabled)
                    {
                        bool gateAcquired = false;

                        try
                        {
                            persistenceReadGate.Wait(cancellationToken);
                            gateAcquired = true;

                            if (persistentStore.TryGet(lookup.BeatmapInfo, out _))
                            {
                                persistedExists = true;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // 请求取消 - 中止此项的预热。
                            return;
                        }
                        catch
                        {
                            // 忽略持久化探测失败并继续计算。
                        }
                        finally
                        {
                            if (gateAcquired)
                            {
                                persistenceReadGate.Release();
                            }
                        }
                    }

                    // 仅当持久化条目尚不存在时才计算并存储基线。
                    if (!persistedExists)
                    {
                        computeAnalysis(lookup, cancellationToken);
                    }
                }
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        /// <summary>
        /// 获取面板分析 bindable。
        /// 默认仅本地读取；当 <paramref name="allowRecompute"/> 为 true 时允许实时重算。
        /// </summary>
        public IBindable<EzAnalysisResult> GetBindableAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0,
                                                               bool allowRecompute = false)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;

            var bindable = new BindableManiaBeatmapAnalysis(beatmapInfo, cancellationToken);

            if (localBeatmapInfo == null || (!ezAnalysisEnabled.Value && !ezAnalysisSqliteEnabled.Value))
                return bindable;

            updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay, allowRecompute);

            return bindable;
        }

        /// <summary>
        /// 获取分析结果。
        /// - allowRecompute=false：仅返回内存缓存/持久化结果（本地优先）。
        /// - allowRecompute=true：允许执行实时重算（用于活动 panel 纠偏）。
        /// </summary>
        public Task<EzAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo,
                                                        IRulesetInfo? rulesetInfo = null,
                                                        IEnumerable<Mod>? mods = null,
                                                        CancellationToken cancellationToken = default,
                                                        int computationDelay = 0,
                                                        bool allowRecompute = false)
        {
            // 两个开关都关闭时，既不读本地也不做重算。
            if (!ezAnalysisEnabled.Value && !ezAnalysisSqliteEnabled.Value)
                return Task.FromResult<EzAnalysisResult?>(null);

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = (rulesetInfo ?? beatmapInfo.Ruleset) as RulesetInfo;

            if (localBeatmapInfo == null || localRulesetInfo == null)
                return Task.FromResult<EzAnalysisResult?>(null);

            var lookup = new EzAnalysisCacheLookup(localBeatmapInfo, localRulesetInfo, mods);

            // 仅从本地内存/持久化读取，保证面板低开销展示。
            if (CheckExists(lookup, out var existing) && existing.HasValue)
                return Task.FromResult(existing);

            if (ezAnalysisSqliteEnabled.Value && EzAnalysisPersistentStore.Enabled && persistentStore.TryGet(localBeatmapInfo, out var persistedFallback))
                return Task.FromResult<EzAnalysisResult?>(persistedFallback);

            // 重算路径仅由 global 开关控制。
            if (allowRecompute && ezAnalysisEnabled.Value)
                return getAndMaybeEvictAsync(lookup, cancellationToken, computationDelay);

            return Task.FromResult<EzAnalysisResult?>(null);
        }

        public bool TryGetBaselineXxySr(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out double xxySr)
        {
            xxySr = 0;

            if (!ezAnalysisEnabled.Value && !ezAnalysisSqliteEnabled.Value)
                return false;

            if (beatmapInfo is not BeatmapInfo localBeatmapInfo)
                return false;

            var localRulesetInfo = (rulesetInfo ?? beatmapInfo.Ruleset) as RulesetInfo;

            if (localRulesetInfo == null || localRulesetInfo.OnlineID != 3)
                return false;

            var lookup = new EzAnalysisCacheLookup(localBeatmapInfo, localRulesetInfo, mods: null);

            if (CheckExists(lookup, out var existing) && existing?.EzManiaSummary.XxySr is double cachedXxySr)
            {
                xxySr = cachedXxySr;
                return true;
            }

            if (ezAnalysisSqliteEnabled.Value && EzAnalysisPersistentStore.Enabled && persistentStore.TryGet(localBeatmapInfo, out var persisted) && persisted.EzManiaSummary.XxySr is double persistedXxySr)
            {
                xxySr = persistedXxySr;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 在 panel 被销毁时主动清除其分析缓存，避免缓存堆积。
        /// 特别考虑快速滚动列表时，panel 可能被快速创建和销毁。
        /// </summary>
        public void InvalidateBeatmapAnalysis(BeatmapInfo? beatmapInfo)
        {
            if (beatmapInfo is null)
                return;

            // 从内存缓存中移除所有与此 beatmap 相关的条目
            var keysToRemove = cachedLookups.Keys.Where(k => k.BeatmapInfo.ID == beatmapInfo.ID).ToList();

            foreach (var key in keysToRemove)
            {
                if (cachedLookups.TryRemove(key, out _))
                {
                    Interlocked.Decrement(ref cachedLookupsCount);
                    Invalidate(k => k.Equals(key));
                }
            }
        }

        private async Task<EzAnalysisResult?> getAndMaybeEvictAsync(EzAnalysisCacheLookup lookup, CancellationToken cancellationToken, int computationDelay)
        {
            EzAnalysisResult? result = await GetAsync(lookup, cancellationToken, computationDelay).ConfigureAwait(false);

            if (result.HasValue)
            {
                // 仅在新 key 首次加入内存缓存时入队，避免重复命中导致队列无限增长。
                if (cachedLookups.TryAdd(lookup, 0))
                {
                    Interlocked.Increment(ref cachedLookupsCount);
                    cacheInsertionOrder.Enqueue(lookup);
                }

                while (Volatile.Read(ref cachedLookupsCount) > max_in_memory_entries && cacheInsertionOrder.TryDequeue(out var toRemove))
                {
                    if (!cachedLookups.TryRemove(toRemove, out _))
                        continue;

                    Interlocked.Decrement(ref cachedLookupsCount);
                    Invalidate(k => k.Equals(toRemove));
                }
            }

            return result;
        }

        /// <summary>
        /// 尝试从持久化存储获取分析结果。
        /// 根据当前的优先级作用域（预热 vs 可见）决定是否使用门控来限制数据库并发。
        /// </summary>
        private async Task<EzAnalysisResult?> tryGetPersistentResultAsync(BeatmapInfo beatmapInfo, CancellationToken cancellationToken)
        {
            try
            {
                // 预热流程（低优先级）需要门控以避免淹没 SQLite。
                if (isLowPriorityFlow)
                {
                    bool gateAcquired = false;

                    try
                    {
                        await persistenceReadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                        gateAcquired = true;

                        return await Task.Run<EzAnalysisResult?>(() => persistentStore.TryGet(beatmapInfo, out var persisted) ? persisted : null, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (gateAcquired)
                            persistenceReadGate.Release();
                    }
                }
                else
                {
                    // 可见流程（高优先级）跳过门控以避免 UI 停滞。
                    return await Task.Run<EzAnalysisResult?>(() => persistentStore.TryGet(beatmapInfo, out var persisted) ? persisted : null, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                // 忽略持久化失败并回退到计算。
                return null;
            }
        }

        protected override Task<EzAnalysisResult?> ComputeValueAsync(EzAnalysisCacheLookup lookup, CancellationToken token = default)
        {
            return computeValueWithDedupAsync(lookup, token);
        }

        private async Task<EzAnalysisResult?> computeValueWithDedupAsync(EzAnalysisCacheLookup lookup, CancellationToken token)
        {
            // 如果此查找的计算已经在进行中，则重用它。
            // 重要：计算本身不应被任何单个请求者取消。
            // 面板会被回收并积极取消其令牌（例如当离屏时），但我们仍然
            // 希望共享计算完成以便结果可以重用。
            var existing = inflightComputations.GetOrAdd(lookup, lookupKey =>
            {
                var task = computeValueInternalCore(lookup, computationsCancellationSource.Token);

                // 当任务完成时移除条目（尽力而为），但仅当值匹配时。
                task.ContinueWith(
                    completedTask => inflightComputations.TryRemove(new KeyValuePair<EzAnalysisCacheLookup, Task<EzAnalysisResult?>>(lookup, task)),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return task;
            });

            // 允许各个调用者取消等待而不取消共享计算。
            try
            {
                return await existing.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private async Task<EzAnalysisResult?> computeValueInternalCore(EzAnalysisCacheLookup lookup, CancellationToken token)
        {
            if (!ezAnalysisEnabled.Value)
                return null;

            // 快速路径：如果存在持久化的无 mod 基线，则返回它而不进入单线程计算队列。
            // 这避免了队头阻塞，其中一个昂贵的未命中会延迟许多便宜的命中。
            if (lookup.OrderedMods.Length == 0 && ezAnalysisSqliteEnabled.Value && EzAnalysisPersistentStore.Enabled)
            {
                var persistedResult = await tryGetPersistentResultAsync(lookup.BeatmapInfo, token).ConfigureAwait(false);
                if (persistedResult != null)
                    return persistedResult;
            }

            var scheduler = updateScheduler;

            var task = Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeAnalysis(lookup, token);
            }, token, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, scheduler);

            return await task.ConfigureAwait(false);
        }

        private EzAnalysisResult? computeAnalysis(EzAnalysisCacheLookup lookup, CancellationToken cancellationToken, bool allowPersistedFastPath = true)
        {
            try
            {
                bool persistenceEnabled = ezAnalysisSqliteEnabled.Value && EzAnalysisPersistentStore.Enabled;

                // 无mod快速路径：尝试直接从持久化存储获取结果
                if (allowPersistedFastPath && lookup.OrderedMods.Length == 0 && persistenceEnabled && persistentStore.TryGet(lookup.BeatmapInfo, out var persisted))
                {
                    return persisted;
                }

                var rulesetInstance = lookup.Ruleset.CreateInstance();

                if (!(rulesetInstance is ILegacyRuleset))
                    return null;

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);

                // 将速率调整 mods（DT/HT 等）应用于 KPS 值。
                // 这些 mods 影响有效时间进程，因此 KPS 应该随速率缩放。
                double rate = getRateAdjustMultiplier(lookup.OrderedMods);

                if (!Precision.AlmostEquals(rate, 1.0))
                {
                    averageKps *= rate;
                    maxKps *= rate;

                    for (int i = 0; i < kpsList.Count; i++)
                        kpsList[i] *= rate;
                }

                double? xxySr = null;

                bool shouldCalculateXxy = lookup.Ruleset.OnlineID == 3;

                if (shouldCalculateXxy && playableBeatmap.HitObjects.Count > 0 && XxySrCalculatorBridge.TryCalculate(playableBeatmap, rate, out double sr) && !double.IsNaN(sr)
                    && !double.IsInfinity(sr))
                    xxySr = sr;

                cancellationToken.ThrowIfCancellationRequested();

                kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

                var summary = new KpsSummary(averageKps, maxKps, kpsList);
                var details = new EzManiaSummary(columnCounts, holdNoteCounts, xxySr);
                var analysis = new EzAnalysisResult(summary, details);

                if (lookup.OrderedMods.Length == 0 && persistenceEnabled && analysis.EzManiaSummary.ColumnCounts.Count > 0)
                {
                    persistentStore.StoreIfDifferent(lookup.BeatmapInfo, analysis);
                }

                return analysis;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref computeFailCount) <= 10)
                {
                    string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                    Logger.Error(ex,
                        $"[EzBeatmapManiaAnalysisCache] computeAnalysis failed. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}",
                        Ez2ConfigManager.LOGGER_NAME);
                }

                return null;
            }
            finally
            {
                // 主动释放此谱面的 working beatmap 缓存，避免在选歌切换中累积大量已分析谱面的可玩对象驻留内存。
                // 已经被调用方持有的实例不受影响；这里只清理 manager 层面的可复用缓存引用。
                ((IWorkingBeatmapCache)beatmapManager).Invalidate(lookup.BeatmapInfo);
            }
        }

        private static double getRateAdjustMultiplier(Mod[] mods)
        {
            // 优先使用通用规则集机制：任何实现 IApplicableToRate 的 mod 都应该贡献。
            // 按 mod 顺序应用以匹配谱面转换/游戏应用语义。
            try
            {
                double rate = 1.0;

                for (int i = 0; i < mods.Length; i++)
                {
                    if (mods[i] is IApplicableToRate applicableToRate)
                        rate = applicableToRate.ApplyToRate(0, rate);
                }

                if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0)
                    return 1.0;

                return rate;
            }
            catch
            {
                return 1.0;
            }
        }

        private void updateTrackedBindables()
        {
            if (!ezAnalysisEnabled.Value)
                return;

            if (currentRuleset.Value == null)
                return;

            lock (bindableUpdateLock)
            {
                cancelTrackedBindableUpdate();

                foreach (var b in trackedBindables)
                {
                    // 兼容性路径：仅当外部主动向 trackedBindables 注册时才会触发。
                    // 目前 panel 调用链默认不注册 tracked bindable，主要走按需请求模式。
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
                                    int computationDelay = 0,
                                    bool allowRecompute = false)
        {
            if (!ezAnalysisEnabled.Value && !ezAnalysisSqliteEnabled.Value)
                return;

            // 如果 bindable 已经被取消，则什么都不做。
            if (cancellationToken.IsCancellationRequested)
                return;

            // 捕获 bindable 自己的取消令牌以在计划的回调中检查。
            // 我们应该只在 bindable 本身被处置/取消时才跳过设置值，
            // 而不是在新的 mod 变化触发另一个更新周期时。
            var bindableCancellationToken = bindable.CancellationToken;

            // 请求分析。仅当任务成功完成时才应用结果。
            _ = applyAsync();

            async Task applyAsync()
            {
                try
                {
                    var analysis = await GetAnalysisAsync(beatmapInfo, rulesetInfo, mods, cancellationToken, computationDelay, allowRecompute).ConfigureAwait(false);
                    if (analysis == null)
                        return;

                    Schedule(() =>
                    {
                        // 只检查 bindable 自己的令牌，而不是链接的更新令牌。
                        // 这确保我们总是应用最新结果，即使另一个更新周期开始了。
                        if (bindableCancellationToken.IsCancellationRequested)
                            return;

                        bindable.Value = analysis.Value;
                    });
                }
                catch (OperationCanceledException)
                {
                    // 忽略取消。
                }
                catch
                {
                    // 忽略失败；它们不应该使 UI 崩溃。
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();

            computationsCancellationSource.Cancel();
            computationsCancellationSource.Dispose();

            updateScheduler.Dispose();
        }

        private void disableAnalysisRuntime()
        {
            debouncedModSettingsChange?.Cancel();

            // 清理 mod 设置追踪器，停止对 mod 变化的监听
            modSettingChangeTracker?.Dispose();
            modSettingChangeTracker = null;

            cancelTrackedBindableUpdate();

            // 取消所有正在运行的计算，防止关闭后继续占用内存
            computationsCancellationSource.Cancel();
            computationsCancellationSource = new CancellationTokenSource();

            // 清空所有追踪的 bindables 及其引用
            lock (bindableUpdateLock)
            {
                trackedBindables.Clear();
            }

            inflightComputations.Clear();
            cachedLookups.Clear();
            Interlocked.Exchange(ref cachedLookupsCount, 0);

            while (cacheInsertionOrder.TryDequeue(out _))
            {
            }

            Invalidate(_ => true);
        }

        private class BindableManiaBeatmapAnalysis : Bindable<EzAnalysisResult>
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

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods) => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) =>
                playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

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
