// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.BMS.Configuration
{
    public class BMSRulesetConfigManager : RulesetConfigManager<BMSRulesetSetting>
    {
        public BMSRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(BMSRulesetSetting.BmsRootPath, string.Empty);
            SetDefault(BMSRulesetSetting.ScrollSpeed, 25.0, 1.0, 40.0, 1.0);
            SetDefault(BMSRulesetSetting.AutoPreloadKeysounds, true);
            SetDefault(BMSRulesetSetting.KeysoundVolume, 1.0, 0.0, 1.0, 0.01);
        }
    }

    public enum BMSRulesetSetting
    {
        BmsRootPath,
        ScrollSpeed,
        AutoPreloadKeysounds,
        KeysoundVolume,
    }
}
