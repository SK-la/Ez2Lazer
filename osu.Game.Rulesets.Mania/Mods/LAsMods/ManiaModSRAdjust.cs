// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Mania.LAsEZMania.Analysis;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModSRAdjust : Mod, IApplicableToDifficulty
    {
        public override string Name => "SR Adjust";

        public override string Acronym => "SRA";

        public override LocalisableString Description => "修正xxySR计算中的一些系数。影响的是难度卡上的SR(月亮星)数值。";

        public override ModType Type => ModType.LA_Mod;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        [SettingSource("Rescale Threshold", "超过此阈值后将降低难度膨胀速度", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> RescaleThreshold { get; } = new BindableDouble(SRCalculator.RescaleHighThreshold)
        {
            MinValue = 5,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource("LN Integral Multiplier", "LN 因子", SettingControlType = typeof(MultiplierSettingsSlider))]
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
                yield return ("Rescale Threshold", new LocalisableString(RescaleThreshold.Value.ToString(CultureInfo.InvariantCulture)));
                yield return ("LN Integral Multiplier", new LocalisableString(LnMultiplier.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
        }
    }
}
