// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
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
                    Caption = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER,
                    HintText = EzSettingsStrings.HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.HideMainMenuOnlineBanner),
                })
                {
                    Keywords = new[] { "main menu", "banner", "news", "advertisement", "ui" }
                }
            });
        }
    }
}
