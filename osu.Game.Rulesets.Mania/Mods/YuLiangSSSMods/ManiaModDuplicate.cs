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
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    /// <summary>
    /// 基于凉雨的 Duplicate Mod, 解决无循环音频问题；在功能完成后，重命名为CutLoopPlayMod更容易理解
    /// 备注部分为我修改的内容
    /// </summary>
    public class ManiaModDuplicate : Mod, IApplicableAfterBeatmapConversion, IApplicableToTrack, IHasSeed, IApplicableToPlayer, IApplicableToHUD
    {
        private DuplicateVirtualTrack duplicateTrack;
        private bool hasValidCut;
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
            hasValidCut = ResolvedSegmentLength > 0;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            if ((CutTimeStart.Value is null && CutTimeEnd.Value is not null) || (CutTimeStart.Value is not null && CutTimeEnd.Value is null))
            {
                setResolvedCut(null, null);
                return;
            }

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            maniaBeatmap.Breaks.Clear();

            double? cutTimeStart = CutTimeStart.Value * (Millisecond.Value ? 1 : 1000);
            double? cutTimeEnd = CutTimeEnd.Value * (Millisecond.Value ? 1 : 1000);
            double breakTime = BreakTime.Value * 1000;
            double? length = cutTimeEnd - cutTimeStart;

            var selectedPart = maniaBeatmap.HitObjects.Where(h => h.StartTime >= cutTimeStart && h.GetEndTime() <= cutTimeEnd);

            if (CutTimeStart.Value is null && CutTimeEnd.Value is null)
            {
                selectedPart = maniaBeatmap.HitObjects;
                var minTime = maniaBeatmap.HitObjects.MinBy(h => h.StartTime);
                var maxTime = maniaBeatmap.HitObjects.MaxBy(h => h.GetEndTime());

                if (minTime is not null && maxTime is not null)
                {
                    cutTimeStart = minTime.StartTime;
                    cutTimeEnd = maxTime.GetEndTime();
                    length = cutTimeEnd - cutTimeStart;
                }
            }

            if (length is null || length <= 0)
            {
                setResolvedCut(null, null);
                return;
            }

            setResolvedCut(cutTimeStart, cutTimeEnd);

            var newPart = new List<ManiaHitObject>();

            double beatmapLength = maniaBeatmap.BeatmapInfo.Length;

            for (int timeIndex = 0; timeIndex < Time.Value; timeIndex++)
            {
                if (timeIndex == 0)
                {
                    if (Rand.Value)
                    {
                        var shuffledColumns = Enumerable.Range(0, maniaBeatmap.TotalColumns).OrderBy(_ => rng.Next()).ToList();
                        selectedPart.OfType<ManiaHitObject>().ForEach(h => h.Column = shuffledColumns[h.Column]);
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
                            StartTime = note.StartTime + timeIndex * (breakTime + (double)length!),
                            EndTime = note.GetEndTime() + timeIndex * (breakTime + (double)length),
                            NodeSamples = [note.Samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        obj.Add(new Note
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + timeIndex * (breakTime + (double)length!),
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

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
        }

        //将Beatmap给DuplicateVirtualTrack来创建虚拟音轨
        public void ApplyToPlayer(Player player)
        {
            if (ResolvedSegmentLength <= 0)
                return;

            var workingBeatmap = player.Beatmap.Value;
            duplicateTrack = new DuplicateVirtualTrack(workingBeatmap, this);
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

            overlay.Add(duplicateTrack);
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
                return ManiaModDuplicate.CalculateTime(value);
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
                return ManiaModDuplicate.CalculateTime(value);
            }
        }
    }*/
}
