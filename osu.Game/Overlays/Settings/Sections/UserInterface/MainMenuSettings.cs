// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class MainMenuSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.MainMenuHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Ez2ConfigManager ezConfig,
                          PixivBackgroundCoordinator pixivBackgroundCoordinator,
                          INotificationOverlay? notifications)
        {
            var backgroundSource = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource);

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
                new EzPixivBackgroundSettings(ezConfig, pixivBackgroundCoordinator, notifications, backgroundSource),
                new SettingsItemV2(new FormEnumDropdown<SeasonalBackgroundMode>
                {
                    Caption = UserInterfaceStrings.SeasonalBackgrounds,
                    Current = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode),
                })
            };
        }
    }
}
