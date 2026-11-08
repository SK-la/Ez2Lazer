// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
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
    /// 选歌 Ez 分析前台缓存与派发点。调用方只描述要分析的谱面 / 规则集 / mods，不指定读哪一层。
    ///
    /// 派发顺序：本会话 store → 即时计算（受 <see cref="Ez2Setting.EzAnalysisRecEnabled"/>）→ 主 SQLite NoMod 切片
    /// （受 <see cref="Ez2Setting.EzAnalysisSqliteEnabled"/>）→ 无结果。
    /// 已存切片只描述本规则集 NoMod 请求，因此有 mod 或换规则集的请求没有已存答案，由即时计算回答。
    /// 标量（星 / PP / 技能星）经 <see cref="ReadVariantValues"/> 按当前选中系统读取，缺失的半侧才回落到基线列。
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

        /// <summary>
        /// Passes currently running or queued, keyed like <see cref="EzAnalysisLiveCache"/>. A second request for the
        /// same key joins the running pass instead of queueing a duplicate behind it.
        /// </summary>
        private readonly ConcurrentDictionary<EzAnalysisLiveCache.AnalysisKey, InFlightPass> inFlightPasses
            = new ConcurrentDictionary<EzAnalysisLiveCache.AnalysisKey, InFlightPass>();

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

        /// <summary>
        /// The two SunnySR coefficients. Session results are dropped when either changes, because an entry computed
        /// under the old pair would otherwise answer for the new one.
        /// </summary>
        /// <remarks>
        /// This is a session-only invalidation, not a version bump: stored rows keep whatever they were computed
        /// with, exactly as the settings describe themselves.
        /// </remarks>
        private readonly List<IBindable<double>> ssrTuningBindables = new List<IBindable<double>>();

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

            // A coefficient change makes every held result stale; dropping them costs a recompute of whatever is on
            // screen, the same price the first visit paid.
            ssrTuningBindables.Add(ezConfig.GetBindable<double>(Ez2Setting.SsrRescaleThreshold));
            ssrTuningBindables.Add(ezConfig.GetBindable<double>(Ez2Setting.SsrLnMultiplier));
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

            foreach (var tuning in ssrTuningBindables)
            {
                tuning.BindValueChanged(_ =>
                {
                    // Live values only: no version bump, no stored row is touched.
                    liveMetrics.ClearAll();
                    Scheduler.AddOnce(updateTrackedBindables);
                });
            }

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

        public IBindable<EzAnalysisResult> GetBindableAnalysis(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var bindable = new BindableBeatmapEzAnalysis(beatmapInfo, cancellationToken);

            // NoMod：仅预填主 SQLite 的 kps/KPC 切片，让首帧就有 KPS；其余数值由 getAnalysisAsync 按同一套派发补齐。
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

        /// <summary>
        /// The single read entry every consumer goes through. Callers state what they want analysed; which source
        /// answers is decided here from the request and the current database state.
        /// </summary>
        /// <remarks>
        /// Dispatch order: this session's store, then a live pass when on-demand computation is enabled, then the
        /// stored SQLite slice. A stored slice only describes a NoMod request for the beatmap's own ruleset, so a
        /// modded or foreign-ruleset request is answered by the live pass.
        /// </remarks>
        public virtual Task<EzAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            rulesetInfo ??= beatmapInfo.Ruleset;

            if (beatmapInfo is not BeatmapInfo localBeatmapInfo
                || ((rulesetInfo as RulesetInfo) ?? localBeatmapInfo.Ruleset) is not { } localRulesetInfo)
                return Task.FromResult<EzAnalysisResult?>(null);

            return resolveAsync(new EzAnalysisLookupCache(localBeatmapInfo, localRulesetInfo, mods), cancellationToken, computationDelay);
        }

        private async Task<EzAnalysisResult?> resolveAsync(EzAnalysisLookupCache lookup, CancellationToken cancellationToken, int computationDelay)
        {
            var key = keyOf(lookup);

            if (liveMetrics.Get(key) is { } cached)
                return cached;

            if (runtimeAnalysisEnabled.Value)
            {
                var dynamicAnalysis = await computeIntoStoreAsync(lookup, cancellationToken, computationDelay).ConfigureAwait(false);

                if (dynamicAnalysis != null)
                    return dynamicAnalysis;
            }

            if (analysisDatabase.IsSqliteAnalysisEnabled
                && EzAnalysisDatabase.CanUseStoredAnalysis(lookup.BeatmapInfo, lookup.Ruleset, lookup.OrderedMods)
                && analysisDatabase.TryGetStoredSqliteSlice(lookup.BeatmapInfo, lookup.Ruleset, out var storedSlice))
                return storedSlice;

            return null;
        }

        /// <summary>
        /// The selected system's star, pp and skill star for this chart. The cache decides whether the session
        /// result or the stored baseline answers each half.
        /// </summary>
        /// <param name="officialLive">
        /// Official difficulty the caller already holds from <see cref="BeatmapDifficultyCache"/>; used only when
        /// the selected system is the official one.
        /// </param>
        public EzReadoutValues ReadVariantValues(BeatmapInfo beatmap, IReadOnlyList<Mod>? mods = null, in EzReadoutValues officialLive = default)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            var readout = EzRulesetReadout.Selected(beatmap.Ruleset.OnlineID);

            if (readout is not { } selected)
                return EzReadoutValues.Empty;

            var live = TryGet(beatmap, mods) is { } analysis
                ? variantValuesOf(analysis, selected)
                : EzReadoutValues.Empty;

            if (EzRulesetReadout.IsOfficial(selected))
            {
                live = new EzReadoutValues(
                    officialLive.Star ?? live.Star,
                    officialLive.PerformancePoints ?? live.PerformancePoints,
                    live.SkillStar);
            }

            return EzRulesetReadout.Read(beatmap, selected, live);
        }

        private static EzReadoutValues variantValuesOf(in EzAnalysisResult analysis, EzReadout readout)
        {
            string starChannel = EzRulesetReadout.ChannelOf(readout.Rating);
            double? star = positiveAxis(analysis, starChannel, EzAnalysisChannels.STAR_RATING);
            double? performancePoints = positiveAxis(analysis, starChannel, EzAnalysisChannels.PERFORMANCE_POINTS);

            string skillChannel = EzRulesetReadout.ChannelOf(readout.SkillRating);
            double? skillStar = readout.SkillRating == EzRatingSystem.None
                ? null
                : positiveAxis(analysis, skillChannel, EzRulesetReadout.RatingAxisOf(readout.SkillRating));

            return new EzReadoutValues(star, performancePoints, skillStar);
        }

        /// <summary>A measured scalar is only shown when it is a real value; an empty chart reads as absent.</summary>
        private static double? positiveAxis(in EzAnalysisResult analysis, string channel, string axisId)
            => analysis.AxesOf(channel).TryGetValue(axisId, out double value) && double.IsFinite(value) && value > 0 ? value : null;

        public bool TryGetSunnySR(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out double sunnySR)
            => analysisDatabase.TryGetSunnySR(beatmapInfo, rulesetInfo, out sunnySR);

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

        public IReadOnlyDictionary<Guid, double> GetBaselineSunnySRFromRealm(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
            => analysisDatabase.GetBaselineSunnySRFromRealm(beatmaps, rulesetInfo, mods);

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

        private EzAnalysisLiveCache.AnalysisKey keyOf(in EzAnalysisLookupCache lookup)
            => new EzAnalysisLiveCache.AnalysisKey(lookup.BeatmapInfo.Hash, lookup.Ruleset.OnlineID, lookup.ModsSignature);

        /// <summary>
        /// Runs the pass for <paramref name="lookup"/> into the shared session store. There is no second cache below
        /// this one: a caller either finds the entry in <see cref="EzAnalysisLiveCache"/> or it is computed here.
        /// </summary>
        /// <remarks>
        /// Requests for the same key share one pass. Each waiter contributes a reference; the pass is cancelled only
        /// when the last of them goes away, so a panel recycled while another surface still wants the same chart does
        /// not abort the shared compute.
        /// </remarks>
        private async Task<EzAnalysisResult?> computeIntoStoreAsync(EzAnalysisLookupCache lookup, CancellationToken cancellationToken, int computationDelay)
        {
            if (computationDelay > 0)
                await Task.Delay(computationDelay, cancellationToken).ConfigureAwait(false);

            return await ComputeValueAsync(lookup, cancellationToken).ConfigureAwait(false);
        }

        protected async Task<EzAnalysisResult?> ComputeValueAsync(EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            var key = keyOf(lookup);

            while (true)
            {
                if (inFlightPasses.TryGetValue(key, out var existing))
                {
                    if (existing.TryJoin())
                    {
                        try
                        {
                            return await existing.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            existing.Leave();
                        }
                    }

                    // The pass completed between the lookup and the join; drop it so the next iteration can start a
                    // fresh one instead of spinning on a finished entry.
                    removeInFlightPass(key, existing);
                    continue;
                }

                var created = new InFlightPass();
                created.Task = startPassTask(key, lookup, created.Cancellation.Token);

                if (!inFlightPasses.TryAdd(key, created))
                {
                    // Another caller installed a pass for this key first; the one not observed must not keep running.
                    await created.Cancellation.CancelAsync().ConfigureAwait(false);
                    continue;
                }

                created.TryJoin();

                try
                {
                    return await created.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    created.Leave();
                    removeInFlightPass(key, created);
                }
            }
        }

        private Task<EzAnalysisResult?> startPassTask(EzAnalysisLiveCache.AnalysisKey key, EzAnalysisLookupCache lookup, CancellationToken cancellationToken)
        {
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

        private void removeInFlightPass(EzAnalysisLiveCache.AnalysisKey key, InFlightPass pass)
        {
            if (inFlightPasses.TryGetValue(key, out var current) && ReferenceEquals(current, pass))
                inFlightPasses.TryRemove(key, out _);
        }

        /// <summary>
        /// One pass shared by every caller waiting on a key. The cancellation source is deliberately never disposed:
        /// a waiter that joins after the pass completed still releases its reference, and a disposed source would
        /// turn that release into an exception.
        /// </summary>
        private sealed class InFlightPass
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public Task<EzAnalysisResult?> Task = null!;

            private readonly Lock gate = new Lock();
            private int waiters;
            private bool finished;

            public bool TryJoin()
            {
                lock (gate)
                {
                    if (finished)
                        return false;

                    waiters++;
                    return true;
                }
            }

            public void Leave()
            {
                lock (gate)
                {
                    if (waiters > 0)
                        waiters--;

                    if (waiters > 0 || finished)
                        return;

                    finished = true;
                    Cancellation.Cancel();
                }
            }
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
                EzAnalysisPassPerf.RecordPass(succeeded: false);

                if (lookup.Ruleset.Equals(lookup.BeatmapInfo.Ruleset))
                    Logger.Error(invalidForRuleset, $"[EzAnalysisCache] Failed to convert {lookup.BeatmapInfo.OnlineID} to the beatmap's default ruleset ({lookup.BeatmapInfo.Ruleset}).", Ez2ConfigManager.LOGGER_NAME);

                return null;
            }
            catch (Exception ex)
            {
                EzAnalysisPassPerf.RecordPass(succeeded: false);

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

            foreach (var tuning in ssrTuningBindables)
                tuning.UnbindAll();

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
