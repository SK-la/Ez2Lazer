// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Select;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Mods
{
    /// <summary>
    /// 继承Mod需要自己实现IApplicableToPlayer, IApplicableToHUD, IPreviewOverrideProvider接口的使用
    /// </summary>
    public class ModLoopPlayClip : Mod,
                                   IHasSeed,
                                   IPreviewOverrideProvider,
                                   ILoopTimeRangeMod,
                                   IApplicableToPlayer,
                                   IApplicableToHUD,
                                   IApplicableFailOverride,
                                   IApplicableToRate
    {
        private DuplicateVirtualTrack? duplicateTrack;
        private IWorkingBeatmap? pendingWorkingBeatmap;
        internal double? ResolvedCutTimeStart { get; private set; }
        internal double? ResolvedCutTimeEnd { get; private set; }
        internal double ResolvedSegmentLength { get; private set; }
        public override string Name => "Loop Play Clip (No Fail)";

        public override string Acronym => "LP";
        public override LocalisableString Description => EzModStrings.LoopPlayClip_Description;

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.ArrowCircleDown;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForFreestyleAsRequiredMod => false;

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
                yield return ("Start", $"{(CutTimeStart.Value is null ? "Original Start Time" : (Millisecond.Value ? $"{CutTimeStart.Value} ms" : CalculateTime((int)CutTimeStart.Value)))}");
                yield return ("End", $"{(CutTimeEnd.Value is null ? "Original End Time" : (Millisecond.Value ? $"{CutTimeEnd.Value} ms" : CalculateTime((int)CutTimeEnd.Value)))}");
                yield return ("Infinite Loop", InfiniteLoop.Value ? "Enabled" : "Disabled");
            }
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

        private readonly RateAdjustModHelper rateAdjustHelper;

        public ModLoopPlayClip()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            UseGlobalAbRange.BindValueChanged(_ => applyRangeFromStore(), true);

            // 当全局A/B范围改变时，更新设置
            LoopTimeRangeStore.START_TIME_MS.BindValueChanged(_ => applyRangeFromStoreIfGlobal());
            LoopTimeRangeStore.END_TIME_MS.BindValueChanged(_ => applyRangeFromStoreIfGlobal());
        }

        public void ApplyToTrack(IAdjustableAudioComponent track) => rateAdjustHelper.ApplyToTrack(track);

        public void ApplyToSample(IAdjustableAudioComponent sample)
        {
            // 与 ModRateAdjust 一致：sample 仅做音高/频率调整即可。
            sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);
        }

        public double ApplyToRate(double time, double rate = 1) => rate * SpeedChange.Value;

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            applyRangeFromStore();
        }

        private void applyRangeFromStore()
        {
            if (!UseGlobalAbRange.Value)
                return;

            if (!LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                return;

            // Store is always milliseconds.
            setCutTimeFromMs(startMs, endMs);
            SetResolvedCut(null, null);
        }

        private void applyRangeFromStoreIfGlobal()
        {
            if (UseGlobalAbRange.Value)
                applyRangeFromStore();
        }

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

        // 提供切片时间点给 DuplicateVirtualTrack 使用
        protected void SetResolvedCut(double? start, double? end)
        {
            ResolvedCutTimeStart = start;
            ResolvedCutTimeEnd = end;
            ResolvedSegmentLength = start.HasValue && end.HasValue ? Math.Max(0, end.Value - start.Value) : 0;
        }

        // 获取当前生效的切片起止时间（毫秒）
        protected (double? startMs, double? endMs) GetEffectiveCutTimeMs()
        {
            if (UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                return (startMs, endMs);

            return (cutTimeStartMs, cutTimeEndMs);
        }

        private bool ensureResolvedForPreview(IWorkingBeatmap beatmap)
        {
            if (ResolvedSegmentLength > 0 && ResolvedCutTimeStart is not null && ResolvedCutTimeEnd is not null)
                return true;

            try
            {
                var maniaBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                var (cutTimeStart, cutTimeEnd) = GetEffectiveCutTimeMs();

                // 若开始为空则取最早物件时间，若结束为空则取最晚物件时间（不再整体判无效）。
                var minTime = maniaBeatmap.HitObjects.MinBy(h => h.StartTime);
                var maxTime = maniaBeatmap.HitObjects.MaxBy(h => h.GetEndTime());
                cutTimeStart ??= minTime?.StartTime;
                cutTimeEnd ??= maxTime?.GetEndTime();

                double? length = cutTimeEnd - cutTimeStart;

                if (length is null || length <= 0)
                {
                    SetResolvedCut(null, null);
                    return false;
                }

                SetResolvedCut(cutTimeStart, cutTimeEnd);
                return true;
            }
            catch
            {
                SetResolvedCut(null, null);
                return false;
            }
        }

        // 将 Beatmap 交给 DuplicateVirtualTrack，用独立 Track 实例按切片参数播放
        public void ApplyToPlayer(Player player)
        {
            if (ResolvedSegmentLength <= 0)
                return;

            pendingWorkingBeatmap = player.Beatmap.Value;

            duplicateTrack = new DuplicateVirtualTrack
            {
                OverrideProvider = this,
                PendingOverrides = null,
            };
        }

        public static string CalculateTime(double time)
        {
            int minute = Math.Abs((int)time / 60);
            double second = Math.Abs(time % 60);
            string minus = time < 0 ? "-" : string.Empty;
            string secondLessThan10 = second < 10 ? "0" : string.Empty;
            return $"{minus}{minute}:{secondLessThan10}{second:N1}";
        }

        // 需要有一个Drawable来承载虚拟音轨
        public void ApplyToHUD(HUDOverlay overlay)
        {
            if (duplicateTrack == null)
                return;

            if (pendingWorkingBeatmap == null)
                return;

            overlay.Add(duplicateTrack);
            duplicateTrack.StartPreview(pendingWorkingBeatmap);
        }

        public PreviewOverrideSettings? GetPreviewOverrides(IWorkingBeatmap beatmap)
        {
            if (!ensureResolvedForPreview(beatmap))
                return null;

            return new PreviewOverrideSettings
            {
                PreviewStart = ResolvedCutTimeStart,
                PreviewDuration = ResolvedSegmentLength,
                LoopCount = LoopCount.Value,
                LoopInterval = BreakTime.Value * 1000,
                ForceLooping = true,
                EnableHitSounds = false
            };
        }

        public void SetLoopTimeRange(double startTime, double endTime)
        {
            if (endTime <= startTime)
                return;

            LoopTimeRangeStore.Set(startTime, endTime);

            // The editor timeline works in milliseconds, while this mod exposes seconds by default.
            setCutTimeFromMs(startTime, endTime);

            // Reset preview cache so changes take effect immediately where used.
            SetResolvedCut(null, null);

            // Keep current instance in sync with the session store when global mode is enabled.
            if (UseGlobalAbRange.Value)
                applyRangeFromStore();
        }

        public bool PerformFail() => false;

        public bool RestartOnFail => false;

        // 简化后的统一参数访问器，自动适配全局/本地，单位换算集中
        private double? cutTimeStartMs
        {
            get => UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double startMs, out _)
                ? startMs
                : toMs(CutTimeStart.Value);
            set
            {
                if (UseGlobalAbRange.Value)
                    setGlobalRange(value, cutTimeEndMs);
                else
                    CutTimeStart.Value = fromMs(value);
            }
        }

        private double? cutTimeEndMs
        {
            get => UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out _, out double endMs)
                ? endMs
                : toMs(CutTimeEnd.Value);
            set
            {
                if (UseGlobalAbRange.Value)
                    setGlobalRange(cutTimeStartMs, value);
                else
                    CutTimeEnd.Value = fromMs(value);
            }
        }

        // 工具方法，集中单位换算和全局写入
        private double? toMs(int? v) => v == null ? null : v * (Millisecond.Value ? 1 : 1000);
        private int? fromMs(double? ms) => ms == null ? null : (Millisecond.Value ? (int)ms : (int)(ms / 1000d));
        private void setGlobalRange(double? start, double? end) => LoopTimeRangeStore.Set(start ?? 0, end ?? 0);

        // 新增：根据当前单位设置 CutTimeStart/End
        private void setCutTimeFromMs(double startMs, double endMs)
        {
            CutTimeStart.Value = Millisecond.Value ? (int)startMs : (int)(startMs / 1000d);
            CutTimeEnd.Value = Millisecond.Value ? (int)endMs : (int)(endMs / 1000d);
        }
    }

    /*public partial class CutStart : RoundedSliderBar<double>
    {
        public override LocalisableString TooltipText
        {
            get
            {
                double value = Current.Value;
                if (value == -10)
                {
                    return "Original Start Time";
                }
                return ManiaModLoopPlayClip.CalculateTime(value);
            }
        }
    }

    public partial class CutEnd : RoundedSliderBar<double>
    {
        public override LocalisableString TooltipText
        {
            get
            {
                double value = Current.Value;
                if (value == 1800)
                {
                    return "Original End Time";
                }
                return ManiaModLoopPlayClip.CalculateTime(value);
            }
        }
    }*/
}
