// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Lists;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌 Ez 分析前台缓存；<see cref="Ez2Setting.EzAnalysisRecEnabled"/> 控制本 cache 是否对 SQLite 体系指标做即时计算。
    ///
    /// - L1 Realm 基线 xxy/PP：UI 直读 <see cref="BeatmapInfo"/>，不经本 cache；Panel PP 应对齐官方 <see cref="BeatmapDifficultyCache"/>。
    /// - L2 主 SQLite（<see cref="Ez2Setting.EzAnalysisSqliteEnabled"/>）：NoMod kps/KPC 切片；只读，不含 xxy/PP。
    /// - L3 本 cache（受 Rec 开关）：当前 ruleset/mods 下即时计算 <see cref="EzAnalysisResult"/>（kps、mod xxy 等）；Rec 关则仅 L2 预填/回退。
    /// 分支库 xxy/PP 为预生成快照，由 <see cref="EzAnalysisDatabase"/> 读取；Rec 关时不为分支场景做选歌补算。
    /// NoMod 语义对齐 <see cref="BeatmapDifficultyCache"/>：优先读 L2 已存切片，缺失时在内存补算而不落盘。
    /// <para>
    /// 本 cache 是纯读组件：任何落盘（主 SQLite 切片、Realm 值行与基线列）都由 BDSP、<see cref="BeatmapUpdater"/> 或设置里的手动重算发起。
    /// 不承担启动预热、Realm 回填或分支库构建。
    /// </para>
    /// </summary>
    /// <summary>
    /// The single place every analysis consumer goes through. Schedule (debounce, cancellation, tracked bindables)
    /// is its job; the values themselves live in <see cref="EzAnalysisLiveCache"/>, which is shared with the
    /// skill provider so there is one session store rather than one per component.
    /// </summary>
    public partial class EzAnalysisCache : Component, IEzChartAnalysisLookup
    {
        private static int computeFailCount;

        private readonly EzAnalysisLiveCache liveMetrics;

        public EzAnalysisCache(EzAnalysisLiveCache? liveMetrics = null)
        {
            this.liveMetrics = liveMetrics ?? new EzAnalysisLiveCache();
        }

        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(EzAnalysisCache));
        private readonly WeakList<BindableBeatmapEzAnalysis> trackedBindables = new WeakList<BindableBeatmapEzAnalysis>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly Lock bindableUpdateLock = new Lock();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();
        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private readonly IBindable<bool> runtimeAnalysisEnabled = new Bindable<bool>();

        private const int mod_settings_debounce = SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE + 10;

        [Resolved]
        private EzAnalysisDatabase analysisDatabase { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            runtimeAnalysisEnabled.BindTo(ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            runtimeAnalysisEnabled.BindValueChanged(v =>
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
                if (mods.OldValue.SequenceEqual(mods.NewValue, ReferenceEqualityComparer.Instance))
                    return;

                modSettingChangeTracker?.Dispose();

                Scheduler.AddOnce(updateTrackedBindables);

                modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                modSettingChangeTracker.SettingChanged += _ =>
                {
                    lock (bindableUpdateLock)
                    {
                        debouncedModSettingsChange?.Cancel();
                        debouncedModSettingsChange = Scheduler.AddDelayed(updateTrackedBindables, mod_settings_debounce);
                    }
                };
            }, true);
        }

        public void Invalidate(IBeatmapInfo oldBeatmap, IBeatmapInfo newBeatmap)
        {
            liveMetrics.InvalidateSubject(oldBeatmap.Hash);

            lock (bindableUpdateLock)
            {
                bool trackedBindablesRefreshRequired = false;

                foreach (var bindable in trackedBindables.Where(b => b.BeatmapInfo.Equals(oldBeatmap)))
                {
                    bindable.BeatmapInfo = newBeatmap;
                    trackedBindablesRefreshRequired = true;
                }

                if (trackedBindablesRefreshRequired)
                    Scheduler.AddOnce(updateTrackedBindables);
            }
        }

        public IBindable<EzAnalysisResult> GetBindableAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)        {
            var bindable = new BindableBeatmapEzAnalysis(beatmapInfo, cancellationToken);

            // NoMod：仅预填主 SQLite 的 kps/KPC 切片；xxy/PP 占位由面板直读 Realm（对齐 StarRating）。
            if (currentMods.Value.Count == 0
                && beatmapInfo is BeatmapInfo seedBeatmapInfo
                && analysisDatabase.TryGetStoredSqliteSlice(seedBeatmapInfo, currentRuleset.Value, out var storedSlice))
                bindable.Value = storedSlice;

            if (beatmapInfo is BeatmapInfo localBeatmapInfo)
                updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay);

            lock (bindableUpdateLock)
                trackedBindables.Add(bindable);

            return bindable;
        }

        internal int GetTrackedBindableCount()
        {
            lock (bindableUpdateLock)
            {
                int count = 0;

                foreach (var _ in trackedBindables)
                    count++;

                return count;
            }
        }

        public virtual Task<EzAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            rulesetInfo ??= beatmapInfo.Ruleset;

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = (rulesetInfo as RulesetInfo) ?? localBeatmapInfo?.Ruleset;

            if (runtimeAnalysisEnabled.Value && localBeatmapInfo != null && localRulesetInfo != null)
            {
                if (!analysisDatabase.IsSqliteAnalysisEnabled)
                    return GetDynamicAnalysisAsync(localBeatmapInfo, localRulesetInfo, mods, cancellationToken, computationDelay);

                return getDynamicWithStoredFallbackAsync(localBeatmapInfo, localRulesetInfo);
            }

            if (localBeatmapInfo != null
                && EzAnalysisDatabase.CanUseStoredAnalysis(localBeatmapInfo, rulesetInfo, mods)
                && analysisDatabase.TryGetStoredSqliteSlice(beatmapInfo, rulesetInfo, out var storedSlice))
                return Task.FromResult<EzAnalysisResult?>(storedSlice);

            return Task.FromResult<EzAnalysisResult?>(null);

            async Task<EzAnalysisResult?> getDynamicWithStoredFallbackAsync(BeatmapInfo dynamicBeatmapInfo, RulesetInfo dynamicRulesetInfo)
            {
                var dynamicAnalysis = await GetDynamicAnalysisAsync(dynamicBeatmapInfo, dynamicRulesetInfo, mods, cancellationToken, computationDelay).ConfigureAwait(false);

                if (dynamicAnalysis != null)
                    return dynamicAnalysis;

                if (analysisDatabase.IsSqliteAnalysisEnabled
                    && EzAnalysisDatabase.CanUseStoredAnalysis(dynamicBeatmapInfo, dynamicRulesetInfo, mods)
                    && analysisDatabase.TryGetStoredSqliteSlice(dynamicBeatmapInfo, dynamicRulesetInfo, out var fallbackStoredSlice))
                    return fallbackStoredSlice;

                return null;
            }
        }

        /// <summary>
        /// Live analysis that ignores <see cref="Ez2Setting.EzAnalysisRecEnabled"/> and the stored SQLite slice.
        /// For surfaces that must always show current-mod values; the setting only gates the song-select warm-up.
        /// </summary>
        public Task<EzAnalysisResult?> GetLiveAnalysisAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                            CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            rulesetInfo ??= beatmapInfo.Ruleset;

            if (beatmapInfo is BeatmapInfo localBeatmapInfo
                && ((rulesetInfo as RulesetInfo) ?? localBeatmapInfo.Ruleset) is { } localRulesetInfo)
                return GetDynamicAnalysisAsync(localBeatmapInfo, localRulesetInfo, mods, cancellationToken, computationDelay);

            return Task.FromResult<EzAnalysisResult?>(null);
        }

        public bool TryGetXxySr(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out double xxySr)
            => analysisDatabase.TryGetXxySr(beatmapInfo, rulesetInfo, out xxySr);

        /// <summary>Cached result for this chart and mods; never computes.</summary>
        public EzAnalysisResult? TryGet(BeatmapInfo beatmap, IReadOnlyList<Mod>? mods)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            return liveMetrics.Get(new EzAnalysisLiveCache.AnalysisKey(beatmap.Hash, beatmap.Ruleset.OnlineID, EzAnalysisLookupCache.ComputeModsSignature(mods)));
        }

        /// <summary>
        /// Cached result, or a synchronous pass into the shared store when absent.
        /// </summary>
        /// <remarks>
        /// Batch callers only (player aggregation). The pass is heavy and runs on the update scheduler, so calling
        /// this from draw/update would stall the frame.
        /// </remarks>
        public EzAnalysisResult? GetOrCompute(BeatmapInfo beatmap, RulesetInfo ruleset, IReadOnlyList<Mod>? mods)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            var cached = TryGet(beatmap, mods);

            if (cached != null)
                return cached;

            var lookup = new EzAnalysisLookupCache(beatmap, ruleset, mods);

            try
            {
                return ComputeValueAsync(lookup, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public IBindable<string?> ActiveSongsBranchDisplayName => analysisDatabase.ActiveSongsBranchDisplayName;

        public IBindable<int> ActiveSongsBranchVersion => analysisDatabase.ActiveSongsBranchVersion;

        public bool HasActiveSongsBranch => analysisDatabase.HasActiveSongsBranch;

        public bool HasActiveSongsBranchFor(IRulesetInfo? rulesetInfo)
            => analysisDatabase.HasActiveSongsBranchFor(rulesetInfo);

        public IReadOnlyDictionary<Guid, double> GetActiveSongsBranchValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
            => analysisDatabase.GetActiveSongsBranchValues(beatmaps, rulesetInfo, mods);

        public IReadOnlyDictionary<Guid, double> GetActiveSongsBranchPpValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
            => analysisDatabase.GetActiveSongsBranchPpValues(beatmaps, rulesetInfo, mods);

        public IReadOnlyDictionary<Guid, double> GetBaselineXxySrFromRealm(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
            => analysisDatabase.GetBaselineXxySrFromRealm(beatmaps, rulesetInfo, mods);

        public IReadOnlyDictionary<Guid, double> GetStoredPpValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
            => analysisDatabase.GetStoredPpValues(beatmaps, rulesetInfo, mods);

        public bool IsActiveSongsBranchFor(IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods)
            => analysisDatabase.IsActiveSongsBranchFor(rulesetInfo, mods);

        public bool IsSongsBranchActive(string databasePath)
            => analysisDatabase.IsSongsBranchActive(databasePath);

        public void DeactivateSongsBranch()
            => analysisDatabase.DeactivateSongsBranch();

        public bool TryActivateSongsBranch(string databasePath, out LocalisableString message)
            => analysisDatabase.TryActivateSongsBranch(databasePath, out message);

        public bool TryToggleSongsBranchActivation(string databasePath, out LocalisableString message)
            => analysisDatabase.TryToggleSongsBranchActivation(databasePath, out message);

        public bool TryDeleteSongsBranch(string databasePath, out LocalisableString message)
            => analysisDatabase.TryDeleteSongsBranch(databasePath, out message);

        // public bool TryToggleSongsBranchHidden(string databasePath, out LocalisableString message, out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
        //     => analysisDatabase.TryToggleSongsBranchHidden(databasePath, out message, out nonHideableBeatmapSets);

        public bool TryToggleCollectionHidden(Guid collectionId, string collectionName, IEnumerable<string> beatmapMd5Hashes, out LocalisableString message,
                                              out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets, long lastModifiedUnixMilliseconds = 0)
            => analysisDatabase.TryToggleCollectionHidden(collectionId, collectionName, beatmapMd5Hashes, out message, out nonHideableBeatmapSets, lastModifiedUnixMilliseconds);

        public IReadOnlySet<Guid> GetHiddenCollectionIds()
            => analysisDatabase.GetHiddenCollectionIds();

        public IReadOnlyList<EzAnalysisPersistentStore.SongsBranchDescriptor> GetAvailableSongsBranches(IRulesetInfo? rulesetInfo = null, IReadOnlyList<Mod>? mods = null, int maxCount = 0)
            => analysisDatabase.GetAvailableSongsBranches(rulesetInfo, mods, maxCount);

        public Task<EzAnalysisDatabase.SongsBranchBuildResult> CreateAndActivateSongsBranchAsync(IEnumerable<BeatmapInfo> beatmaps, EzAnalysisPersistentStore.SourceCollectionSnapshot? sourceCollection,
                                                                                                 IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods, bool activateAfterCreate = true,
                                                                                                 Action<int, int>? progress = null, CancellationToken cancellationToken = default)
            => analysisDatabase.CreateAndActivateSongsBranchAsync(beatmaps, sourceCollection, rulesetInfo, mods, activateAfterCreate, progress, cancellationToken);

        protected virtual Task<EzAnalysisResult?> GetDynamicAnalysisAsync(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IEnumerable<Mod>? mods,
                                                                          CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var lookup = new EzAnalysisLookupCache(beatmapInfo, rulesetInfo, mods);

            // The live cache is the single store of analysed values; this component only schedules. Reading it
            // first is what keeps the song-select panel and the radar on one compute per chart + mods.
            if (liveMetrics.Get(keyOf(lookup)) is { } cached)
                return Task.FromResult<EzAnalysisResult?>(cached);

            return computeIntoStoreAsync(lookup, cancellationToken, computationDelay);
        }

        private EzAnalysisLiveCache.AnalysisKey keyOf(in EzAnalysisLookupCache lookup)
            => new EzAnalysisLiveCache.AnalysisKey(lookup.BeatmapInfo.Hash, lookup.Ruleset.OnlineID, lookup.ModsSignature);

        /// <summary>
        /// Runs the pass for <paramref name="lookup"/> into the shared session store. There is no second cache below
        /// this one: a caller either finds the entry in <see cref="EzAnalysisLiveCache"/> or it is computed here.
        /// </summary>
        private async Task<EzAnalysisResult?> computeIntoStoreAsync(EzAnalysisLookupCache lookup, CancellationToken cancellationToken, int computationDelay)
        {
            if (computationDelay > 0)
                await Task.Delay(computationDelay, cancellationToken).ConfigureAwait(false);

            return await ComputeValueAsync(lookup, cancellationToken).ConfigureAwait(false);
        }

        protected Task<EzAnalysisResult?> ComputeValueAsync(EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            var key = keyOf(lookup);

            return Task.Factory.StartNew(() =>
            {
                // The scheduler is single-threaded, so two callers that both missed before this task ran are queued
                // back to back. The second one folds into the entry the first wrote instead of re-running the pass:
                // a hit is only ever judged here, because no caller can see the queue.
                if (liveMetrics.Get(key) is { } existing)
                    return existing;

                var computed = computeAnalysis(lookup, cancellationToken);

                if (computed is { } result)
                    liveMetrics.Set(key, result);

                return computed;
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        private void updateTrackedBindables()
        {
            lock (bindableUpdateLock)
            {
                cancelTrackedBindableUpdate();

                foreach (var bindable in trackedBindables)
                {
                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, bindable.CancellationToken);
                    linkedCancellationSources.Add(linkedSource);

                    if (bindable.BeatmapInfo is BeatmapInfo localBeatmapInfo)
                        updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, linkedSource.Token);
                }
            }
        }

        private void cancelTrackedBindableUpdate()
        {
            lock (bindableUpdateLock)
            {
                debouncedModSettingsChange?.Cancel();
                debouncedModSettingsChange = null;

                trackedUpdateCancellationSource.Cancel();
                trackedUpdateCancellationSource.Dispose();
                trackedUpdateCancellationSource = new CancellationTokenSource();

                foreach (var cancellationSource in linkedCancellationSources)
                    cancellationSource.Dispose();

                linkedCancellationSources.Clear();
            }
        }

        private void updateBindable(BindableBeatmapEzAnalysis bindable, BeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods,
                                    CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            GetAnalysisAsync(beatmapInfo, rulesetInfo, mods, cancellationToken, computationDelay)
                .ContinueWith(task =>
                {
                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        EzAnalysisResult? analysis = task.GetResultSafely();

                        if (analysis != null)
                            bindable.Value = analysis.Value;
                    });
                }, cancellationToken);
        }

        private EzAnalysisResult? computeAnalysis(in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            try
            {
                // L3 is the full pass: axes, dan, radar and the typed structure summary. The SQLite slice is a
                // different, narrower shape (kps/kpc only) and is pre-filled by the caller instead, so returning it
                // from here would hand consumers an entry with no axes.
                return EzAnalysisComputation.Compute(beatmapManager, lookup, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (BeatmapInvalidForRulesetException invalidForRuleset)
            {
                if (lookup.Ruleset.Equals(lookup.BeatmapInfo.Ruleset))
                    Logger.Error(invalidForRuleset, $"[EzAnalysisCache] Failed to convert {lookup.BeatmapInfo.OnlineID} to the beatmap's default ruleset ({lookup.BeatmapInfo.Ruleset}).", Ez2ConfigManager.LOGGER_NAME);

                return null;
            }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref computeFailCount) <= 10)
                {
                    string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                    Logger.Error(ex,
                        $"[EzAnalysisCache] computeAnalysis failed. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}",
                        Ez2ConfigManager.LOGGER_NAME);
                }

                return null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            trackedUpdateCancellationSource.Dispose();
            updateScheduler.Dispose();
        }

        private void disableAnalysisRuntime()
        {
            cancelTrackedBindableUpdate();
            liveMetrics.Clear();
        }

        private sealed class BindableBeatmapEzAnalysis : Bindable<EzAnalysisResult>
        {
            public IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;

            public BindableBeatmapEzAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken)
            {
                BeatmapInfo = beatmapInfo;
                CancellationToken = cancellationToken;
            }
        }
    }
}
