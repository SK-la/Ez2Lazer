// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.LAsEZMania;
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
        private void load(Ez2ConfigManager ezConfig)
        {
            var config = (ManiaRulesetConfigManager)Config;

            Children = new Drawable[]
            {
                new SettingsEnumDropdown<EzMUGHitMode>
                {
                    ClassicDefault = EzMUGHitMode.EZ2AC,
                    LabelText = EzLocalizationManager.HitMode,
                    TooltipText = EzLocalizationManager.HitModeTooltip,
                    Current = ezConfig.GetBindable<EzMUGHitMode>(Ez2Setting.HitMode),
                    Keywords = new[] { "mania" }
                },
                new SettingsEnumDropdown<EnumHealthMode>
                {
                    ClassicDefault = EnumHealthMode.Lazer,
                    Current = ezConfig.GetBindable<EnumHealthMode>(Ez2Setting.CustomHealthMode),
                    LabelText = EzLocalizationManager.HealthMode,
                    TooltipText = EzLocalizationManager.HealthModeTooltip,
                    Keywords = new[] { "mania" }
                },
                new SettingsCheckbox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.CustomPoorHitResultBool),
                    LabelText = EzLocalizationManager.PoorHitResult,
                    TooltipText = EzLocalizationManager.PoorHitResultTooltip,
                    Keywords = new[] { "mania" }
                },
                new SettingsCheckbox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.ManiaBarLinesBool),
                    LabelText = EzLocalizationManager.ManiaBarLinesBool,
                    TooltipText = EzLocalizationManager.ManiaBarLinesBoolTooltip,
                    Keywords = new[] { "mania" }
                },

                new SettingsItemV2(new FormEnumDropdown<ManiaScrollingDirection>
                {
                    Caption = RulesetSettingsStrings.ScrollingDirection,
                    Current = config.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection)
                }),

                new SettingsEnumDropdown<EzManiaScrollingStyle>
                {
                    LabelText = "Scrolling style",
                    Current = config.GetBindable<EzManiaScrollingStyle>(ManiaRulesetSetting.ScrollStyle)
                },

                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = RulesetSettingsStrings.ScrollSpeed,
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed),
                    KeyboardStep = 1,
                    LabelFormat = v => RulesetSettingsStrings.ScrollSpeedTooltip((int)DrawableManiaRuleset.ComputeScrollTime(v), v),
                }),

                new SettingsSlider<double, ManiaScrollBaseSlider>
                {
                    LabelText = "Scroll Base MS (when 200 Speed)",
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollBaseSpeed),
                    KeyboardStep = 1,
                    Keywords = new[] { "base" }
                },
                new SettingsSlider<double, ManiaScrollMsPerSpeedSlider>
                {
                    LabelText = "MS / Speed",
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollTimePerSpeed),
                    KeyboardStep = 1,
                    Keywords = new[] { "mps" }
                },

                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.TimingBasedColouring,
                    Current = config.GetBindable<bool>(ManiaRulesetSetting.TimingBasedNoteColouring),
                })
                {
                    Keywords = new[] { "color" },
                },
            };

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = RulesetSettingsStrings.TouchOverlay,
                Current = config.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay)
            }));

            if (RuntimeInfo.IsMobile)
            {
                Add(new SettingsItemV2(new FormEnumDropdown<ManiaMobileLayout>
                {
                    Caption = RulesetSettingsStrings.MobileLayout,
                    Current = config.GetBindable<ManiaMobileLayout>(ManiaRulesetSetting.MobileLayout),
#pragma warning disable CS0618 // Type or member is obsolete
                    Items = Enum.GetValues<ManiaMobileLayout>().Where(l => l != ManiaMobileLayout.LandscapeWithOverlay),
#pragma warning restore CS0618 // Type or member is obsolete
                }));
            }
        }

        private partial class ManiaScrollSlider : RoundedSliderBar<double>
        {
            // 自定义提示
            private ManiaRulesetConfigManager config = null!;

            [BackgroundDependencyLoader]
            private void load(ManiaRulesetConfigManager config)
            {
                this.config = config;
            }

            public override LocalisableString TooltipText
            {
                get
                {
                    double baseSpeed = config.Get<double>(ManiaRulesetSetting.ScrollBaseSpeed);
                    double timePerSpeed = config.Get<double>(ManiaRulesetSetting.ScrollTimePerSpeed);
                    int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(Current.Value, baseSpeed, timePerSpeed);
                    LocalisableString speedInfo = RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, Current.Value);
                    return $"{baseSpeed}base - ( {Current.Value} - 200) * {timePerSpeed}mps\n = {speedInfo}";
                }
            }
        }

        private partial class ManiaScrollBaseSlider : RoundedSliderBar<double>
        {
            private ManiaRulesetConfigManager config = null!;

            [BackgroundDependencyLoader]
            private void load(ManiaRulesetConfigManager config)
            {
                this.config = config;
            }

            public override LocalisableString TooltipText
            {
                get
                {
                    double speed = config.Get<double>(ManiaRulesetSetting.ScrollSpeed);
                    double timePerSpeed = config.Get<double>(ManiaRulesetSetting.ScrollTimePerSpeed);
                    int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(speed, Current.Value, timePerSpeed);
                    LocalisableString speedInfo = RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, speed);
                    return $"{Current.Value}base - ( {speed} - 200) * {timePerSpeed}mps\n = {speedInfo}";
                }
            }
        }

        private partial class ManiaScrollMsPerSpeedSlider : RoundedSliderBar<double>
        {
            private ManiaRulesetConfigManager config = null!;

            [BackgroundDependencyLoader]
            private void load(ManiaRulesetConfigManager config)
            {
                this.config = config;
            }

            public override LocalisableString TooltipText
            {
                get
                {
                    double speed = config.Get<double>(ManiaRulesetSetting.ScrollSpeed);
                    double baseSpeed = config.Get<double>(ManiaRulesetSetting.ScrollBaseSpeed);
                    int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(speed, baseSpeed, Current.Value);
                    LocalisableString speedInfo = RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, speed);
                    return $"{baseSpeed}base - ( {speed} - 200) * {Current.Value}mps\n = {speedInfo}";
                }
            }
        }
    }
}
