// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLN : Mod, IHasSeed
    {
        public override string Name => "LN";

        public override string Acronym => "LN";

        public override LocalisableString Description => EzManiaModStrings.LN_Description;

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.YuLiangSSS_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Divide_Label), nameof(EzManiaModStrings.Divide_Description))]
        public BindableNumber<int> Divide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Percentage_Label), nameof(EzManiaModStrings.Percentage_Description))]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 5,
            MaxValue = 100,
            Precision = 5
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.OriginalLN_Label), nameof(EzManiaModStrings.OriginalLN_Description))]
        public BindableBool OriginalLN { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ColumnNum_Label), nameof(EzManiaModStrings.ColumnNum_Description))]
        public BindableInt SelectColumn { get; set; } = new BindableInt(10)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Gap_Label), nameof(EzManiaModStrings.Gap_Description))]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.LineSpacing_Label), nameof(EzManiaModStrings.LineSpacing_Description))]
        public BindableInt LineSpacing { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.InvertLineSpacing_Label), nameof(EzManiaModStrings.InvertLineSpacing_Description))]
        public BindableBool InvertLineSpacing { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DurationLimit_Label), nameof(EzManiaModStrings.DurationLimit_Description))]
        public BindableDouble DurationLimit { get; set; } = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 15,
            Precision = 0.5
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Seed_Label), nameof(EzManiaModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Divide", $"1/{Divide.Value}");
                yield return ("Percentage", $"{Percentage.Value}%");

                if (OriginalLN.Value) yield return ("Original LN", "On");

                yield return ("Column Num", $"{SelectColumn.Value}");
                yield return ("Gap", $"{Gap.Value}");

                if (DurationLimit.Value > 0) yield return ("Duration Limit", $"{DurationLimit.Value}s");

                yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
            }
        }
    }
}
