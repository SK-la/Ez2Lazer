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
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌分析前台缓存。
    ///
    /// 职责尽量对齐官方 BeatmapDifficultyCache：
    /// - 自身负责当前 ruleset/mods 下的前台查询、bindable 跟踪与失效更新。
    /// - SQLite 持久化结果仅作为默认 ruleset + 无 mod 时的基线读取来源。
    /// - 不承担预热、补算、写库职责。
    /// </summary>
    public partial class EzAnalysisCache : MemoryCachingComponent<EzAnalysisLookupCache, EzAnalysisResult?>
    {
        private static int computeFailCount;

        private readonly EzAnalysisDatabase analysisDatabase;
        private readonly IBindable<bool> runtimeAnalysisEnabled;
        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(EzAnalysisCache));
        private readonly WeakList<BindableBeatmapEzAnalysis> trackedBindables = new WeakList<BindableBeatmapEzAnalysis>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();
        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private const int mod_settings_debounce = SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE + 10;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        public EzAnalysisCache(EzAnalysisDatabase analysisDatabase, Ez2ConfigManager ezConfig)
        {
            this.analysisDatabase = analysisDatabase;
            runtimeAnalysisEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled);
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

        protected override bool CacheNullValues => false;

        public void Invalidate(IBeatmapInfo oldBeatmap, IBeatmapInfo newBeatmap)
        {
            base.Invalidate(lookup => lookup.BeatmapInfo.Equals(oldBeatmap));

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

            if (analysisDatabase.TryGetStoredAnalysis(beatmapInfo, beatmapInfo.Ruleset, out var storedAnalysis))
                bindable.Value = storedAnalysis;

            if (beatmapInfo is BeatmapInfo localBeatmapInfo)
                updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay);

            lock (bindableUpdateLock)
                trackedBindables.Add(bindable);

            return bindable;
        }

        public virtual Task<EzAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            rulesetInfo ??= beatmapInfo.Ruleset;

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;

            if (runtimeAnalysisEnabled.Value && localBeatmapInfo != null && rulesetInfo is RulesetInfo localRulesetInfo)
                return getDynamicWithStoredFallbackAsync(localBeatmapInfo, localRulesetInfo);

            if (localBeatmapInfo != null
                && EzAnalysisDatabase.CanUseStoredAnalysis(localBeatmapInfo, rulesetInfo, mods)
                && analysisDatabase.TryGetStoredAnalysis(beatmapInfo, rulesetInfo, out var storedAnalysis))
                return Task.FromResult<EzAnalysisResult?>(storedAnalysis);

            return Task.FromResult<EzAnalysisResult?>(null);

            async Task<EzAnalysisResult?> getDynamicWithStoredFallbackAsync(BeatmapInfo dynamicBeatmapInfo, RulesetInfo dynamicRulesetInfo)
            {
                var dynamicAnalysis = await GetDynamicAnalysisAsync(dynamicBeatmapInfo, dynamicRulesetInfo, mods, cancellationToken, computationDelay).ConfigureAwait(false);

                if (dynamicAnalysis != null)
                    return dynamicAnalysis;

                if (EzAnalysisDatabase.CanUseStoredAnalysis(dynamicBeatmapInfo, dynamicRulesetInfo, mods)
                    && analysisDatabase.TryGetStoredAnalysis(dynamicBeatmapInfo, dynamicRulesetInfo, out var fallbackStoredAnalysis))
                    return fallbackStoredAnalysis;

                return null;
            }
        }

        public bool TryGetXxySr(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out double xxySr)
            => analysisDatabase.TryGetXxySr(beatmapInfo, rulesetInfo, out xxySr);

        public IBindable<string?> ActiveXxySrBranchDisplayName => analysisDatabase.ActiveXxySrBranchDisplayName;

        public IBindable<int> ActiveXxySrBranchVersion => analysisDatabase.ActiveXxySrBranchVersion;

        public bool HasActiveXxySrBranch => analysisDatabase.HasActiveXxySrBranch;

        public bool HasActiveXxySrBranchFor(IRulesetInfo? rulesetInfo)
            => analysisDatabase.HasActiveXxySrBranchFor(rulesetInfo);

        public IReadOnlyDictionary<Guid, double> GetActiveXxySrBranchValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo)
            => analysisDatabase.GetActiveXxySrBranchValues(beatmaps, rulesetInfo);

        public IReadOnlyDictionary<Guid, double> GetStoredXxySrValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
            => analysisDatabase.GetStoredXxySrValues(beatmaps, rulesetInfo, mods);

        public bool IsActiveXxySrBranchFor(IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods)
            => analysisDatabase.IsActiveXxySrBranchFor(rulesetInfo, mods);

        public bool IsXxySrBranchActive(string databasePath)
            => analysisDatabase.IsXxySrBranchActive(databasePath);

        public void DeactivateXxySrBranch()
            => analysisDatabase.DeactivateXxySrBranch();

        public bool TryActivateXxySrBranch(string databasePath, out LocalisableString message)
            => analysisDatabase.TryActivateXxySrBranch(databasePath, out message);

        public bool TryToggleXxySrBranchActivation(string databasePath, out LocalisableString message)
            => analysisDatabase.TryToggleXxySrBranchActivation(databasePath, out message);

        public bool TryDeleteXxySrBranch(string databasePath, out LocalisableString message)
            => analysisDatabase.TryDeleteXxySrBranch(databasePath, out message);

        public bool TryToggleXxySrBranchHidden(string databasePath, out LocalisableString message, out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
            => analysisDatabase.TryToggleXxySrBranchHidden(databasePath, out message, out nonHideableBeatmapSets);

        public bool TryToggleCollectionHidden(Guid collectionId, string collectionName, IEnumerable<string> beatmapMd5Hashes, out LocalisableString message,
                                              out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
            => analysisDatabase.TryToggleCollectionHidden(collectionId, collectionName, beatmapMd5Hashes, out message, out nonHideableBeatmapSets);

        public IReadOnlySet<Guid> GetHiddenCollectionIds()
            => analysisDatabase.GetHiddenCollectionIds();

        public IReadOnlyList<EzAnalysisPersistentStore.XxySrBranchDescriptor> GetAvailableXxySrBranches(IRulesetInfo? rulesetInfo = null, IReadOnlyList<Mod>? mods = null, int maxCount = 0)
            => analysisDatabase.GetAvailableXxySrBranches(rulesetInfo, mods, maxCount);

        public Task<EzAnalysisDatabase.XxySrBranchBuildResult> CreateAndActivateXxySrBranchAsync(IEnumerable<BeatmapInfo> beatmaps, EzAnalysisPersistentStore.SourceCollectionSnapshot? sourceCollection,
                                                                                                 IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods,
                                                                                                 Action<int, int>? progress = null, CancellationToken cancellationToken = default)
            => analysisDatabase.CreateAndActivateXxySrBranchAsync(beatmaps, sourceCollection, rulesetInfo, mods, progress, cancellationToken);

        protected virtual Task<EzAnalysisResult?> GetDynamicAnalysisAsync(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IEnumerable<Mod>? mods,
                                                                          CancellationToken cancellationToken = default, int computationDelay = 0)
            => GetAsync(new EzAnalysisLookupCache(beatmapInfo, rulesetInfo, mods), cancellationToken, computationDelay);

        protected override Task<EzAnalysisResult?> ComputeValueAsync(EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeAnalysis(lookup, cancellationToken);
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        private void updateTrackedBindables()
        {
            if (!runtimeAnalysisEnabled.Value)
                return;

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
            Invalidate(_ => true);
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
