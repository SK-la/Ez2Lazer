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
using osu.Game.LAsEzExtensions.Analysis.Persistence;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.SelectV2;
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
    public partial class EzBeatmapManiaAnalysisCache : MemoryCachingComponent<ManiaAnalysisCacheLookup, ManiaBeatmapAnalysisResult?>
    {
        private static int computeFailCount;

        // (Removed runtime instrumentation counters)
        // 太多同时更新会导致卡顿；官方 star cache 使用 1 线程，但我们可以尝试略微提高并发以加快可见项响应。
        // 这里将高优先级并发从 1 增加到 2 来观察是否能减少感知延迟，同时保留低优先级为 1。
        private readonly ThreadedTaskScheduler highPriorityScheduler = new ThreadedTaskScheduler(2, nameof(EzBeatmapManiaAnalysisCache));
        private readonly ThreadedTaskScheduler lowPriorityScheduler = new ThreadedTaskScheduler(1, $"{nameof(EzBeatmapManiaAnalysisCache)} (Warmup)");

        private readonly ManualResetEventSlim highPriorityIdleEvent = new ManualResetEventSlim(true);

        // A small gate to avoid flooding SQLite with too many concurrent readers.
        private readonly SemaphoreSlim persistenceReadGate = new SemaphoreSlim(4, 4);

        // Deduplicate in-flight computations so multiple requests for the same lookup reuse the same Task
        private readonly ConcurrentDictionary<ManiaAnalysisCacheLookup, Task<ManiaBeatmapAnalysisResult?>> inflightComputations =
            new ConcurrentDictionary<ManiaAnalysisCacheLookup, Task<ManiaBeatmapAnalysisResult?>>();

        private static readonly AsyncLocal<int> low_priority_scope_depth = new AsyncLocal<int>();

        private readonly WeakList<BindableManiaBeatmapAnalysis> trackedBindables = new WeakList<BindableManiaBeatmapAnalysis>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private EzManiaAnalysisPersistentStore persistentStore { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private int modsRevision;

        // Limit in-memory cache size.
        // - When persistence is available, keep a small working set (current page) in memory.
        // - When persistence is unavailable/disabled, allow a slightly larger set to reduce recomputation.
        private const int max_in_memory_entries_with_persistence = 24;
        private const int max_in_memory_entries_without_persistence = 48;
        private readonly ConcurrentQueue<ManiaAnalysisCacheLookup> cacheInsertionOrder = new ConcurrentQueue<ManiaAnalysisCacheLookup>();

        // We avoid modifying the official MemoryCachingComponent by keeping our own set of cached keys.
        // Eviction is then performed by calling Invalidate(key == oldest), which is O(n) over the base cache,
        // but n is bounded to a small working set (24/48) so this is fine.
        private readonly ConcurrentDictionary<ManiaAnalysisCacheLookup, byte> cachedLookups = new ConcurrentDictionary<ManiaAnalysisCacheLookup, byte>();
        private int cachedLookupsCount;

        // 与 SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE 保持一致。
        private const int mod_settings_debounce = SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE;

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
            // Warmup should be classified via BeginLowPriorityScope so that other code can
            // correctly distinguish warmup flows (e.g. persistence read gating). Additionally,
            // limit concurrent persistent reads to avoid flooding SQLite during warmup.
            return Task.Factory.StartNew(() =>
            {
                // Mark this async flow as low-priority for classification elsewhere.
                using (BeginLowPriorityScope())
                {
                    highPriorityIdleEvent.Wait(cancellationToken);

                    // No mods: only baseline is persisted.
                    var lookup = new ManiaAnalysisCacheLookup(beatmapInfo, rulesetInfo, mods: null, requireXxySr: false);

                    // First, gate and probe the persistent store to avoid flooding readers.
                    bool persistedExists = false;

                    if (EzManiaAnalysisPersistentStore.Enabled)
                    {
                        bool gateAcquired = false;

                        try
                        {
                            persistenceReadGate.Wait(cancellationToken);
                            gateAcquired = true;

                            if (persistentStore.TryGet(lookup.BeatmapInfo, out _))
                            {
                                // xxysr 补算机制已禁用，直接返回持久化结果
                                persistedExists = true;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // cancellation requested - abort warmup for this item.
                            return;
                        }
                        catch
                        {
                            // ignore persistence probe failures and fall through to compute.
                        }
                        finally
                        {
                            if (gateAcquired)
                            {
                                try { persistenceReadGate.Release(); }
                                catch { }
                            }
                        }
                    }

                    // Only compute and store baseline if a persisted entry does not already exist.
                    if (!persistedExists)
                    {
                        try
                        {
                            computeAnalysis(lookup, cancellationToken, out bool _);
                        }
                        catch (OperationCanceledException)
                        {
                            // ignore cancellations during warmup
                        }
                        catch
                        {
                            // ignore failures; warmup should not crash.
                        }
                    }
                }
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, lowPriorityScheduler);
        }

        public IBindable<ManiaBeatmapAnalysisResult> GetBindableAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0, bool requireXxySr = false)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;

            var bindable = new BindableManiaBeatmapAnalysis(beatmapInfo, cancellationToken)
            {
                Value = ManiaBeatmapAnalysisDefaults.EMPTY
            };

            if (localBeatmapInfo == null)
                return bindable;

            updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay, requireXxySr);

            lock (bindableUpdateLock)
                trackedBindables.Add(bindable);

            return bindable;
        }

        public Task<ManiaBeatmapAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo,
                                                                  IRulesetInfo? rulesetInfo = null,
                                                                  IEnumerable<Mod>? mods = null,
                                                                  CancellationToken cancellationToken = default,
                                                                  int computationDelay = 0,
                                                                  bool requireXxySr = false)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = (rulesetInfo ?? beatmapInfo.Ruleset) as RulesetInfo;

            if (localBeatmapInfo == null || localRulesetInfo == null)
                return Task.FromResult<ManiaBeatmapAnalysisResult?>(null);

            var lookup = new ManiaAnalysisCacheLookup(localBeatmapInfo, localRulesetInfo, mods, requireXxySr);

            return getAndMaybeEvictAsync(lookup, cancellationToken, computationDelay);
        }

        private async Task<ManiaBeatmapAnalysisResult?> getAndMaybeEvictAsync(ManiaAnalysisCacheLookup lookup, CancellationToken cancellationToken, int computationDelay)
        {
            ManiaBeatmapAnalysisResult? result = await GetAsync(lookup, cancellationToken, computationDelay).ConfigureAwait(false);

            if (result.HasValue)
            {
                cacheInsertionOrder.Enqueue(lookup);

                if (cachedLookups.TryAdd(lookup, 0))
                    Interlocked.Increment(ref cachedLookupsCount);

                int maxEntries = EzManiaAnalysisPersistentStore.Enabled
                    ? max_in_memory_entries_with_persistence
                    : max_in_memory_entries_without_persistence;

                while (Volatile.Read(ref cachedLookupsCount) > maxEntries && cacheInsertionOrder.TryDequeue(out var toRemove))
                {
                    if (!cachedLookups.TryRemove(toRemove, out _))
                        continue;

                    Interlocked.Decrement(ref cachedLookupsCount);
                    Invalidate(k => k.Equals(toRemove));
                }
            }

            return result;
        }

        protected override Task<ManiaBeatmapAnalysisResult?> ComputeValueAsync(ManiaAnalysisCacheLookup lookup, CancellationToken token = default)
        {
            return computeValueWithDedupAsync(lookup, token);
        }

        private async Task<ManiaBeatmapAnalysisResult?> computeValueWithDedupAsync(ManiaAnalysisCacheLookup lookup, CancellationToken token)
        {
            // If a computation for this lookup is already in-flight, reuse it.
            // Important: the computation itself should not be cancelled by any single requester.
            // Panels get recycled and cancel their tokens aggressively (eg. when off-screen), but we still
            // want shared computations to complete so results can be reused.
            var existing = inflightComputations.GetOrAdd(lookup, lookupKey =>
            {
                var task = computeValueInternalCore(lookup, CancellationToken.None);

                // Remove the entry when the task completes (best-effort), but only if the value matches.
                task.ContinueWith(
                    completedTask => inflightComputations.TryRemove(new KeyValuePair<ManiaAnalysisCacheLookup, Task<ManiaBeatmapAnalysisResult?>>(lookup, task)),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return task;
            });

            // Allow individual callers to cancel waiting without cancelling the shared computation.
            try
            {
                return await existing.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private async Task<ManiaBeatmapAnalysisResult?> computeValueInternalCore(ManiaAnalysisCacheLookup lookup, CancellationToken token)
        {
            // Quick path: if a persisted no-mod baseline exists, return it without entering the single-thread compute queue.
            // This avoids head-of-line blocking where one expensive miss would delay many cheap hits.
            if (lookup.OrderedMods.Length == 0 && EzManiaAnalysisPersistentStore.Enabled)
            {
                // Use low_priority_scope_depth to distinguish warmup/background flows from visible flows.
                // Warmup flows will call BeginLowPriorityScope and set the async-local depth > 0.
                // We gate only warmup/background flows to limit DB concurrency; visible flows use
                // the fast-path read to avoid UI stalls.
                if (low_priority_scope_depth.Value > 0)
                {
                    bool gateAcquired = false;

                    try
                    {
                        await persistenceReadGate.WaitAsync(token).ConfigureAwait(false);
                        gateAcquired = true;

                        var persistedResult = await Task.Run<ManiaBeatmapAnalysisResult?>(() =>
                        {
                            if (persistentStore.TryGet(lookup.BeatmapInfo, out var persisted))
                            {
                                // xxysr 补算机制已禁用，直接返回持久化结果
                                return persisted;
                            }

                            return null;
                        }, token).ConfigureAwait(false);

                        if (persistedResult != null)
                            return persistedResult;
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                    catch
                    {
                        // Ignore persistence failures and fall back to compute.
                    }
                    finally
                    {
                        if (gateAcquired)
                            persistenceReadGate.Release();
                    }
                }
                else
                {
                    try
                    {
                        var persistedResult = await Task.Run<ManiaBeatmapAnalysisResult?>(() =>
                        {
                            if (persistentStore.TryGet(lookup.BeatmapInfo, out var persisted))
                            {
                                // xxysr 补算机制已禁用，直接返回持久化结果
                                return persisted;
                            }

                            return null;
                        }, token).ConfigureAwait(false);

                        if (persistedResult != null)
                            return persistedResult;
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                    catch
                    {
                        // Ignore persistence failures and fall back to compute.
                    }
                }
            }

            bool isLowPriority = low_priority_scope_depth.Value > 0;
            var scheduler = isLowPriority ? lowPriorityScheduler : highPriorityScheduler;

            var task = Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                // Warmup: never compete with visible/high-priority computations.
                if (isLowPriority)
                    highPriorityIdleEvent.Wait(token);

                return computeAnalysis(lookup, token, out _);
            }, token, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, scheduler);

            return await task.ConfigureAwait(false);
        }

        // enter/exit high-priority work helpers removed; gating uses ManualResetEventSlim directly.

        private ManiaBeatmapAnalysisResult? computeAnalysis(ManiaAnalysisCacheLookup lookup, CancellationToken cancellationToken, out bool persistHit)
        {
            persistHit = false;

            try
            {
                // Persistent fast-path for no-mod baseline.
                if (lookup.OrderedMods.Length == 0 && persistentStore.TryGet(lookup.BeatmapInfo, out var persisted))
                {
                    persistHit = true;
                    // xxysr 补算机制已禁用：缓存中任意参数丢失都算缓存未命中要重算。
                    // 直接返回已持久化的结果，无需补算。
                    return persisted;
                }

                var rulesetInstance = lookup.Ruleset.CreateInstance();

                if (!(rulesetInstance is ILegacyRuleset))
                    return null;

                var legacyRuleset = (ILegacyRuleset)rulesetInstance;
                int keyCount = 0;

                try
                {
                    keyCount = legacyRuleset.GetKeyCount(lookup.BeatmapInfo, lookup.OrderedMods);
                }
                catch
                {
                    // Some mod combinations may cause key count derivation to fail.
                    // We'll fall back to inferring key count from the playable beatmap.
                    keyCount = 0;
                }

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                int inferredKeyCount = getKeyCountFromBeatmap(playableBeatmap);
                if (keyCount <= 0)
                    keyCount = inferredKeyCount;
                else if (inferredKeyCount > keyCount)
                    keyCount = inferredKeyCount;

                cancellationToken.ThrowIfCancellationRequested();

                var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);

                // Apply rate-adjust mods (DT/HT etc.) to KPS values.
                // These mods affect effective time progression, so KPS should scale with rate.
                double rate = getRateAdjustMultiplier(lookup.OrderedMods);

                if (!Precision.AlmostEquals(rate, 1.0))
                {
                    averageKps *= rate;
                    maxKps *= rate;

                    for (int i = 0; i < kpsList.Count; i++)
                        kpsList[i] *= rate;
                }

                // Only calculate xxySr for mania mode (OnlineID == 3)
                double? xxySr = null;

                if (lookup.Ruleset.OnlineID == 3 && playableBeatmap.HitObjects.Count > 0 && XxySrCalculatorBridge.TryCalculate(playableBeatmap, out double sr) && !double.IsNaN(sr) && !double.IsInfinity(sr))
                    xxySr = sr;

                cancellationToken.ThrowIfCancellationRequested();

                string scratchText = EzBeatmapCalculator.GetScratchFromPrecomputed(columnCounts, maxKps, kpsList, keyCount);

                kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

                var analysis = new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
                    holdNoteCounts,
                    scratchText,
                    xxySr);

                if (lookup.OrderedMods.Length == 0)
                {
                    // Skip storing empty beatmaps (no notes) to avoid unnecessary database entries
                    if (analysis.ColumnCounts.Count > 0)
                    {
                        // For baseline (no-mod), compare with stored data and update if different
                        // This ensures data completeness (e.g., xxySr gets patched when previously null)
                        persistentStore.StoreIfDifferent(lookup.BeatmapInfo, analysis);
                    }
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
                    Logger.Error(ex, $"[EzBeatmapManiaAnalysisCache] computeAnalysis failed. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}");
                }

                return null;
            }
        }

        private static double getRateAdjustMultiplier(Mod[] mods)
        {
            // Prefer the generic ruleset mechanism: any mod implementing IApplicableToRate should contribute.
            // Apply in mod order to match beatmap conversion / gameplay application semantics.
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

        private static int getKeyCountFromBeatmap(IBeatmap beatmap)
        {
            try
            {
                int maxColumn = beatmap.HitObjects.OfType<IHasColumn>().Select(h => h.Column).DefaultIfEmpty(-1).Max();
                return maxColumn + 1;
            }
            catch
            {
                return 0;
            }
        }

        private void updateTrackedBindables()
        {
            if (currentRuleset.Value == null)
                return;

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

                    updateBindable(b, localBeatmapInfo, currentRuleset.Value, currentMods.Value, linkedSource.Token, requireXxySr: false);
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
                                    bool requireXxySr = false)
        {
            // If the bindable is already cancelled, do nothing.
            if (cancellationToken.IsCancellationRequested)
                return;

            // Request the analysis. Apply the result only when the task completes successfully.
            _ = applyAsync();

            async Task applyAsync()
            {
                try
                {
                    var analysis = await GetAnalysisAsync(beatmapInfo, rulesetInfo, mods, cancellationToken, computationDelay, requireXxySr).ConfigureAwait(false);
                    if (analysis == null)
                        return;

                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        bindable.Value = analysis.Value;
                    });
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellations.
                }
                catch
                {
                    // Ignore failures; they should not crash the UI.
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            highPriorityScheduler.Dispose();
            lowPriorityScheduler.Dispose();
            highPriorityIdleEvent.Dispose();
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
