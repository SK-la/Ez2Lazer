// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
        protected Bindable<double> BaseSpeedBindable = null!;
        protected Bindable<double> TimePerSpeedBindable = null!;
        protected Bindable<double> SpeedBindable = null!;

        public ManiaSettingsSubsection(ManiaRuleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            var config = (ManiaRulesetConfigManager)Config;

            BaseSpeedBindable = config.GetBindable<double>(ManiaRulesetSetting.ScrollBaseSpeed);
            TimePerSpeedBindable = config.GetBindable<double>(ManiaRulesetSetting.ScrollTimePerSpeed);
            SpeedBindable = config.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<EzMUGHitMode>
                {
                    Caption = EzLocalizationManager.HitMode,
                    HintText = EzLocalizationManager.HitModeTooltip,
                    Current = ezConfig.GetBindable<EzMUGHitMode>(Ez2Setting.HitMode),
                })
                {
                    Keywords = new[] { "mania" }
                },
                new SettingsItemV2(new FormEnumDropdown<EnumHealthMode>
                {
                    Caption = EzLocalizationManager.HealthMode,
                    HintText = EzLocalizationManager.HealthModeTooltip,
                    Current = ezConfig.GetBindable<EnumHealthMode>(Ez2Setting.CustomHealthMode),
                })
                {
                    Keywords = new[] { "mania" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzLocalizationManager.PoorHitResult,
                    HintText = EzLocalizationManager.PoorHitResultTooltip,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.CustomPoorHitResultBool),
                })
                {
                    Keywords = new[] { "mania" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzLocalizationManager.ManiaBarLinesBool,
                    HintText = EzLocalizationManager.ManiaBarLinesBoolTooltip,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.ManiaBarLinesBool),
                })
                {
                    Keywords = new[] { "mania" }
                },

                new SettingsItemV2(new FormEnumDropdown<ManiaScrollingDirection>
                {
                    Caption = RulesetSettingsStrings.ScrollingDirection,
                    Current = config.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection)
                }),

                new SettingsItemV2(new FormEnumDropdown<EzManiaScrollingStyle>
                {
                    Caption = "Scrolling style",
                    Current = config.GetBindable<EzManiaScrollingStyle>(ManiaRulesetSetting.ScrollStyle)
                })
                {
                    Keywords = new[] { "mania" }
                },

                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = RulesetSettingsStrings.ScrollSpeed,
                    Current = SpeedBindable,
                    KeyboardStep = 1,
                    LabelFormat = v =>
                    {
                        int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(v, BaseSpeedBindable.Value, TimePerSpeedBindable.Value);
                        var speedInfo = RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, v);
                        return $"{BaseSpeedBindable.Value}base - ( {v} - 200) * {TimePerSpeedBindable.Value}mps\n = {speedInfo}";
                    },
                }),

                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = "Scroll Base MS (when 200 Speed)",
                    Current = BaseSpeedBindable,
                    KeyboardStep = 1,
                    LabelFormat = v =>
                    {
                        int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(SpeedBindable.Value, v, TimePerSpeedBindable.Value);
                        var speedInfo = RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, SpeedBindable.Value);
                        return $"{v}base - ( {SpeedBindable.Value} - 200) * {TimePerSpeedBindable.Value}mps\n = {speedInfo}";
                    },
                })
                {
                    Keywords = new[] { "mania" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = "MS / Speed",
                    Current = TimePerSpeedBindable,
                    KeyboardStep = 1,
                    LabelFormat = v =>
                    {
                        int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(SpeedBindable.Value, BaseSpeedBindable.Value, v);
                        var speedInfo = RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, SpeedBindable.Value);
                        return $"{BaseSpeedBindable.Value}base - ( {SpeedBindable.Value} - 200) * {v}mps\n = {speedInfo}";
                    },
                })
                {
                    Keywords = new[] { "mania" }
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
    }
}
