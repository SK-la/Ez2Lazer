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

        [Resolved(CanBeNull = true)]
        private EzFontSettingsOverlay? fontOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.ACRYLIC_UI_ENABLED,
                    HintText = EzSettingsStrings.ACRYLIC_UI_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.AcrylicUiEnabled),
                })
                {
                    Keywords = new[] { "acrylic", "glass", "blur", "song select", "ui", "毛玻璃" }
                },
                new SettingsButtonV2
                {
                    Text = EzSettingsStrings.UI_FONT_MODIFY,
                    TooltipText = EzSettingsStrings.UI_FONT_MODIFY_TOOLTIP,
                    Action = () => fontOverlay?.ShowFromSettings(),
                    Keywords = new[] { "font", "typeface", "ui", "system", "字体" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.ACRYLIC_UI_BLUR_STRENGTH,
                    HintText = EzSettingsStrings.ACRYLIC_UI_BLUR_STRENGTH_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AcrylicUiBlurStrength),
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "acrylic", "blur", "strength", "song select", "ui", "虚化" }
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
                    Caption = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER,
                    HintText = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HideMainMenuOnlineBanner),
                })
                {
                    Keywords = new[] { "main menu", "banner", "news", "advertisement", "ui" }
                },
            });
        }
    }
}
