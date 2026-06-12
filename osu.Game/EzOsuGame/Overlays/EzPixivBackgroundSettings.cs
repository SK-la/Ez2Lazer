// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public static partial class EzPixivBackgroundSettings
    {
        public static void AddTo(SettingsSubsection subsection, Ez2ConfigManager ezConfig, PixivBackgroundCoordinator coordinator, INotificationOverlay? notifications)
        {
            var tokenInput = new Bindable<string>(coordinator.Auth.LoadRefreshToken() ?? string.Empty);

            var tokenTextBox = new FormTextBox
            {
                Caption = EzSettingsStrings.PIXIV_REFRESH_TOKEN,
                HintText = EzSettingsStrings.PIXIV_REFRESH_TOKEN_TOOLTIP,
                Current = tokenInput,
            };

            subsection.Add(new SettingsItemV2(tokenTextBox)
            {
                Keywords = new[] { "pixiv", "background", "refresh", "token", "oauth", "auth" }
            });

            var saveButton = new SettingsButton
            {
                Text = EzSettingsStrings.PIXIV_SAVE_TOKEN,
                TooltipText = EzSettingsStrings.PIXIV_SAVE_TOKEN_TOOLTIP,
                Keywords = new[] { "pixiv", "save", "token" },
            };
            saveButton.Action = () =>
            {
                string token = tokenInput.Value?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(token))
                {
                    post(notifications, EzSettingsStrings.PIXIV_TOKEN_EMPTY);
                    return;
                }

                coordinator.Auth.SaveRefreshToken(token);
                post(notifications, EzSettingsStrings.PIXIV_TOKEN_SAVED);
            };

            var verifyButton = new SettingsButton
            {
                Text = EzSettingsStrings.PIXIV_VERIFY_TOKEN,
                TooltipText = EzSettingsStrings.PIXIV_VERIFY_TOKEN_TOOLTIP,
                Keywords = new[] { "pixiv", "verify", "token", "login" },
            };
            verifyButton.Action = () =>
            {
                string token = tokenInput.Value?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(token))
                    coordinator.Auth.SaveRefreshToken(token);

                if (!coordinator.Auth.TryRefreshAccessToken(out _, out string? error))
                {
                    post(notifications, error ?? EzSettingsStrings.PIXIV_VERIFY_FAILED);
                    return;
                }

                if (coordinator.Api.TryGetUserAccount(out string? account, out error))
                    post(notifications, EzSettingsStrings.PIXIV_VERIFY_SUCCESS.Format(account ?? "?"));
                else
                    post(notifications, error ?? EzSettingsStrings.PIXIV_VERIFY_FAILED);
            };

            var clearButton = new SettingsButton
            {
                Text = EzSettingsStrings.PIXIV_CLEAR_TOKEN,
                TooltipText = EzSettingsStrings.PIXIV_CLEAR_TOKEN_TOOLTIP,
                Keywords = new[] { "pixiv", "clear", "logout", "token" },
            };
            clearButton.Action = () =>
            {
                coordinator.Auth.ClearRefreshToken();
                tokenInput.Value = string.Empty;
                post(notifications, EzSettingsStrings.PIXIV_TOKEN_CLEARED);
            };

            subsection.Add(saveButton);
            subsection.Add(verifyButton);
            subsection.Add(clearButton);

            subsection.Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_ENABLED,
                HintText = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_ENABLED_TOOLTIP,
                Current = ezConfig.GetBindable<bool>(Ez2Setting.PixivAutoDownloadEnabled),
            })
            {
                Keywords = new[] { "pixiv", "background", "download", "auto", "cache", "bg_pixiv" }
            });

            subsection.Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = EzSettingsStrings.PIXIV_ALLOW_R18,
                HintText = EzSettingsStrings.PIXIV_ALLOW_R18_TOOLTIP,
                Current = ezConfig.GetBindable<bool>(Ez2Setting.PixivAllowR18),
            })
            {
                Keywords = new[] { "pixiv", "r-18", "r18", "nsfw", "filter" }
            });

            addListSetting(subsection, ezConfig, Ez2Setting.PixivAccountWhitelist, EzSettingsStrings.PIXIV_ACCOUNT_WHITELIST, EzSettingsStrings.PIXIV_ACCOUNT_WHITELIST_TOOLTIP,
                new[] { "pixiv", "whitelist", "account", "artist", "filter" });

            addListSetting(subsection, ezConfig, Ez2Setting.PixivAccountBlacklist, EzSettingsStrings.PIXIV_ACCOUNT_BLACKLIST, EzSettingsStrings.PIXIV_ACCOUNT_BLACKLIST_TOOLTIP,
                new[] { "pixiv", "blacklist", "account", "artist", "filter" });

            addListSetting(subsection, ezConfig, Ez2Setting.PixivTagInclude, EzSettingsStrings.PIXIV_TAG_INCLUDE, EzSettingsStrings.PIXIV_TAG_INCLUDE_TOOLTIP,
                new[] { "pixiv", "tag", "include", "filter" });

            addListSetting(subsection, ezConfig, Ez2Setting.PixivTagExclude, EzSettingsStrings.PIXIV_TAG_EXCLUDE, EzSettingsStrings.PIXIV_TAG_EXCLUDE_TOOLTIP,
                new[] { "pixiv", "tag", "exclude", "filter" });

            addListSetting(subsection, ezConfig, Ez2Setting.PixivSkipSaveAccountPrefixes, EzSettingsStrings.PIXIV_SKIP_SAVE_ACCOUNT_PREFIXES,
                EzSettingsStrings.PIXIV_SKIP_SAVE_ACCOUNT_PREFIXES_TOOLTIP, new[] { "pixiv", "save", "prefix", "account", "naming" });

            addListSetting(subsection, ezConfig, Ez2Setting.PixivSkipSaveTags, EzSettingsStrings.PIXIV_SKIP_SAVE_TAGS, EzSettingsStrings.PIXIV_SKIP_SAVE_TAGS_TOOLTIP,
                new[] { "pixiv", "save", "tag", "naming" });
        }

        private static void addListSetting(
            SettingsSubsection subsection,
            Ez2ConfigManager ezConfig,
            Ez2Setting setting,
            EzLocalizationManager.EzLocalisableString caption,
            EzLocalizationManager.EzLocalisableString hint,
            string[] keywords)
        {
            subsection.Add(new SettingsItemV2(new FormTextBox
            {
                Caption = caption,
                HintText = hint,
                Current = ezConfig.GetBindable<string>(setting),
            })
            {
                Keywords = keywords
            });
        }

        private static void post(INotificationOverlay? notifications, LocalisableString text)
        {
            notifications?.Post(new SimpleNotification { Text = text });
        }
    }
}
