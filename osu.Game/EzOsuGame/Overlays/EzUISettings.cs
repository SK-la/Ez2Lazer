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
    public partial class EzUISettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_UI_SETTINGS_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.EZ_ANALYSIS_REC_ENABLED,
                    HintText = EzSettingsStrings.EZ_ANALYSIS_REC_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled),
                })
                {
                    Keywords = new[] { "analysis", "ez", "song select", "kps", "kpc" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.EZ_ANALYSIS_SQLITE_ENABLED,
                    HintText = EzSettingsStrings.EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled),
                })
                {
                    Keywords = new[] { "analysis", "sqlite", "cache", "warmup", "persistent" }
                },
                new EzDataRebuildSettingsSection(),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER,
                    HintText = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HideMainMenuOnlineBanner),
                })
                {
                    Keywords = new[] { "main menu", "banner", "news", "advertisement", "ui" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzNotificationBehaviour>
                {
                    Caption = EzSettingsStrings.NOTIFICATION_BEHAVIOUR,
                    HintText = EzSettingsStrings.NOTIFICATION_BEHAVIOUR_TOOLTIP,
                    Current = ezConfig.GetBindable<EzNotificationBehaviour>(Ez2Setting.NotificationBehaviour),
                })
                {
                    Keywords = new[] { "notification", "toast", "alert", "sound", "ui", "gameplay" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzScreenshotAction>
                {
                    Caption = EzSettingsStrings.SCREENSHOT_ACTION,
                    Current = ezConfig.GetBindable<EzScreenshotAction>(Ez2Setting.ScreenshotAction),
                })
                {
                    Keywords = new[] { "screenshot", "clipboard", "capture", "image", "ui" }
                },
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
                    Caption = EzSettingsStrings.HIT_OBJECT_LIFETIME_USES_OWN_TIME,
                    HintText = EzSettingsStrings.HIT_OBJECT_LIFETIME_USES_OWN_TIME_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HitObjectLifetimeUsesOwnTime),
                })
                {
                    Keywords = new[] { "ez", "timing", "lifetime", "hitobject" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SKIP_EMPTY_EDGE_COLUMNS,
                    HintText = EzSettingsStrings.SKIP_EMPTY_EDGE_COLUMNS_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns),
                })
                {
                    Keywords = new[] { "mania", "empty", "column" }
                },
            });
        }
    }
}
