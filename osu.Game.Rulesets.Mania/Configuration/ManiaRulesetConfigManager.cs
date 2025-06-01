// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Configuration.Tracking;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Configuration
{
    public class ManiaRulesetConfigManager : RulesetConfigManager<ManiaRulesetSetting>
    {
        public ManiaRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        private const double current_scroll_speed_precision = 1.0;

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(ManiaRulesetSetting.ColumnWidth, 46, 9, 90, 1.0);
            SetDefault(ManiaRulesetSetting.SpecialFactor, 1, 0.1, 4, 0.1);
            // SetDefault(ManiaRulesetSetting.HitMode, MUGHitMode.EZ2AC);
            SetDefault(ManiaRulesetSetting.ScrollSpeed, 200, 1.0, 401.0, current_scroll_speed_precision);
            SetDefault(ManiaRulesetSetting.ScrollBaseSpeed, 500, 100, 1000, 1.0);
            SetDefault(ManiaRulesetSetting.ScrollTimePerSpeed, 5, 1.0, 40, 1.0);
            SetDefault(ManiaRulesetSetting.ScrollStyle, EzManiaScrollingStyle.ScrollTimeStyleFixed);

            SetDefault(ManiaRulesetSetting.ScrollPerKeyMode, false);
            SetDefault(ManiaRulesetSetting.PerspectiveAngle, 90.0f, 30.0f, 90.0f);
            SetDefault(ManiaRulesetSetting.ScrollDirection, ManiaScrollingDirection.Down);
            SetDefault(ManiaRulesetSetting.TimingBasedNoteColouring, false);
            SetDefault(ManiaRulesetSetting.MobileLayout, ManiaMobileLayout.Portrait);
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings
        {
            new TrackedSetting<double>(ManiaRulesetSetting.ScrollSpeed,
                speed => new SettingDescription(
                    rawValue: speed,
                    name: RulesetSettingsStrings.ScrollSpeed,
                    value: RulesetSettingsStrings.ScrollSpeedTooltip(
                        (int)DrawableManiaRuleset.ComputeScrollTime(speed, Get<double>(ManiaRulesetSetting.ScrollBaseSpeed), Get<double>(ManiaRulesetSetting.ScrollTimePerSpeed)),
                        speed
                    )
                )
            ),
        };
    }

    public enum ManiaRulesetSetting
    {
        [Obsolete("Use ScrollSpeed instead.")] // Can be removed 2023-11-30
        ScrollTime,
        ScrollBaseSpeed,
        ScrollTimePerSpeed,
        ScrollStyle,
        // HitMode,

        PerspectiveAngle,
        ColumnWidth,
        SpecialFactor,
        ScrollPerKeyMode,
        ScrollSpeed,
        ScrollDirection,
        TimingBasedNoteColouring,
        MobileLayout,
    }

    public enum EzManiaScrollingStyle
    {
        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionUp))]
        [Description("40速 通配速度风格(不可用)")]
        ScrollSpeedStyle,

        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionDown))]
        [Description("ms值 恒定速度")]
        ScrollTimeStyle,

        [Description("ms值 恒定时间")]
        ScrollTimeStyleFixed,
    }
}
