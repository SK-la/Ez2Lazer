// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration.Tracking;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Configuration
{
    public class ManiaRulesetConfigManager : RulesetConfigManager<ManiaRulesetSetting>
    {
        public ManiaRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
            Migrate();
        }

        private const double current_scroll_speed_precision = 1.0;

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            // SetDefault(ManiaRulesetSetting.HitMode, EzMUGHitMode.EZ2AC);
            SetDefault(ManiaRulesetSetting.ScrollBaseSpeed, 500, 100, 1000, 1.0);
            SetDefault(ManiaRulesetSetting.ScrollTimePerSpeed, 5, 1.0, 40, 1.0);
            SetDefault(ManiaRulesetSetting.ScrollStyle, EzManiaScrollingStyle.ScrollTimeStyleFixed);

            SetDefault(ManiaRulesetSetting.ScrollPerKeyMode, false);
            SetDefault(ManiaRulesetSetting.PerspectiveAngle, 90.0f, 30.0f, 90.0f);
            SetDefault(ManiaRulesetSetting.ScrollSpeed, 200, 1.0, 401.0, current_scroll_speed_precision);
            SetDefault(ManiaRulesetSetting.ScrollDirection, ManiaScrollingDirection.Down);
            SetDefault(ManiaRulesetSetting.TimingBasedNoteColouring, false);
            SetDefault(ManiaRulesetSetting.MobileLayout, ManiaMobileLayout.Portrait);
            SetDefault(ManiaRulesetSetting.TouchOverlay, false);
        }

        public void Migrate()
        {
            var mobileLayout = GetBindable<ManiaMobileLayout>(ManiaRulesetSetting.MobileLayout);

#pragma warning disable CS0618 // Type or member is obsolete
            if (mobileLayout.Value == ManiaMobileLayout.LandscapeWithOverlay)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                mobileLayout.Value = ManiaMobileLayout.Landscape;
                SetValue(ManiaRulesetSetting.TouchOverlay, true);
            }
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

    // TODO: 未来应考虑完全迁移到Ez2Setting中
    public enum ManiaRulesetSetting
    {
        ScrollStyle,
        ScrollTime,
        ScrollBaseSpeed,
        ScrollTimePerSpeed,

        // HitMode,
        //暂时无用
        PerspectiveAngle,
        ScrollPerKeyMode,

        //官方设置
        ScrollSpeed,
        ScrollDirection,
        TimingBasedNoteColouring,
        MobileLayout,
        TouchOverlay,
    }
}
