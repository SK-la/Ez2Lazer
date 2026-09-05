// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameplaySettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_GAMEPLAY_SETTINGS_HEADER;

        [Resolved(CanBeNull = true)]
        private EzFontSettingsOverlay? fontOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.STORYBOARD_VIDEO_AUTO_SIZE,
                    HintText = EzSettingsStrings.STORYBOARD_VIDEO_AUTO_SIZE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.StoryboardAutoVideoSize),
                })
                {
                    Keywords = new[] { "storyboard", "video", "size", "auto", "autosize", "ui" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SKIP_WITH_GAMEPLAY_KEYS,
                    HintText = EzSettingsStrings.SKIP_WITH_GAMEPLAY_KEYS_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.SkipWithGameplayKeys),
                })
                {
                    Keywords = new[] { "skip", "gameplay", "key", "mania", "std" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.HIT_OBJECT_LIFETIME_USES_OWN_TIME,
                    HintText = EzSettingsStrings.HIT_OBJECT_LIFETIME_USES_OWN_TIME_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HitObjectLifetimeUsesOwnTime),
                })
                {
                    Keywords = new[] { "ez", "timing", "lifetime", "hitobject" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.TURBO_MODE,
                    HintText = EzSettingsStrings.TURBO_MODE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.TurboMode),
                })
                {
                    Keywords = new[] { "turbo", "performance", "fps", "frame rate", "low spec", "极速", "帧数" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.FLOW_MODE,
                    HintText = EzSettingsStrings.FLOW_MODE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.FlowMode),
                })
                {
                    Keywords = new[] { "flow", "zen", "心流", "results", "ranking", "song select" }
                },
            });
        }
    }
}
