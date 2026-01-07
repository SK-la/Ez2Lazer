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
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.LAsEzExtensions.Analysis.Persistence;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
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
    public partial class EzBeatmapManiaAnalysisCache : MemoryCachingComponent<EzBeatmapManiaAnalysisCache.ManiaAnalysisCacheLookup, ManiaBeatmapAnalysisResult?>
    {
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

        [Resolved]
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

                            if (persistentStore.TryGet(lookup.BeatmapInfo, requireXxySr: lookup.RequireXxySr, out var persisted, out bool missingRequiredXxy))
                            {
                                if (!missingRequiredXxy && persisted != null)
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
            var result = await GetAsync(lookup, cancellationToken, computationDelay).ConfigureAwait(false);

            if (result != null)
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
            var existing = inflightComputations.GetOrAdd(lookup, _ => computeValueInternalCore(lookup, token));

            if (!existing.IsCompleted)
            {
                // Reusing an in-flight computation; no instrumentation logging.
            }

            try
            {
                return await existing.ConfigureAwait(false);
            }
            finally
            {
                // Remove when completed (best-effort). If another request re-added concurrently, leave it.
                inflightComputations.TryRemove(new KeyValuePair<ManiaAnalysisCacheLookup, Task<ManiaBeatmapAnalysisResult?>>(lookup, existing));
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
                            if (persistentStore.TryGet(lookup.BeatmapInfo, requireXxySr: lookup.RequireXxySr, out var persisted, out bool missingRequiredXxy))
                            {
                                if (!missingRequiredXxy)
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
                            if (persistentStore.TryGet(lookup.BeatmapInfo, requireXxySr: lookup.RequireXxySr, out var persisted, out bool missingRequiredXxy))
                            {
                                if (!missingRequiredXxy)
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
                if (lookup.Ruleset.OnlineID != 3)
                    return null;

                // Persistent fast-path for no-mod baseline.
                if (lookup.OrderedMods.Length == 0 && persistentStore.TryGet(lookup.BeatmapInfo, requireXxySr: lookup.RequireXxySr, out var persisted, out bool missingRequiredXxy))
                {
                    persistHit = true;
                    if (!missingRequiredXxy)
                        return persisted;

                    // Baseline exists but xxy is missing -> compute xxy and patch.
                    var rulesetInstanceForXxy = lookup.Ruleset.CreateInstance();
                    if (!(rulesetInstanceForXxy is ILegacyRuleset))
                        return persisted;

                    PlayableCachedWorkingBeatmap workingBeatmapForXxy = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
                    var playableBeatmapForXxy = workingBeatmapForXxy.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    double? xxySrPatched = null;
                    if (playableBeatmapForXxy.HitObjects.Count > 0 && XxySrCalculatorBridge.TryCalculate(playableBeatmapForXxy, out double sr) && !double.IsNaN(sr) && !double.IsInfinity(sr))
                        xxySrPatched = sr;

                    var patched = new ManiaBeatmapAnalysisResult(
                        persisted.AverageKps,
                        persisted.MaxKps,
                        persisted.KpsList,
                        persisted.ColumnCounts,
                        persisted.HoldNoteCounts,
                        persisted.ScratchText,
                        xxySrPatched);

                    persistentStore.Store(lookup.BeatmapInfo, patched);
                    return patched;
                }

                var rulesetInstance = lookup.Ruleset.CreateInstance();
                if (!(rulesetInstance is ILegacyRuleset))
                    return null;

                var legacyRuleset = (ILegacyRuleset)rulesetInstance;
                int keyCount = legacyRuleset.GetKeyCount(lookup.BeatmapInfo, lookup.OrderedMods);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);

                double? xxySr = null;

                if (lookup.RequireXxySr)
                {
                    if (playableBeatmap.HitObjects.Count > 0 && XxySrCalculatorBridge.TryCalculate(playableBeatmap, out double sr) && !double.IsNaN(sr) && !double.IsInfinity(sr))
                        xxySr = sr;
                }

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
                    persistentStore.Store(lookup.BeatmapInfo, analysis);
                }

                return analysis;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
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

        public readonly struct ManiaAnalysisCacheLookup : IEquatable<ManiaAnalysisCacheLookup>
        {
            public readonly BeatmapInfo BeatmapInfo;
            public readonly RulesetInfo Ruleset;
            public readonly Mod[] OrderedMods;
            public readonly bool RequireXxySr;
            private readonly string[] modSignatures;

            public ManiaAnalysisCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IEnumerable<Mod>? mods, bool requireXxySr)
            {
                BeatmapInfo = beatmapInfo;
                Ruleset = ruleset;
                OrderedMods = mods?.OrderBy(m => m.Acronym).Select(mod => mod.DeepClone()).ToArray() ?? Array.Empty<Mod>();
                RequireXxySr = requireXxySr;
                modSignatures = OrderedMods.Select(createModSignature).ToArray();
            }

            public bool Equals(ManiaAnalysisCacheLookup other) => BeatmapInfo.ID.Equals(other.BeatmapInfo.ID)
                                                                  && string.Equals(BeatmapInfo.Hash, other.BeatmapInfo.Hash, StringComparison.Ordinal)
                                                                  && Ruleset.Equals(other.Ruleset)
                                                                  && RequireXxySr == other.RequireXxySr
                                                                  && modSignatures.SequenceEqual(other.modSignatures);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(BeatmapInfo.ID);
                hashCode.Add(BeatmapInfo.Hash);
                hashCode.Add(Ruleset.ShortName);
                hashCode.Add(RequireXxySr);

                foreach (string s in modSignatures)
                    hashCode.Add(s);

                return hashCode.ToHashCode();
            }

            private static string createModSignature(Mod m)
            {
                // Signature = TypeFullName + sorted list of setting_name=setting_value
                string type = m.GetType().FullName ?? m.GetType().Name;
                var settings = m.GetSettingsSourceProperties().Select(p => p.Item2).ToArray();
                if (settings.Length == 0)
                    return type;

                var pairs = settings.Select(prop =>
                {
                    var bindable = (IBindable)prop.GetValue(m)!;
                    object val = bindable.GetUnderlyingSettingValue();
                    return prop.Name + "=" + (val.ToString() ?? "");
                }).OrderBy(x => x);

                return type + ":" + string.Join(";", pairs);
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
