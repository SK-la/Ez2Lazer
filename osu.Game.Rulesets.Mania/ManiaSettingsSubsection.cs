// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania
{
    public partial class ManiaSettingsSubsection : RulesetSettingsSubsection
    {
        protected override LocalisableString Header => "osu!mania";

        public ManiaSettingsSubsection(ManiaRuleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (ManiaRulesetConfigManager)Config;

            Children = new Drawable[]
            {
                // new SettingsSlider<double, ManiaColumnWidth>
                // {
                //     LabelText = "Column Width(Not Active)",
                //     Current = config.GetBindable<double>(ManiaRulesetSetting.ColumnWidth),
                //     KeyboardStep = 1,
                // },
                // new SettingsSlider<double, ManiaSpecialFactor>
                // {
                //     LabelText = "Special Column Width Factor(Not Active)",
                //     Current = config.GetBindable<double>(ManiaRulesetSetting.SpecialFactor),
                //     KeyboardStep = 1,
                // },
                new SettingsEnumDropdown<ManiaScrollingDirection>
                {
                    LabelText = RulesetSettingsStrings.ScrollingDirection,
                    Current = config.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection)
                },
                new SettingsEnumDropdown<ManiaScrollingStyle>
                {
                    LabelText = RulesetSettingsStrings.ScrollingStyle,
                    Current = config.GetBindable<ManiaScrollingStyle>(ManiaRulesetSetting.ScrollStyle)
                },
                new SettingsSlider<double, ManiaScrollSlider>
                {
                    LabelText = RulesetSettingsStrings.ScrollSpeed,
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed),
                    KeyboardStep = 1,
                },
                new SettingsSlider<double, ManiaScrollBaseSpeedSlider>
                {
                    LabelText = RulesetSettingsStrings.ScrollBaseSpeed,
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollBaseSpeed),
                    KeyboardStep = 1,
                },
                new SettingsSlider<double, ManiaScrollTimePerSpeedSlider>
                {
                    LabelText = RulesetSettingsStrings.ScrollTimePrecision,
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollTimePerSpeed),
                    KeyboardStep = 1,
                },
                new SettingsCheckbox
                {
                    Keywords = new[] { "color" },
                    LabelText = RulesetSettingsStrings.TimingBasedColouring,
                    Current = config.GetBindable<bool>(ManiaRulesetSetting.TimingBasedNoteColouring),
                },
            };

            if (RuntimeInfo.IsMobile)
            {
                Add(new SettingsEnumDropdown<ManiaMobileLayout>
                {
                    LabelText = RulesetSettingsStrings.MobileLayout,
                    Current = config.GetBindable<ManiaMobileLayout>(ManiaRulesetSetting.MobileLayout),
                });
            }
        }

        private partial class ManiaScrollSlider : RoundedSliderBar<double>
        {
            private ManiaRulesetConfigManager config = null!;

            [BackgroundDependencyLoader]
            private void load(ManiaRulesetConfigManager config)
            {
                this.config = config;
            }

            public override LocalisableString TooltipText => RulesetSettingsStrings.ScrollSpeedTooltip(
                (int)DrawableManiaRuleset.ComputeScrollTime(Current.Value, config.Get<double>(ManiaRulesetSetting.ScrollBaseSpeed), config.Get<double>(ManiaRulesetSetting.ScrollTimePerSpeed)),
                Current.Value
            );
        }

        private partial class ManiaScrollBaseSpeedSlider : RoundedSliderBar<double>
        {
            public override LocalisableString TooltipText => RulesetSettingsStrings.ScrollSpeedTooltip(
                (int)Current.Value, 200);
        }

        private partial class ManiaScrollTimePerSpeedSlider : RoundedSliderBar<double>
        {
        }

        // private partial class ManiaColumnWidth : RoundedSliderBar<double>
        // {
        // }
        //
        // private partial class ManiaSpecialFactor : RoundedSliderBar<double>
        // {
        // }
    }
}
