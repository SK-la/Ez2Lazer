// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Select;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    /// <summary>
    /// 基于凉雨的 Duplicate Mod, 解决无循环音频问题；
    /// <para></para>备注部分为我修改的内容, 增加IApplicableToPlayer, IApplicableToHUD, IPreviewOverrideProvider接口的使用
    /// </summary>
    public class ManiaModLoopPlayClip : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IApplicableToPlayer, IApplicableToHUD, IPreviewOverrideProvider, ILoopTimeRangeMod, IApplicableFailOverride, IApplicableToRate, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        private DuplicateVirtualTrack? duplicateTrack;
        private IWorkingBeatmap? pendingWorkingBeatmap;
        internal double? ResolvedCutTimeStart { get; private set; }
        internal double? ResolvedCutTimeEnd { get; private set; }
        internal double ResolvedSegmentLength { get; private set; }
        public override string Name => "Loop Play Clip (No Fail)";

        public override string Acronym => "LP";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => EzManiaModStrings.LoopPlayClip_Description;

        public override IconUsage? Icon => FontAwesome.Solid.ArrowCircleDown;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        // LP 内置变速（复刻 HT 的实现）后，为避免叠加导致体验混乱，直接与其它变速 Mod 互斥。
        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModRateAdjust),
            typeof(ModTimeRamp),
            typeof(ModAdaptiveSpeed),
            typeof(ManiaModConstantSpeed),
            typeof(ManiaModNoFail),
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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.LoopCount_Label), nameof(EzManiaModStrings.LoopCount_Description))]
        public BindableInt LoopCount { get; set; } = new BindableInt(20)
        {
            MinValue = 1,
            MaxValue = 100,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.SpeedChange_Label), nameof(EzManiaModStrings.SpeedChange_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.5,
            MaxValue = 2.0,
            Precision = 0.01,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.AdjustPitch_Label), nameof(EzManiaModStrings.AdjustPitch_Description))]
        public BindableBool AdjustPitch { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ConstantSpeed_Label), nameof(EzManiaModStrings.ConstantSpeed_Description))]
        public BindableBool ConstantSpeed { get; } = new BindableBool(true);

        /*[SettingSource("Cut Time Start", "Select your part(second).", SettingControlType = typeof(SettingsSlider<int, CutStart>))]
        public BindableInt CutTimeStart { get; set; } = new BindableInt(-10)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CutTimeEnd_Label), nameof(EzManiaModStrings.CutTimeEnd_Description), SettingControlType = typeof(SettingsSlider<int, CutEnd>))]
        public BindableInt CutTimeEnd { get; set; } = new BindableInt(1800)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };*/

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CutStartTime_Label), nameof(EzManiaModStrings.CutStartTime_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeStart { get; set; } = new Bindable<int?>();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CutEndTime_Label), nameof(EzManiaModStrings.CutEndTime_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeEnd { get; set; } = new Bindable<int?>();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.UseMillisecond_Label), nameof(EzManiaModStrings.UseMillisecond_Description))]
        public BindableBool Millisecond { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.UseGlobalABRange_Label), nameof(EzManiaModStrings.UseGlobalABRange_Description))]
        public BindableBool UseGlobalAbRange { get; set; } = new BindableBool(true);

        private readonly RateAdjustModHelper rateAdjustHelper;

        public ManiaModLoopPlayClip()
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

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            if (!ConstantSpeed.Value)
                return;

            if (drawableRuleset is DrawableManiaRuleset maniaRuleset)
                maniaRuleset.VisualisationMethod = ScrollVisualisationMethod.Constant;
        }

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
            setResolvedCut(null, null);
        }

        private void applyRangeFromStoreIfGlobal()
        {
            if (UseGlobalAbRange.Value)
                applyRangeFromStore();
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.BreakTime_Label), nameof(EzManiaModStrings.BreakTime_Description))]
        public BindableDouble BreakTime { get; set; } = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 0.1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Random_Label), nameof(EzManiaModStrings.Random_Description))]
        public BindableBool Rand { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Mirror_Label), nameof(EzManiaModStrings.Mirror_Description))]
        public BindableBool Mirror { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.InfiniteLoop_Label), nameof(EzManiaModStrings.InfiniteLoop_Description))]
        public BindableBool InfiniteLoop { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.MirrorTime_Label), nameof(EzManiaModStrings.MirrorTime_Description))]
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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Seed_Label), nameof(EzManiaModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        // 提供切片时间点给 DuplicateVirtualTrack 使用
        private void setResolvedCut(double? start, double? end)
        {
            ResolvedCutTimeStart = start;
            ResolvedCutTimeEnd = end;
            ResolvedSegmentLength = start.HasValue && end.HasValue ? Math.Max(0, end.Value - start.Value) : 0;
        }

        private bool ensureResolvedForPreview(IWorkingBeatmap beatmap)
        {
            if (ResolvedSegmentLength > 0 && ResolvedCutTimeStart is not null && ResolvedCutTimeEnd is not null)
                return true;

            try
            {
                var maniaBeatmap = (ManiaBeatmap)beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                var (cutTimeStart, cutTimeEnd) = getEffectiveCutTimeMs();

                // 若开始为空则取最早物件时间，若结束为空则取最晚物件时间（不再整体判无效）。
                var minTime = maniaBeatmap.HitObjects.MinBy(h => h.StartTime);
                var maxTime = maniaBeatmap.HitObjects.MaxBy(h => h.GetEndTime());
                cutTimeStart ??= minTime?.StartTime;
                cutTimeEnd ??= maxTime?.GetEndTime();

                double? length = cutTimeEnd - cutTimeStart;

                if (length is null || length <= 0)
                {
                    setResolvedCut(null, null);
                    return false;
                }

                setResolvedCut(cutTimeStart, cutTimeEnd);
                return true;
            }
            catch
            {
                setResolvedCut(null, null);
                return false;
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            maniaBeatmap.Breaks.Clear();

            var (cutTimeStart, cutTimeEnd) = getEffectiveCutTimeMs();

            double breakTime = BreakTime.Value * 1000;
            double? length = cutTimeEnd - cutTimeStart;

            // 改为最少一个非空设置
            var minTimeBeatmap = maniaBeatmap.HitObjects.MinBy(h => h.StartTime);
            var maxTimeBeatmap = maniaBeatmap.HitObjects.MaxBy(h => h.GetEndTime());
            cutTimeStart ??= minTimeBeatmap?.StartTime;
            cutTimeEnd ??= maxTimeBeatmap?.GetEndTime();

            var selectedPart = maniaBeatmap.HitObjects.Where(h => h.StartTime > cutTimeStart && h.GetEndTime() < cutTimeEnd).ToList();

            if (length is null || length <= 0)
            {
                setResolvedCut(null, null);
                return;
            }

            setResolvedCut(cutTimeStart, cutTimeEnd);

            var newPart = new List<ManiaHitObject>();

            for (int timeIndex = 0; timeIndex < LoopCount.Value; timeIndex++)
            {
                if (timeIndex == 0)
                {
                    if (Rand.Value)
                    {
                        var shuffledColumns = Enumerable.Range(0, maniaBeatmap.TotalColumns).OrderBy(_ => rng.Next()).ToList();
                        selectedPart.ForEach(h => h.Column = shuffledColumns[h.Column]);
                    }

                    if (Mirror.Value)
                    {
                    }

                    // 调整时间从切片起点开始
                    foreach (var note in selectedPart)
                    {
                        note.StartTime -= (float)cutTimeStart!;
                        if (note is HoldNote holdNote)
                            holdNote.EndTime -= (float)cutTimeStart;
                    }

                    newPart.AddRange(selectedPart);
                    continue;
                }

                var obj = new List<ManiaHitObject>();

                foreach (var note in selectedPart)
                {
                    if (note.GetEndTime() != note.StartTime)
                    {
                        obj.Add(new HoldNote
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + timeIndex * (breakTime + (double)length),
                            EndTime = note.GetEndTime() + timeIndex * (breakTime + (double)length),
                            NodeSamples = [note.Samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        obj.Add(new Note
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + timeIndex * (breakTime + (double)length),
                            Samples = note.Samples,
                        });
                    }
                }

                if (Rand.Value)
                {
                    var shuffledColumns = Enumerable.Range(0, maniaBeatmap.TotalColumns).OrderBy(_ => rng.Next()).ToList();
                    obj.OfType<ManiaHitObject>().ForEach(h => h.Column = shuffledColumns[h.Column]);
                }

                newPart.AddRange(obj);
            }

            maniaBeatmap.HitObjects = newPart;
        }

        // 将 Beatmap 交给 DuplicateVirtualTrack，用独立 Track 实例按切片参数播放
        public void ApplyToPlayer(Player player)
        {
            if (ResolvedSegmentLength <= 0)
                return;

            pendingWorkingBeatmap = player.Beatmap.Value;

            // 计算总循环长度
            double totalLength = InfiniteLoop.Value ? double.MaxValue : LoopCount.Value * (ResolvedSegmentLength + BreakTime.Value * 1000);
            pendingWorkingBeatmap.Track.Length = totalLength;

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
            setResolvedCut(null, null);

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

        // 获取当前生效的切片起止时间（毫秒）
        private (double? startMs, double? endMs) getEffectiveCutTimeMs()
        {
            if (UseGlobalAbRange.Value && LoopTimeRangeStore.TryGet(out double startMs, out double endMs))
                return (startMs, endMs);

            return (cutTimeStartMs, cutTimeEndMs);
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
