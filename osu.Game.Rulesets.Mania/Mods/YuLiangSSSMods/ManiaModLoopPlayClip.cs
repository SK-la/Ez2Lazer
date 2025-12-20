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
using osu.Game.LAsEzExtensions.Select;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    /// <summary>
    /// 基于凉雨的 Duplicate Mod, 解决无循环音频问题；
    /// <para></para>备注部分为我修改的内容, 增加IApplicableToPlayer, IApplicableToHUD, IPreviewOverrideProvider接口的使用
    /// </summary>
    public class ManiaModLoopPlayClip : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IApplicableToPlayer, IApplicableToHUD, IPreviewOverrideProvider
    {
        private DuplicateVirtualTrack? duplicateTrack;
        private IWorkingBeatmap? pendingWorkingBeatmap;
        internal double? ResolvedCutTimeStart { get; private set; }
        internal double? ResolvedCutTimeEnd { get; private set; }
        internal double ResolvedSegmentLength { get; private set; }
        public override string Name => "Duplicate";

        public override string Acronym => "DL";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Practise more(Default setting if you want to duplicate whole song).";

        public override IconUsage? Icon => FontAwesome.Solid.ArrowCircleDown;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ($"{Time.Value}", "Times");
                yield return ("Start", $"{(CutTimeStart.Value is null ? "Original Start Time" : CalculateTime((int)CutTimeStart.Value))}");
                yield return ("End", $"{(CutTimeEnd.Value is null ? "Original End Time" : CalculateTime((int)CutTimeEnd.Value))}");
                yield return ("Break", $"{BreakTime:N1}s");
            }
        }

        [SettingSource("Time", "Duplicate times.")]
        public BindableInt Time { get; set; } = new BindableInt(20)
        {
            MinValue = 1,
            MaxValue = 100,
            Precision = 1
        };

        /*[SettingSource("Cut Time Start", "Select your part(second).", SettingControlType = typeof(SettingsSlider<int, CutStart>))]
        public BindableInt CutTimeStart { get; set; } = new BindableInt(-10)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };

        [SettingSource("Cut Time End", "Select your part(second).", SettingControlType = typeof(SettingsSlider<int, CutEnd>))]
        public BindableInt CutTimeEnd { get; set; } = new BindableInt(1800)
        {
            MinValue = -10,
            MaxValue = 1800,
            Precision = 1
        };*/

        [SettingSource("Cut Time Start", "Select your part(second).", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeStart { get; set; } = new Bindable<int?>();

        [SettingSource("Cut Time End", "Select your part(second).", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CutTimeEnd { get; set; } = new Bindable<int?>();

        [SettingSource("Use millisecond(for cut time)", "More detailed.")]
        public BindableBool Millisecond { get; set; } = new BindableBool(false);

        [SettingSource("Break Time", "If you need break(second).")]
        public BindableDouble BreakTime { get; set; } = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 0.1
        };

        [SettingSource("Random", "Do a Random on every duplicate.")]
        public BindableBool Rand { get; set; } = new BindableBool(false);

        [SettingSource("Mirror", "Mirror next part.")]
        public BindableBool Mirror { get; set; } = new BindableBool(true);

        [SettingSource("Mirror Time", "Every next time part will be mirrored.")]
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

        [SettingSource("Seed", "Use a custom seed instead of a random one", SettingControlType = typeof(SettingsNumberBox))]
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

                double? cutTimeStart = CutTimeStart.Value * (Millisecond.Value ? 1 : 1000);
                double? cutTimeEnd = CutTimeEnd.Value * (Millisecond.Value ? 1 : 1000);

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

            double? cutTimeStart = CutTimeStart.Value * (Millisecond.Value ? 1 : 1000);
            double? cutTimeEnd = CutTimeEnd.Value * (Millisecond.Value ? 1 : 1000);
            double breakTime = BreakTime.Value * 1000;
            double? length = cutTimeEnd - cutTimeStart;

            // 改为最少一个非空设置
            var minTimeBeatmap = maniaBeatmap.HitObjects.MinBy(h => h.StartTime);
            var maxTimeBeatmap = maniaBeatmap.HitObjects.MaxBy(h => h.GetEndTime());
            cutTimeStart ??= minTimeBeatmap?.StartTime;
            cutTimeEnd ??= maxTimeBeatmap?.GetEndTime();

            var selectedPart = maniaBeatmap.HitObjects.Where(h => h.StartTime >= cutTimeStart && h.GetEndTime() <= cutTimeEnd);

            if (length is null || length <= 0)
            {
                setResolvedCut(null, null);
                return;
            }

            setResolvedCut(cutTimeStart, cutTimeEnd);

            var newPart = new List<ManiaHitObject>();

            for (int timeIndex = 0; timeIndex < Time.Value; timeIndex++)
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
                LoopCount = Time.Value,
                LoopInterval = BreakTime.Value * 1000,
                ForceLooping = true,
                EnableHitSounds = false
            };
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
