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
using osu.Game.LAsEzExtensions.Analysis.Persistence;
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
        private EzManiaAnalysisPersistentStore persistentStore { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        private int modsRevision;

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

                var analysis = new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
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
