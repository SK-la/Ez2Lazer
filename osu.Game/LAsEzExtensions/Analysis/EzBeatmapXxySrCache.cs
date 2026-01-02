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
using osu.Framework.Logging;
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
    [Obsolete("已由 EzBeatmapManiaAnalysisCache 接管（统一缓存 KPS/KPC/Scratch/xxy_SR）。该类型仅保留为备份/回归对比用途，请不要在运行时再注入或使用。")]
    public partial class EzBeatmapXxySrCache : MemoryCachingComponent<EzBeatmapXxySrCache.XxySrCacheLookup, double?>
    {
        private const string logger_name = "xxy_sr";
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

                // 注意：playable beatmap 的内容取决于 mods。
                // 这里必须按当前 lookup.OrderedMods 获取，否则会导致“关 mod 后仍显示旧 SR”的问题。
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // 明显异常：如果 hitobjects 为空，仍计算出 SR 会导致离谱结果。
                // 这种情况更像是转换/加载/算法输入不对，直接记录并返回 null。
                if (playableBeatmap.HitObjects.Count == 0)
                {
                    string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                    Logger.Log($"xxy_SR aborted: playableBeatmap has 0 hitobjects. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}", logger_name, LogLevel.Error);
                    return null;
                }

                if (!XxySrCalculatorBridge.TryCalculate(playableBeatmap, out double sr))
                    return null;

                // Defensive: avoid propagating invalid values to UI.
                if (double.IsNaN(sr) || double.IsInfinity(sr))
                {
                    Logger.Log($"xxy_SR returned invalid value (NaN/Infinity). beatmapId={lookup.BeatmapInfo.ID} ruleset={lookup.Ruleset.ShortName}", logger_name, LogLevel.Error);
                    return null;
                }

                // "异常"：出现极端偏差时记录（不记录正常计算）。
                if (sr < 0 || sr > 1000)
                {
                    string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                    Logger.Log($"xxy_SR abnormal value: {sr}. hitobjects={playableBeatmap.HitObjects.Count} beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}", logger_name, LogLevel.Error);
                }

                return sr;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                // 只记录异常：用于排查“值偏差非常大/计算失败导致空 pill”。
                string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                Logger.Error(ex, $"xxy_SR compute exception. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}", logger_name);
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

        private static class XxySrCalculatorBridge
        {
            private const string calculator_type_name = "osu.Game.Rulesets.Mania.LAsEZMania.Analysis.SRCalculator";
            private const string calculator_method_name = "CalculateSR";
            private const string mania_assembly_name = "osu.Game.Rulesets.Mania";

            private static readonly Lazy<MethodInfo?> calculateMethod = new Lazy<MethodInfo?>(resolveCalculateMethod, LazyThreadSafetyMode.ExecutionAndPublication);

            private static int resolve_fail_logged;
            private static int invoke_fail_count;

            public static bool TryCalculate(IBeatmap beatmap, out double sr)
            {
                sr = 0;

                var method = calculateMethod.Value;
                if (method == null)
                {
                    if (Interlocked.Exchange(ref resolve_fail_logged, 1) == 0)
                        Logger.Log($"xxy_SR bridge failed to resolve {calculator_type_name}.{calculator_method_name}(IBeatmap).", logger_name, LogLevel.Error);
                    return false;
                }

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
                catch (Exception ex)
                {
                    // Avoid spamming logs if something is systematically broken.
                    if (Interlocked.Increment(ref invoke_fail_count) <= 10)
                        Logger.Error(ex, $"xxy_SR bridge invoke exception. beatmapType={beatmap.GetType().FullName}", logger_name);
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
                catch (Exception ex)
                {
                    if (Interlocked.Exchange(ref resolve_fail_logged, 1) == 0)
                        Logger.Error(ex, $"xxy_SR bridge resolve exception for {calculator_type_name}.{calculator_method_name}(IBeatmap).", logger_name);
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
