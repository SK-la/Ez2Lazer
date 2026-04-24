// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.YuLiangSSSMods
{
    public class ManiaModLNBase : Mod, IHasSeed, IEzApplyOrder
    {
        public override string Name => "LN";

        public override string Acronym => "LN";

        public override LocalisableString Description => LNStrings.LN_DESCRIPTION;

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.YuLiangSSS_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(LNStrings), nameof(LNStrings.DIVIDE_LABEL), nameof(LNStrings.DIVIDE_DESCRIPTION))]
        public BindableNumber<int> Divide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.PERCENTAGE_LABEL), nameof(EzCommonModStrings.PERCENTAGE_DESCRIPTION))]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 5,
            MaxValue = 100,
            Precision = 5
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ORIGINAL_LN_LABEL), nameof(EzCommonModStrings.ORIGINAL_LN_DESCRIPTION))]
        public BindableBool OriginalLN { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.COLUMN_NUM_LABEL), nameof(EzCommonModStrings.COLUMN_NUM_DESCRIPTION))]
        public BindableInt SelectColumn { get; set; } = new BindableInt(10)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.GAP_LABEL), nameof(EzCommonModStrings.GAP_DESCRIPTION))]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.LINE_SPACING_LABEL), nameof(EzCommonModStrings.LINE_SPACING_DESCRIPTION))]
        public BindableInt LineSpacing { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.INVERT_LINE_SPACING_LABEL), nameof(EzCommonModStrings.INVERT_LINE_SPACING_DESCRIPTION))]
        public BindableBool InvertLineSpacing { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.DURATION_LIMIT_LABEL), nameof(EzCommonModStrings.DURATION_LIMIT_DESCRIPTION))]
        public BindableDouble DurationLimit { get; set; } = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 15,
            Precision = 0.5
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (LNStrings.DIVIDE_LABEL, $"1/{Divide.Value}");
                yield return (EzCommonModStrings.PERCENTAGE_LABEL, $"{Percentage.Value}%");

                if (OriginalLN.Value) yield return (EzCommonModStrings.ORIGINAL_LN_LABEL, "On");

                yield return (EzCommonModStrings.COLUMN_NUM_LABEL, $"{SelectColumn.Value}");
                yield return (EzCommonModStrings.GAP_LABEL, $"{Gap.Value}");

                if (DurationLimit.Value > 0) yield return (EzCommonModStrings.DURATION_LIMIT_LABEL, $"{DurationLimit.Value}s");

                yield return (EzCommonModStrings.SEED_LABEL, $"{(Seed.Value == null ? "Null" : Seed.Value)}");
            }
        }
    }

    public static class LNStrings
    {
        public static readonly LocalisableString LN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("LN转换器", "LN Transformer");
        public static readonly LocalisableString DIVIDE_LABEL = new EzLocalizationManager.EzLocalisableString("分割", "Divide");
        public static readonly LocalisableString DIVIDE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("使用1/?", "Use 1/?");
    }
}
