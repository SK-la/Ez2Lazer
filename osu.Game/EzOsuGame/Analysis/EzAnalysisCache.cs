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
    /// 选歌分析前台缓存，对齐官方 <see cref="BeatmapDifficultyCache"/> 的 mod 按需重算语义。
    ///
    /// - L1 Realm 基线 xxy/PP：由 UI 直读 <see cref="BeatmapInfo"/>（见 <see cref="EzSongSelectAnalysisDisplay"/>），不经本 cache。
    /// - L2 主 SQLite：NoMod 时仅提供 kps/KPC 切片（<see cref="EzAnalysisDatabase.TryGetStoredSqliteSlice"/>）。
    /// - L3 本 cache：当前 ruleset/mods 下的动态 <see cref="EzAnalysisResult"/>（含 mod 后 xxy/PP/kps）。
    /// NoMod 面板语义对齐 <see cref="BeatmapDifficultyCache"/>：先 L2 预填，debounce 后总调度检验；检验走 <see cref="EzAnalysisDatabase.BackfillStoredDataAsync"/>（齐全则只读 SQLite）。
    /// 不承担预热、Realm 回填或分支库写入。
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

        public virtual Task<EzAnalysisResult?> GetAnalysisAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            rulesetInfo ??= beatmapInfo.Ruleset;

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;

            if (runtimeAnalysisEnabled.Value && localBeatmapInfo != null && rulesetInfo is RulesetInfo localRulesetInfo)
                return getDynamicWithStoredFallbackAsync(localBeatmapInfo, localRulesetInfo);

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

                if (EzAnalysisDatabase.CanUseStoredAnalysis(dynamicBeatmapInfo, dynamicRulesetInfo, mods)
                    && analysisDatabase.TryGetStoredSqliteSlice(dynamicBeatmapInfo, dynamicRulesetInfo, out var fallbackStoredSlice))
                    return fallbackStoredSlice;

                return null;
            }
        }

        public bool TryGetXxySr(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out double xxySr)
            => analysisDatabase.TryGetXxySr(beatmapInfo, rulesetInfo, out xxySr);

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
                                              out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
            => analysisDatabase.TryToggleCollectionHidden(collectionId, collectionName, beatmapMd5Hashes, out message, out nonHideableBeatmapSets);

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
                if (EzAnalysisDatabase.CanUseStoredAnalysis(lookup.BeatmapInfo, lookup.Ruleset, lookup.OrderedMods))
                {
                    return analysisDatabase.BackfillStoredDataAsync(lookup.BeatmapInfo, skipExistingComparison: false, cancellationToken)
                                           .GetAwaiter()
                                           .GetResult();
                }

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
