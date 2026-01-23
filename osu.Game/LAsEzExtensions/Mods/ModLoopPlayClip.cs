// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Audio;
using osu.Game.LAsEzExtensions.Configuration;
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
                                   IApplicableToRate
    {
        public override string Name => "Loop Play Clip (No Fail)";

        public override string Acronym => "LP";
        public override LocalisableString Description => EzModStrings.LoopPlayClip_Description;

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.ArrowCircleDown;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public bool PerformFail() => false;

        public bool RestartOnFail => false;

        // LP 内置变速（复刻 HT 的实现）后，为避免叠加导致体验混乱，直接与其它变速 Mod 互斥。
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
                yield return ("Break", $"{BreakTime:N1}s");
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

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.LoopCount_Label), nameof(EzModStrings.LoopCount_Description))]
        public BindableInt LoopCount { get; set; } = new BindableInt(20)
        {
            MinValue = 1,
            MaxValue = 100,
            Precision = 1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.SpeedChange_Label), nameof(EzModStrings.SpeedChange_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.5,
            MaxValue = 2.0,
            Precision = 0.01,
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.AdjustPitch_Label), nameof(EzModStrings.AdjustPitch_Description))]
        public BindableBool AdjustPitch { get; } = new BindableBool();

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.ConstantSpeed_Label), nameof(EzModStrings.ConstantSpeed_Description))]
        public BindableBool ConstantSpeed { get; } = new BindableBool(true);

        /*[SettingSource("Cut Time Start", "Select your part(second).", SettingControlType = typeof(SettingsSlider<int, CutStart>))]
        public BindableInt CutTimeStart { get; set; } = new BindableInt(-10)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.CutTimeEnd_Label), nameof(EzModStrings.CutTimeEnd_Description), SettingControlType = typeof(SettingsSlider<int, CutEnd>))]
        public BindableInt CutTimeEnd { get; set; } = new BindableInt(1800)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };*/

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.CutStartTime_Label), nameof(EzModStrings.CutStartTime_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeStart { get; set; } = new Bindable<int?>();

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.CutEndTime_Label), nameof(EzModStrings.CutEndTime_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeEnd { get; set; } = new Bindable<int?>();

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.UseMillisecond_Label), nameof(EzModStrings.UseMillisecond_Description))]
        public BindableBool Millisecond { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.UseGlobalABRange_Label), nameof(EzModStrings.UseGlobalABRange_Description))]
        public BindableBool UseGlobalAbRange { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.BreakTime_Label), nameof(EzModStrings.BreakTime_Description))]
        public BindableDouble BreakTime { get; set; } = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 0.1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Random_Label), nameof(EzModStrings.Random_Description))]
        public BindableBool Rand { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Mirror_Label), nameof(EzModStrings.Mirror_Description))]
        public BindableBool Mirror { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.InfiniteLoop_Label), nameof(EzModStrings.InfiniteLoop_Description))]
        public BindableBool InfiniteLoop { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.MirrorTime_Label), nameof(EzModStrings.MirrorTime_Description))]
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

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Seed_Label), nameof(EzModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

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

            return new OverrideSettings
            {
                StartTime = start,
                Duration = Math.Max(0, end - start),
                LoopCount = LoopCount.Value,
                LoopInterval = BreakTime.Value * 1000,
                ForceLooping = true,
            };
        }

        // 工具方法，集中单位换算和全局写入
        private double toMs(int? v) => v == null ? 0.0 : (int)v * (Millisecond.Value ? 1 : 1000);
        private void setGlobalRange(double? start, double? end) => LoopTimeRangeStore.Set(start ?? 0, end ?? 0);

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
            if (duplicateTrack.Parent is osu.Framework.Graphics.Containers.Container c)
                c.Remove(duplicateTrack, false);
        }

        /// <summary>
        /// 解析给定 <see cref="IBeatmap"/> 的切片时间（毫秒），优先使用全局 A/B 范围，
        /// 否则使用设置中的值（支持秒/毫秒模式），缺失时回退到谱面可播放边界。
        /// 返回 (start, end, length)。
        /// </summary>
        protected (double start, double end, double length) ResolveSliceTimesForBeatmap(IBeatmap beatmap)
        {
            if (UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double globalStart, out double globalEnd))
                return (globalStart, globalEnd, Math.Max(0, globalEnd - globalStart));

            double cutTimeStart = CutTimeStart.Value.HasValue ? (Millisecond.Value ? CutTimeStart.Value.Value : CutTimeStart.Value.Value * 1000d) : double.NaN;
            double cutTimeEnd = CutTimeEnd.Value.HasValue ? (Millisecond.Value ? CutTimeEnd.Value.Value : CutTimeEnd.Value.Value * 1000d) : double.NaN;

            var (minTime, maxTime) = beatmap.CalculatePlayableBounds();

            if (double.IsNaN(cutTimeStart) || double.IsInfinity(cutTimeStart))
                cutTimeStart = minTime;

            if (double.IsNaN(cutTimeEnd) || double.IsInfinity(cutTimeEnd))
                cutTimeEnd = maxTime;

            double length = Math.Max(0, cutTimeEnd - cutTimeStart);
            return (cutTimeStart, cutTimeEnd, length);
        }
    }
}
