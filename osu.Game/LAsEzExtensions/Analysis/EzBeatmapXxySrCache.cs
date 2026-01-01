// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.LAsEzExtensions.Analysis
{
    /// <summary>
    /// 选歌面板用的 xxy_SR（Mania）缓存。
    /// - 计算入口在 osu.Game.Rulesets.Mania 程序集中，为避免循环依赖，这里通过反射调用。
    /// - 使用单线程 <see cref="ThreadedTaskScheduler"/> 统一调度，避免拖动滚动条时同时触发大量重算。
    /// - 跟随 ruleset/mods 及 mod 设置变化自动更新已追踪的 bindable。
    /// </summary>
    public partial class EzBeatmapXxySrCache : MemoryCachingComponent<EzBeatmapXxySrCache.XxySrCacheLookup, double?>
    {
        private const int mod_settings_debounce = 150;

        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(EzBeatmapXxySrCache));

        private readonly WeakList<BindableXxySr> trackedBindables = new WeakList<BindableXxySr>();
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentRuleset.BindValueChanged(_ => Scheduler.AddOnce(updateTrackedBindables));

            currentMods.BindValueChanged(mods =>
            {
                modSettingChangeTracker?.Dispose();

                Scheduler.AddOnce(updateTrackedBindables);

                modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                modSettingChangeTracker.SettingChanged += _ =>
                {
                    debouncedModSettingsChange?.Cancel();
                    debouncedModSettingsChange = Scheduler.AddDelayed(updateTrackedBindables, mod_settings_debounce);
                };
            }, true);
        }

        protected override bool CacheNullValues => false;

        public IBindable<double?> GetBindableXxySr(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;

            var bindable = new BindableXxySr(beatmapInfo, cancellationToken)
            {
                Value = null
            };

            if (localBeatmapInfo == null)
                return bindable;

            updateBindable(bindable, localBeatmapInfo, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay);

            lock (bindableUpdateLock)
                trackedBindables.Add(bindable);

            return bindable;
        }

        public Task<double?> GetXxySrAsync(IBeatmapInfo beatmapInfo,
                                          IRulesetInfo? rulesetInfo = null,
                                          IEnumerable<Mod>? mods = null,
                                          CancellationToken cancellationToken = default,
                                          int computationDelay = 0)
        {
            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = (rulesetInfo ?? beatmapInfo.Ruleset) as RulesetInfo;

            if (localBeatmapInfo == null || localRulesetInfo == null)
                return Task.FromResult<double?>(null);

            return GetAsync(new XxySrCacheLookup(localBeatmapInfo, localRulesetInfo, mods), cancellationToken, computationDelay);
        }

        protected override Task<double?> ComputeValueAsync(XxySrCacheLookup lookup, CancellationToken token = default)
        {
            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeXxySr(lookup, token);
            }, token, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        private double? computeXxySr(in XxySrCacheLookup lookup, CancellationToken cancellationToken)
        {
            try
            {
                // 目前算法仅支持 mania。
                if (lookup.Ruleset.OnlineID != 3)
                    return null;

                var workingBeatmap = beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo);

                // 复用同一份 playable beatmap，避免重复转换。
                var cachedWorking = new PlayableCachedWorkingBeatmap(workingBeatmap);
                var playableBeatmap = cachedWorking.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!XxySrCalculatorBridge.TryCalculate(playableBeatmap, out double sr))
                    return null;

                return sr;
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

                // 规则集变化到非 mania 时，不触发后台计算，并清空已显示的 SR，避免残留旧值。
                if (currentRuleset.Value.OnlineID != 3)
                {
                    foreach (var b in trackedBindables)
                        Schedule(() => b.Value = null);

                    return;
                }

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

        private void updateBindable(BindableXxySr bindable,
                                    BeatmapInfo beatmapInfo,
                                    IRulesetInfo? rulesetInfo,
                                    IEnumerable<Mod>? mods,
                                    CancellationToken cancellationToken = default,
                                    int computationDelay = 0)
        {
            GetXxySrAsync(beatmapInfo, rulesetInfo, mods, cancellationToken, computationDelay)
                .ContinueWith(task =>
                {
                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        bindable.Value = task.GetResultSafely();
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

        public readonly struct XxySrCacheLookup : IEquatable<XxySrCacheLookup>
        {
            public readonly BeatmapInfo BeatmapInfo;
            public readonly RulesetInfo Ruleset;
            public readonly Mod[] OrderedMods;

            public XxySrCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IEnumerable<Mod>? mods)
            {
                BeatmapInfo = beatmapInfo;
                Ruleset = ruleset;

                // DeepClone 用于冻结 mod 设置快照，保证缓存 key 与显示一致。
                OrderedMods = mods?.OrderBy(m => m.Acronym).Select(mod => mod.DeepClone()).ToArray() ?? Array.Empty<Mod>();
            }

            public bool Equals(XxySrCacheLookup other)
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

        private class BindableXxySr : Bindable<double?>
        {
            public readonly IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;

            public BindableXxySr(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken)
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

        private static class XxySrCalculatorBridge
        {
            private const string calculator_type_name = "osu.Game.Rulesets.Mania.LAsEZMania.Analysis.SRCalculator";
            private const string calculator_method_name = "CalculateSR";
            private const string mania_assembly_name = "osu.Game.Rulesets.Mania";

            private static readonly Lazy<MethodInfo?> calculateMethod = new Lazy<MethodInfo?>(resolveCalculateMethod, LazyThreadSafetyMode.ExecutionAndPublication);

            public static bool TryCalculate(IBeatmap beatmap, out double sr)
            {
                sr = 0;

                var method = calculateMethod.Value;
                if (method == null)
                    return false;

                try
                {
                    object? result = method.Invoke(null, new object?[] { beatmap });

                    if (result is double d)
                    {
                        sr = d;
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static MethodInfo? resolveCalculateMethod()
            {
                try
                {
                    var type = findType(calculator_type_name);
                    if (type == null)
                        return null;

                    return type.GetMethod(calculator_method_name, BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(IBeatmap) }, modifiers: null);
                }
                catch
                {
                    return null;
                }
            }

            private static Type? findType(string fullName)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType(fullName, throwOnError: false);
                    if (t != null)
                        return t;
                }

                try
                {
                    // 尝试显式加载 mania 程序集。
                    var asm = Assembly.Load(mania_assembly_name);
                    return asm.GetType(fullName, throwOnError: false);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
