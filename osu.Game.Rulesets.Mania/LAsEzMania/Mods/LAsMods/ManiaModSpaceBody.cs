// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.LAsMods
{
    /// <summary>
    /// 需要同时使用IApplicableAfterBeatmapConversion, IHasApplyOrder
    ///否则时序错误
    /// </summary>
    public class ManiaModSpaceBody : Mod, IApplicableAfterBeatmapConversion, IHasApplyOrder
    {
        public override string Name => "Space Body";

        public override string Acronym => "SB";
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => SpaceBodyStrings.SPACE_BODY_DESCRIPTION;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        [SettingSource(typeof(SpaceBodyStrings), nameof(SpaceBodyStrings.SPACE_BODY_LABEL), nameof(SpaceBodyStrings.SPACE_BODY_GAP_DESCRIPTION), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> SpaceBeat { get; } = new BindableDouble(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource(typeof(SpaceBodyStrings), nameof(SpaceBodyStrings.ADD_SHIELD_LABEL), nameof(SpaceBodyStrings.ADD_SHIELD_DESCRIPTION))]
        public BindableBool Shield { get; } = new BindableBool();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();
            var lastHolds = new List<HoldNote>();

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                // 手动构建 locations 并排序，避免使用 Concat/OrderBy 链式 LINQ 的大量中间分配
                var locations = new List<(double startTime, IList<HitSampleInfo> samples)>();

                foreach (var n in column.OfType<Note>())
                    locations.Add((n.StartTime, n.Samples));

                if (Shield.Value)
                {
                    foreach (var h in column.OfType<HoldNote>())
                    {
                        locations.Add((h.StartTime, h.GetNodeSamples(0)));
                        locations.Add((h.EndTime, h.GetNodeSamples(1)));
                    }
                }
                else
                {
                    foreach (var h in column.OfType<HoldNote>())
                        locations.Add((h.StartTime, h.GetNodeSamples(0)));
                }

                locations.Sort((a, b) => a.startTime.CompareTo(b.startTime));

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    // 长按音符的完整持续时间。
                    double duration = locations[i + 1].startTime - locations[i].startTime;

                    // 长按音符结束时的拍长。
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BeatLength;

                    // 减少持续时间最多1/4拍，以确保没有瞬时音符。
                    // duration = Math.Max(duration / 2, duration - beatLength / 4);
                    duration = Math.Max(duration / 2, duration - beatLength / SpaceBeat.Value);

                    newColumnObjects.Add(new HoldNote
                    {
                        Column = Math.Clamp(column.Key, 0, maniaBeatmap.TotalColumns - 1),
                        StartTime = locations[i].startTime,
                        Duration = duration,
                        NodeSamples = new List<IList<HitSampleInfo>> { locations[i].samples, Array.Empty<HitSampleInfo>() }
                    });
                }

                newObjects.AddRange(newColumnObjects);

                if (newColumnObjects.Any())
                {
                    var last = (HoldNote)newColumnObjects.Last();
                    lastHolds.Add(last);
                }
            }

            // 将每列最后一个长按音符的结束时间对齐到下一个 1/4 节拍
            if (lastHolds.Any())
            {
                double maxEndTime = lastHolds.Max(h => h.StartTime + h.Duration);
                var timingPoint = beatmap.ControlPointInfo.TimingPointAt(maxEndTime);
                double beatLength = timingPoint.BeatLength;
                double offset = timingPoint.Time;
                double currentBeats = (maxEndTime - offset) / beatLength;
                double alignedBeats = Math.Ceiling(currentBeats * 4) / 4;
                double alignedEndTime = offset + alignedBeats * beatLength;

                foreach (var last in lastHolds)
                {
                    last.Duration = alignedEndTime - last.StartTime;
                }
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            // 无休息时间
            maniaBeatmap.Breaks.Clear();
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Space Beat", $"1/{SpaceBeat.Value}");
                yield return ("Shield", Shield.Value ? "On" : "Off");
            }
        }
    }

    public static class SpaceBodyStrings
    {
        public static readonly LocalisableString SPACE_BODY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("全LN面海，可调面缝", "Full LN, adjustable gaps");
        public static readonly LocalisableString SPACE_BODY_LABEL = new EzLocalizationManager.EzLocalisableString("全反键缝隙", "Space Body");
        public static readonly LocalisableString SPACE_BODY_GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("调整前后两个面之间的间隔缝隙", "Full LN, adjustable gaps");
        public static readonly LocalisableString ADD_SHIELD_LABEL = new EzLocalizationManager.EzLocalisableString("添加盾型", "Add Shield");
        public static readonly LocalisableString ADD_SHIELD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("将每个面尾添加盾牌键型", "Add shield notes in the sea");
    }
}
