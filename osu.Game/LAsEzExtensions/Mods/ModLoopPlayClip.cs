// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Audio;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Mods
{
    /// <summary>
    /// 继承Mod需要自己实现IApplicableToPlayer, IApplicableToHUD接口的使用
    /// </summary>
    public class ModLoopPlayClip : Mod,
                                   IHasSeed,
                                   ILoopTimeRangeMod,
                                   IApplicableToPlayer,
                                   IApplicableToHUD,
                                   IApplicableFailOverride,
                                   IApplicableToRate,
                                   IHasApplyOrder
    {
        public override string Name => "Loop Play Clip (No Fail)";

        public override string Acronym => "LP";
        public override LocalisableString Description => LoopPlayClipStrings.LOOP_PLAY_CLIP_DESCRIPTION;

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.ArrowCircleDown;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public bool PerformFail() => false;

        public bool RestartOnFail => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModRateAdjust),
            typeof(ModTimeRamp),
            typeof(ModAdaptiveSpeed),
            typeof(ModNoFail),
        };

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ($"Speed x{SpeedChange.Value:N2}", AdjustPitch.Value ? "Pitch Adjusted" : "Pitch Unchanged");
                yield return ($"{LoopCount.Value}", "Loop Count");
                yield return ("Break", $"{BreakQuarter.Value} × 1/2 beat");
                yield return ("Start", $"{(CutTimeStart.Value is null ? "Original Start Time" : Millisecond.Value ? $"{CutTimeStart.Value} ms" : GetStringTime((int)CutTimeStart.Value))}");
                yield return ("End", $"{(CutTimeEnd.Value is null ? "Original End Time" : Millisecond.Value ? $"{CutTimeEnd.Value} ms" : GetStringTime((int)CutTimeEnd.Value))}");
                yield return ("Infinite Loop", InfiniteLoop.Value ? "Enabled" : "Disabled");
            }
        }

        public static string GetStringTime(double time)
        {
            int minute = Math.Abs((int)time / 60);
            double second = Math.Abs(time % 60);
            string minus = time < 0 ? "-" : string.Empty;
            string secondLessThan10 = second < 10 ? "0" : string.Empty;
            return $"{minus}{minute}:{secondLessThan10}{second:N1}";
        }

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.LOOP_COUNT_LABEL), nameof(LoopPlayClipStrings.LOOP_COUNT_DESCRIPTION))]
        public BindableInt LoopCount { get; set; } = new BindableInt(20)
        {
            MinValue = 1,
            MaxValue = 100,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SPEED_CHANGE_LABEL), nameof(EzCommonModStrings.SPEED_CHANGE_DESCRIPTION), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.5,
            MaxValue = 2.0,
            Precision = 0.01,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ADJUST_PITCH_LABEL), nameof(EzCommonModStrings.ADJUST_PITCH_DESCRIPTION))]
        public BindableBool AdjustPitch { get; } = new BindableBool();

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.CONSTANT_SPEED_LABEL), nameof(LoopPlayClipStrings.CONSTANT_SPEED_DESCRIPTION))]
        public BindableBool ConstantSpeed { get; } = new BindableBool(true);

        /*[SettingSource("Cut Time Start", "Select your part(second).", SettingControlType = typeof(SettingsSlider<int, CutStart>))]
        public BindableInt CutTimeStart { get; set; } = new BindableInt(-10)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.CUT_END_TIME_LABEL), nameof(LoopPlayClipStrings.CUT_END_TIME_DESCRIPTION), SettingControlType = typeof(SettingsSlider<int, CutEnd>))]
        public BindableInt CutTimeEnd { get; set; } = new BindableInt(1800)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };*/

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.CUT_START_TIME_LABEL), nameof(LoopPlayClipStrings.CUT_START_TIME_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeStart { get; set; } = new Bindable<int?>();

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.CUT_END_TIME_LABEL), nameof(LoopPlayClipStrings.CUT_END_TIME_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeEnd { get; set; } = new Bindable<int?>();

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.USE_MILLISECOND_LABEL), nameof(LoopPlayClipStrings.USE_MILLISECOND_DESCRIPTION))]
        public BindableBool Millisecond { get; set; } = new BindableBool(true);

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.USE_GLOBAL_AB_RANGE_LABEL), nameof(LoopPlayClipStrings.USE_GLOBAL_AB_RANGE_DESCRIPTION))]
        public BindableBool UseGlobalAbRange { get; set; } = new BindableBool(true);

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.BREAK_TIME_LABEL), nameof(LoopPlayClipStrings.BREAK_TIME_DESCRIPTION))]
        public BindableInt BreakQuarter { get; set; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 12,
            Precision = 1
        };

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.RANDOM_LABEL), nameof(LoopPlayClipStrings.RANDOM_DESCRIPTION))]
        public BindableBool Rand { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.MIRROR_LABEL), nameof(EzCommonModStrings.MIRROR_DESCRIPTION))]
        public BindableBool Mirror { get; set; } = new BindableBool(true);

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.INFINITE_LOOP_LABEL), nameof(LoopPlayClipStrings.INFINITE_LOOP_DESCRIPTION))]
        public BindableBool InfiniteLoop { get; set; } = new BindableBool(false);

        [SettingSource(typeof(LoopPlayClipStrings), nameof(LoopPlayClipStrings.MIRROR_TIME_LABEL), nameof(LoopPlayClipStrings.MIRROR_TIME_DESCRIPTION))]
        public BindableInt MirrorTime { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 1
        };

        //[SettingSource("Invert", "Invert next part.")]
        //public BindableBool Invert { get; set; } = new BindableBool(false);

        //[SettingSource("Invert Time", "Every next time part will be inverted.")]
        //public BindableInt InvertTime { get; set; } = new BindableInt(1)
        //{
        //    MinValue = 1,
        //    MaxValue = 10,
        //    Precision = 1
        //};

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        private readonly DuplicateVirtualTrack duplicateTrack;
        private IWorkingBeatmap? pendingWorkingBeatmap;

        // 时间设置统一管理
        private readonly RateAdjustModHelper rateAdjustHelper;

        protected double ResolvedCutTimeStart
        {
            get
            {
                if (pendingWorkingBeatmap != null)
                {
                    // 先确保已解析时间范围
                    EnsureResolvedForBeatmap(pendingWorkingBeatmap);
                }

                // 开启AB开关时，优先从LoopTimeRangeStore获取值，并写入SettingSource
                if (UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                {
                    // 将全局值同步到本地设置（使用从 store 获得的 endMs，避免在 getter 中触发对另一个 getter 的调用导致递归）
                    setGlobalRange(startMs, endMs);
                    return startMs;
                }

                // 从SettingSource获取值
                double start = toMs(CutTimeStart.Value);

                // 开始时间获取失败时设为0
                if (double.IsNaN(start) || double.IsInfinity(start) || !CutTimeStart.Value.HasValue)
                {
                    return 0;
                }

                return start;
            }
        }

        protected double ResolvedCutTimeEnd
        {
            get
            {
                if (pendingWorkingBeatmap != null)
                {
                    // 先确保已解析时间范围
                    EnsureResolvedForBeatmap(pendingWorkingBeatmap);
                }

                // 开启AB开关时，优先从LoopTimeRangeStore获取值，并写入SettingSource
                if (UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                {
                    // 将全局值同步到本地设置（使用从 store 获得的 startMs，避免在 getter 中触发对另一个 getter 的调用导致递归）
                    setGlobalRange(startMs, endMs);
                    return endMs;
                }

                // 从SettingSource获取值
                if (CutTimeEnd.Value.HasValue)
                {
                    double end = toMs(CutTimeEnd.Value);

                    // 结束时间获取失败时使用歌曲原本的结束时间
                    if (double.IsNaN(end) || double.IsInfinity(end))
                    {
                        return GetOriginalBounds().end;
                    }

                    return end;
                }

                // 结束时间获取失败时使用歌曲原本的结束时间
                return GetOriginalBounds().end;
            }
        }

        protected double ResolvedSegmentLength { get; private set; }

        public ModLoopPlayClip()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            UseGlobalAbRange.BindValueChanged(_ => applyRangeFromStore(), true);

            // 当全局A/B范围改变时，更新设置
            LoopTimeRangeStore.START_TIME_MS.BindValueChanged(_ => applyRangeFromStore());
            LoopTimeRangeStore.END_TIME_MS.BindValueChanged(_ => applyRangeFromStore());

            duplicateTrack = new DuplicateVirtualTrack();
        }

        private void applyRangeFromStore()
        {
            if (!LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                return;

            setCutTimeToSettingSource(startMs, endMs);
        }

        private void setCutTimeToSettingSource(double startMs, double endMs)
        {
            CutTimeStart.Value = Millisecond.Value ? (int)startMs : (int)(startMs / 1000d);
            CutTimeEnd.Value = Millisecond.Value ? (int)endMs : (int)(endMs / 1000d);
        }

        public void ApplyToTrack(IAdjustableAudioComponent track) => rateAdjustHelper.ApplyToTrack(track);

        public void ApplyToSample(IAdjustableAudioComponent sample)
        {
            sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);
        }

        public double ApplyToRate(double time, double rate = 1) => rate * SpeedChange.Value;

        public void ApplyToPlayer(Player player)
        {
            pendingWorkingBeatmap = player.Beatmap.Value;

            // 确保时间范围已解析
            EnsureResolvedForBeatmap(pendingWorkingBeatmap);
        }

        public void ApplyToHUD(HUDOverlay overlay)
        {
            if (pendingWorkingBeatmap == null)
            {
                Logger.Log("[ModLoopPlayClip] ApplyToHUD: beatmap is null.", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            if (duplicateTrack.Parent == null)
                overlay.Add(duplicateTrack);

            // Start duplicate preview directly with explicit overrides.
            var overrides = GetOverrides(pendingWorkingBeatmap);
            duplicateTrack.StartPreview(pendingWorkingBeatmap, overrides);
        }

        public void SetLoopTimeRange(double startTime, double endTime)
        {
            if (endTime <= startTime)
                return;

            LoopTimeRangeStore.Set(startTime, endTime);

            // The editor timeline works in milliseconds, while this mod exposes seconds by default.
            setCutTimeToSettingSource(startTime, endTime);

            // Keep current instance in sync with the session store when global mode is enabled.
            if (UseGlobalAbRange.Value)
                applyRangeFromStore();
        }

        protected void EnsureResolvedForBeatmap(IWorkingBeatmap beatmap)
        {
            if (ResolvedSegmentLength > 0)
                return;

            bool hasRange = LoopTimeRangeStore.TryGet(out double cutTimeStart, out double cutTimeEnd);
            var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

            if (!hasRange)
            {
                // 若开始为空则取最早物件时间，若结束为空则取最晚物件时间（不再整体判无效）。
                var (minTime, maxTime) = playableBeatmap.CalculatePlayableBounds();
                cutTimeStart = minTime;
                cutTimeEnd = maxTime;
            }

            double length = cutTimeEnd - cutTimeStart;

            ResolvedSegmentLength = Math.Max(0, length);
        }

        // StartPreview方法调用时会调用此方法获取参数
        public OverrideSettings GetOverrides(IWorkingBeatmap beatmap)
        {
            EnsureResolvedForBeatmap(beatmap);

            // 使用属性获取最新的时间值
            double start = ResolvedCutTimeStart;
            double end = ResolvedCutTimeEnd;

            // 计算总偏移量（全局用户偏移 + 谱面特定偏移 + 平台偏移），实际多数情况为0
            double totalOffset = getAppliedOffsetMs(beatmap);
            double audioStart = start - totalOffset;
            double audioEnd = end - totalOffset;

            var timing = beatmap.Beatmap.ControlPointInfo.TimingPointAt(start);
            double halfBeatMs = timing.BeatLength / 2.0;
            double loopIntervalMs = halfBeatMs * Math.Max(1, BreakQuarter.Value);

            return new OverrideSettings
            {
                StartTime = audioStart,
                Duration = Math.Max(0, audioEnd - audioStart),
                LoopCount = LoopCount.Value,
                LoopInterval = loopIntervalMs,
                ForceLooping = true,
            };
        }

        // 工具方法，集中单位换算和全局写入
        private double toMs(int? v) => v == null ? 0.0 : (int)v * (Millisecond.Value ? 1 : 1000);
        private void setGlobalRange(double? start, double? end) => LoopTimeRangeStore.Set(start ?? 0, end ?? 0);

        /// <summary>
        /// 计算应用于谱面的总偏移量（毫秒）
        /// 包括全局用户偏移、谱面特定偏移和平台偏移
        /// </summary>
        private double getAppliedOffsetMs(IWorkingBeatmap beatmap)
        {
            // 获取谱面特定偏移
            double audioLeadIn = beatmap.Beatmap.AudioLeadIn;
            double beatmapOffset = beatmap.Beatmap.CountdownOffset;
            Logger.Log($"[ModLoopPlayClip] AudioLeadIn = {audioLeadIn}, Beatmap CountdownOffset = {beatmapOffset}");
            return audioLeadIn + beatmapOffset;
        }

        // 获取原始音谱边界
        protected (double start, double end) GetOriginalBounds()
        {
            if (pendingWorkingBeatmap != null)
            {
                var playableBeatmap = pendingWorkingBeatmap.GetPlayableBeatmap(pendingWorkingBeatmap.BeatmapInfo.Ruleset);
                return playableBeatmap.CalculatePlayableBounds();
            }

            // 默认返回0, 0
            return (0, 0);
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            applyRangeFromStore();

            duplicateTrack.StopPreview();

            // 从父容器中移除 duplicateTrack（如果已加入），确保 mod 关闭时不会残留在 HUD 中或影响后续音频。
            if (duplicateTrack.Parent is Container c)
                c.Remove(duplicateTrack, false);
        }

        /// <summary>
        /// 解析给定 <see cref="IBeatmap"/> 的切片时间（毫秒），优先使用全局 A/B 范围，
        /// 否则使用设置中的值（支持秒/毫秒模式），缺失时回退到谱面可播放边界。
        /// 返回 (start, end, length)。
        /// </summary>
        protected (double start, double end, double length) ResolveSliceTimesForBeatmap(IBeatmap beatmap)
        {
            double cutTimeStart, cutTimeEnd;

            if (UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double globalStart, out double globalEnd))
            {
                cutTimeStart = globalStart;
                cutTimeEnd = globalEnd;
            }
            else
            {
                cutTimeStart = CutTimeStart.Value.HasValue ? (Millisecond.Value ? CutTimeStart.Value.Value : CutTimeStart.Value.Value * 1000d) : double.NaN;
                cutTimeEnd = CutTimeEnd.Value.HasValue ? (Millisecond.Value ? CutTimeEnd.Value.Value : CutTimeEnd.Value.Value * 1000d) : double.NaN;
            }

            var (minTime, maxTime) = beatmap.CalculatePlayableBounds();

            if (double.IsNaN(cutTimeStart) || double.IsInfinity(cutTimeStart))
                cutTimeStart = minTime;

            if (double.IsNaN(cutTimeEnd) || double.IsInfinity(cutTimeEnd))
                cutTimeEnd = maxTime;

            // 计算总偏移量并从编辑器时间中减去，得到原始谱面时间
            // 这是为了与GetOverrides中的音频时间保持一致
            // double totalOffset = getAppliedOffsetMs(pendingWorkingBeatmap ?? throw new InvalidOperationException("WorkingBeatmap not available"));
            // cutTimeStart -= totalOffset;
            // cutTimeEnd -= totalOffset;

            double length = Math.Max(0, cutTimeEnd - cutTimeStart);
            return (cutTimeStart, cutTimeEnd, length);
        }
    }

    public static class LoopPlayClipStrings
    {
        public static readonly LocalisableString LOOP_PLAY_CLIP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "将谱面切割成片段用于循环练习。",
            "Cut the beatmap into a clip for loop practice. (The original is YuLiangSSS's Duplicate Mod)");

        public static readonly LocalisableString LOOP_COUNT_LABEL = new EzLocalizationManager.EzLocalisableString("循环次数", "Loop Count");
        public static readonly LocalisableString LOOP_COUNT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("切片循环次数", "Loop Clip Count.");

        public static readonly LocalisableString CONSTANT_SPEED_LABEL = new EzLocalizationManager.EzLocalisableString("无SV变速", "Constant Speed");
        public static readonly LocalisableString CONSTANT_SPEED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("去除SV变速。（恒定速度/忽略谱面中的变速）", "Constant Speed. No more tricky speed changes.(恒定速度/忽略谱面中的变速)");

        public static readonly LocalisableString CUT_START_TIME_LABEL = new EzLocalizationManager.EzLocalisableString("切片开始时间", "Cut Start Time");
        public static readonly LocalisableString CUT_START_TIME_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("切片开始时间, 默认是秒。推荐通过谱面编辑器A-B控件设置，可自动输入", "Cut StartTime. Default is second.");
        public static readonly LocalisableString CUT_END_TIME_LABEL = new EzLocalizationManager.EzLocalisableString("切片结束时间", "Cut End Time");
        public static readonly LocalisableString CUT_END_TIME_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("切片结束时间, 默认是秒。推荐通过谱面编辑器A-B控件设置，可自动输入", "Cut EndTime. Default is second.");

        public static readonly LocalisableString USE_MILLISECOND_LABEL = new EzLocalizationManager.EzLocalisableString("使用毫秒", "Use Millisecond");
        public static readonly LocalisableString USE_MILLISECOND_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("改为使用ms单位", "Use millisecond(ms).");

        public static readonly LocalisableString USE_GLOBAL_AB_RANGE_LABEL = new EzLocalizationManager.EzLocalisableString("使用全局A-B范围", "Use Global A-B Range");

        public static readonly LocalisableString USE_GLOBAL_AB_RANGE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "始终使用谱面编辑器中A/B空间设置的范围（毫秒）。推荐保持开启",
            "Use global A-B range. Always use the editor A/B range stored for this session (ms).");

        public static readonly LocalisableString BREAK_TIME_LABEL = new EzLocalizationManager.EzLocalisableString("休息时间", "Break Time");

        public static readonly LocalisableString BREAK_TIME_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "设置两个切片循环之间的休息时间（以四分之一拍为单位，范围 1-12，默认 4）",
            "Set the break between clip loops as multiples of 1/4 beat (1-12, default 4).");

        public static readonly LocalisableString RANDOM_LABEL = new EzLocalizationManager.EzLocalisableString("随机", "Random");
        public static readonly LocalisableString RANDOM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("在切片每次重复时进行随机", "Random. Do a Random on every duplicate.");

        public static readonly LocalisableString MIRROR_TIME_LABEL = new EzLocalizationManager.EzLocalisableString("镜像时间", "Mirror Time");
        public static readonly LocalisableString MIRROR_TIME_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("每隔多少次循环做一次镜像", "Mirror Time. Every next time part will be mirrored.");

        public static readonly LocalisableString INFINITE_LOOP_LABEL = new EzLocalizationManager.EzLocalisableString("无限循环", "Infinite Loop");

        public static readonly LocalisableString INFINITE_LOOP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "启用无限循环播放。游戏中必须使用Esc退出才能结束，无法获得成绩结算。",
            "Infinite Loop. Enable infinite loop playback. You must use Esc to exit in the game to end, and you cannot get score settlement.");
    }
}
