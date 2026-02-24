// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Mania.LAsEZMania.Analysis;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.LAsMods
{
    public class ManiaModSRAdjust : Mod, IApplicableToDifficulty
    {
        public override string Name => "SR Adjust";

        public override string Acronym => "SRA";

        public override LocalisableString Description => SRAdjustStrings.SR_DESCRIPTION;

        public override ModType Type => ModType.LA_Mod;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        [SettingSource(typeof(SRAdjustStrings), nameof(SRAdjustStrings.SR_ADJUST_RESCALE_THRESHOLD_LABEL), nameof(SRAdjustStrings.SR_ADJUST_RESCALE_THRESHOLD_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> RescaleThreshold { get; } = new BindableDouble(SRCalculator.RescaleHighThreshold)
        {
            MinValue = 5,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(SRAdjustStrings), nameof(SRAdjustStrings.SR_ADJUST_LN_MULTIPLIER_LABEL), nameof(SRAdjustStrings.SR_ADJUST_LN_MULTIPLIER_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> LnMultiplier { get; } = new BindableDouble(SRCalculator.LnIntegralMultiplier)
        {
            MinValue = 2,
            MaxValue = 8,
            Precision = 0.5
        };

        public ManiaModSRAdjust()
        {
            RescaleThreshold.BindValueChanged(e => SRCalculator.RescaleHighThreshold = e.NewValue, true);
            LnMultiplier.BindValueChanged(e => SRCalculator.LnIntegralMultiplier = e.NewValue, true);
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (SRAdjustStrings.SR_ADJUST_RESCALE_THRESHOLD_LABEL, new LocalisableString(RescaleThreshold.Value.ToString(CultureInfo.InvariantCulture)));
                yield return (SRAdjustStrings.SR_ADJUST_LN_MULTIPLIER_LABEL, new LocalisableString(LnMultiplier.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
        }
    }

    public static class SRAdjustStrings
    {
        public static readonly LocalisableString SR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "调整 SR 计算中的重缩放阈值和 LN 因子, 影响难度卡上的SR(月亮星)数值。",
            "Adjust rescale threshold and LN multiplier in SR calculation. Affects the SR (star rating) value shown on difficulty cards.");

        public static readonly LocalisableString SR_ADJUST_RESCALE_THRESHOLD_LABEL = new EzLocalizationManager.EzLocalisableString("重缩放阈值", "Rescale Threshold");
        public static readonly LocalisableString SR_ADJUST_RESCALE_THRESHOLD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("超过此阈值后将降低难度膨胀速度", "Reduce difficulty inflation speed when above this threshold.");
        public static readonly LocalisableString SR_ADJUST_LN_MULTIPLIER_LABEL = new EzLocalizationManager.EzLocalisableString("LN 因子", "LN Integral Multiplier");
        public static readonly LocalisableString SR_ADJUST_LN_MULTIPLIER_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("LN 因子", "LN integral multiplier.");
    }
}
