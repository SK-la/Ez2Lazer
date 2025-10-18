// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLN : Mod, IHasSeed
    {
        public override string Name => "LN";

        public override string Acronym => "LN";

        public override LocalisableString Description => "LN";

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.CustomMod;
        public override bool Ranked => false;

        [SettingSource("Divide", "Use 1/?")]
        public BindableNumber<int> Divide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource("Percentage", "LN Content")]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 5,
            MaxValue = 100,
            Precision = 5
        };

        [SettingSource("Original LN", "Original LN won't be converted.")]
        public BindableBool OriginalLN { get; set; } = new BindableBool(false);

        [SettingSource("Column Num", "Select the number of column to transform.")]
        public BindableInt SelectColumn { get; set; } = new BindableInt(10)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource("Gap", "For changing random columns after transforming the gap's number of notes.")]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource("Line Spacing", "Transform every line when set to 0.")]
        public BindableInt LineSpacing { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource("Invert Line Spacing", "Invert the Line Spacing.")]
        public BindableBool InvertLineSpacing { get; set; } = new BindableBool(false);

        [SettingSource("Duration Limit", "The max duration(second) of a LN.(No limit when set to 0)")]
        public BindableDouble DurationLimit { get; set; } = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 15,
            Precision = 0.5
        };

        [SettingSource("Seed", "Use a custom seed instead of a random one.", SettingControlType = typeof(SettingsNumberBox))]
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
