// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class MainMenuSettings : SettingsSubsection
    {
        private static readonly List<string> menu_logo_items = new List<string>
        {
            @"Menu/logo",
            @"Menu/logo2",
        };

        protected override LocalisableString Header => UserInterfaceStrings.MainMenuHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Ez2ConfigManager ezConfig,
                          PixivBackgroundCoordinator pixivBackgroundCoordinator,
                          INotificationOverlay? notifications)
        {
            var backgroundSource = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource);
            var menuLogoPath = ezConfig.GetBindable<string>(Ez2Setting.MenuLogoPath);
            var logoBackdropStyle = ezConfig.GetBindable<EzLogoBackdropStyle>(Ez2Setting.MenuLogoBackdropStyle);

            var logoBackdropColour = new SettingsItemV2(new EzFormColour
            {
                Caption = EzSettingsStrings.LOGO_BACKDROP_COLOUR,
                Current = ezConfig.GetBindable<Colour4>(Ez2Setting.MenuLogoBackdropColour),
            });
            var logoBackdropBlur = new SettingsItemV2(new FormSliderBar<double>
            {
                Caption = EzSettingsStrings.LOGO_BACKDROP_BLUR,
                Current = ezConfig.GetBindable<double>(Ez2Setting.MenuLogoBackdropBlur),
                KeyboardStep = 1,
            });
            var logoBackdropOpacity = new SettingsItemV2(new FormSliderBar<double>
            {
                Caption = EzSettingsStrings.LOGO_BACKDROP_OPACITY,
                HintText = EzSettingsStrings.LOGO_BACKDROP_OPACITY_TOOLTIP,
                Current = ezConfig.GetBindable<double>(Ez2Setting.MenuLogoBackdropOpacity),
                KeyboardStep = 0.01f,
            });

            logoBackdropStyle.BindValueChanged(style =>
            {
                logoBackdropColour.CanBeShown.Value = style.NewValue == EzLogoBackdropStyle.Solid;
                logoBackdropBlur.CanBeShown.Value = style.NewValue == EzLogoBackdropStyle.Acrylic;
                // logoBackdropOpacity.CanBeShown.Value = style.NewValue == EzLogoBackdropStyle.Linked;
            }, true);

            ensureItemAvailable(menuLogoPath, menu_logo_items);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ShowMenuTips,
                    Current = config.GetBindable<bool>(OsuSetting.MenuTips)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.InterfaceVoices,
                    Current = config.GetBindable<bool>(OsuSetting.MenuVoice)
                })
                {
                    Keywords = new[] { "intro", "welcome" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.OsuMusicTheme,
                    Current = config.GetBindable<bool>(OsuSetting.MenuMusic)
                })
                {
                    Keywords = new[] { "intro", "welcome" },
                },
                new SettingsItemV2(new FormEnumDropdown<IntroSequence>
                {
                    Caption = UserInterfaceStrings.IntroSequence,
                    Current = config.GetBindable<IntroSequence>(OsuSetting.IntroSequence),
                }),
                new SettingsItemV2(new FormEnumDropdown<BackgroundSource>
                {
                    Caption = UserInterfaceStrings.BackgroundSource,
                    Current = backgroundSource,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SONG_SELECT_EZ_BACKGROUND,
                    HintText = EzSettingsStrings.SONG_SELECT_EZ_BACKGROUND_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.SongSelectUseEzBackground),
                }),
                new SettingsItemV2(new FormDropdown<string>
                {
                    Caption = "Menu Logo",
                    Current = menuLogoPath,
                    Items = menu_logo_items,
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = EzSettingsStrings.LOGO_CENTRE_TEXT,
                    HintText = EzSettingsStrings.LOGO_CENTRE_TEXT_TOOLTIP,
                    PlaceholderText = EzSettingsStrings.LOGO_CENTRE_TEXT_PLACEHOLDER,
                    Current = ezConfig.GetBindable<string>(Ez2Setting.MenuLogoText),
                }),
                new SettingsItemV2(new FormEnumDropdown<EzLogoVisualisationStyle>
                {
                    Caption = EzSettingsStrings.LOGO_VISUALISATION,
                    HintText = EzSettingsStrings.LOGO_VISUALISATION_TOOLTIP,
                    Current = ezConfig.GetBindable<EzLogoVisualisationStyle>(Ez2Setting.MenuLogoVisualisationStyle),
                }),
                new SettingsItemV2(new FormEnumDropdown<EzLogoBackdropStyle>
                {
                    Caption = EzSettingsStrings.LOGO_BACKDROP,
                    HintText = EzSettingsStrings.LOGO_BACKDROP_TOOLTIP,
                    Current = logoBackdropStyle,
                }),
                logoBackdropColour,
                logoBackdropBlur,
                logoBackdropOpacity,
                new EzPixivBackgroundSettings(ezConfig, pixivBackgroundCoordinator, notifications, backgroundSource),
                new SettingsItemV2(new FormEnumDropdown<SeasonalBackgroundMode>
                {
                    Caption = UserInterfaceStrings.SeasonalBackgrounds,
                    Current = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode),
                })
            };
        }

        private static void ensureItemAvailable(Bindable<string> current, List<string> items)
        {
            if (!string.IsNullOrEmpty(current.Value) && !items.Contains(current.Value))
                items.Insert(0, current.Value);

            if (items.Count == 0)
                items.Add(string.Empty);

            if (string.IsNullOrEmpty(current.Value))
                current.Value = items[0];
        }
    }
}
