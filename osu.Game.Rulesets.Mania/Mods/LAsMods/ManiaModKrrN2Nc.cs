// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Mods.KrrConversion;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModKrrN2Nc : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IHasApplyOrder
    {
        public override string Name => "Krr N2Nc";

        public override string Acronym => "N2N";

        public override LocalisableString Description => "[KrrTool] KeyCounts conversion";

        public override double ScoreMultiplier => 1;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrN2Nc_TargetKeys_Label), nameof(EzManiaModStrings.KrrN2Nc_TargetKeys_Description))]
        public BindableNumber<int> TargetKeys { get; } = new BindableInt(8)
        {
            MinValue = 1,
            MaxValue = 18,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrN2Nc_MaxKeys_Label), nameof(EzManiaModStrings.KrrN2Nc_MaxKeys_Description))]
        public BindableNumber<int> MaxKeys { get; } = new BindableInt(6)
        {
            MinValue = 0,
            MaxValue = 10
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrN2Nc_MinKeys_Label), nameof(EzManiaModStrings.KrrN2Nc_MinKeys_Description))]
        public BindableNumber<int> MinKeys { get; } = new BindableInt(2)
        {
            MinValue = 0,
            MaxValue = 10
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.BeatSpeed_Label), nameof(EzManiaModStrings.BeatSpeed_Description))]
        public BindableNumber<int> BeatSpeed { get; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 8
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Seed_Label), nameof(EzModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrN2Nc_DisplayTarget_Label), nameof(EzManiaModStrings.KrrN2Nc_DisplayTarget_Description))]
        public BindableBool DisplayTargetKeys { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ApplyOrder_Label), nameof(EzManiaModStrings.ApplyOrder_Description))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Target Keys", $"{TargetKeys.Value}");
                yield return ("Beat Speed", $"{BeatSpeed.Value}");

                if (Seed.Value is null)
                    yield return ("Seed", "Null");
                else
                    yield return ("Seed", $"Seed {Seed.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var options = new KrrOptions
            {
                TargetKeys = TargetKeys.Value,
                MaxKeys    = MaxKeys.Value,
                MinKeys    = MinKeys.Value,
                BeatSpeed = BeatSpeed.Value,
                Seed       = Seed.Value
            };

            // 转换器内部负责：先重建对象，再更新列数
            KrrN2NcConverter.Transform(maniaBeatmap, options);

            // 最后更新谱面总列数，避免越界
            try
            {
                maniaBeatmap.Stages.Clear();
                maniaBeatmap.Stages.Add(new StageDefinition(options.TargetKeys));
                maniaBeatmap.Difficulty.CircleSize = options.TargetKeys;
            }
            catch (Exception ex)
            {
                Logger.Log($"[ManiaModKrrN2Nc] Failed to update stages: {ex.Message}");
            }
        }
    }

    public class KrrOptions
    {
        public int TargetKeys { get; set; } = 8;
        public int MaxKeys { get; set; } = 4;
        public int MinKeys { get; set; } = 1;
        public int BeatSpeed { get; set; } = 4;
        public int? Seed { get; set; }
    }
}
