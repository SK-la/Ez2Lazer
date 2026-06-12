// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Pixiv follow-stream background settings. Shown below the menu background source dropdown when Pixiv is selected.
    /// </summary>
    public partial class EzPixivBackgroundSettings : FillFlowContainer
    {
        public EzPixivBackgroundSettings(
            Ez2ConfigManager ezConfig,
            PixivBackgroundCoordinator coordinator,
            INotificationOverlay? notifications,
            IBindable<BackgroundSource> backgroundSource)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);

            var tokenInput = new Bindable<string>(coordinator.Auth.LoadRefreshToken() ?? string.Empty);

            var saveButton = createActionButton(EzSettingsStrings.PIXIV_SAVE_TOKEN, EzSettingsStrings.PIXIV_SAVE_TOKEN_TOOLTIP, new[] { "pixiv", "save", "token" },
                new MarginPadding { Right = 2.5f });
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

            var verifyButton = createActionButton(EzSettingsStrings.PIXIV_VERIFY_TOKEN, EzSettingsStrings.PIXIV_VERIFY_TOKEN_TOOLTIP, new[] { "pixiv", "verify", "token", "login" },
                new MarginPadding { Horizontal = 2.5f });
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

            var clearButton = createActionButton(EzSettingsStrings.PIXIV_CLEAR_TOKEN, EzSettingsStrings.PIXIV_CLEAR_TOKEN_TOOLTIP, new[] { "pixiv", "clear", "logout", "token" },
                new MarginPadding { Left = 2.5f });
            clearButton.Action = () =>
            {
                coordinator.Auth.ClearRefreshToken();
                tokenInput.Value = string.Empty;
                post(notifications, EzSettingsStrings.PIXIV_TOKEN_CLEARED);
            };

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormTextBox
                {
                    Caption = EzSettingsStrings.PIXIV_REFRESH_TOKEN,
                    HintText = EzSettingsStrings.PIXIV_REFRESH_TOKEN_TOOLTIP,
                    Current = tokenInput,
                })
                {
                    Keywords = new[] { "pixiv", "background", "refresh", "token", "oauth", "auth" }
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        saveButton,
                        verifyButton,
                        clearButton,
                    }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_ENABLED,
                    HintText = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.PixivAutoDownloadEnabled),
                })
                {
                    Keywords = new[] { "pixiv", "background", "download", "auto", "cache", "bg_pixiv" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.PIXIV_ALLOW_R18,
                    HintText = EzSettingsStrings.PIXIV_ALLOW_R18_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.PixivAllowR18),
                })
                {
                    Keywords = new[] { "pixiv", "r-18", "r18", "nsfw", "filter" }
                },
                createListSetting(ezConfig, Ez2Setting.PixivAccountWhitelist, EzSettingsStrings.PIXIV_ACCOUNT_WHITELIST, EzSettingsStrings.PIXIV_ACCOUNT_WHITELIST_TOOLTIP,
                    new[] { "pixiv", "whitelist", "account", "artist", "filter" }),
                createListSetting(ezConfig, Ez2Setting.PixivAccountBlacklist, EzSettingsStrings.PIXIV_ACCOUNT_BLACKLIST, EzSettingsStrings.PIXIV_ACCOUNT_BLACKLIST_TOOLTIP,
                    new[] { "pixiv", "blacklist", "account", "artist", "filter" }),
                createListSetting(ezConfig, Ez2Setting.PixivTagInclude, EzSettingsStrings.PIXIV_TAG_INCLUDE, EzSettingsStrings.PIXIV_TAG_INCLUDE_TOOLTIP,
                    new[] { "pixiv", "tag", "include", "filter" }),
                createListSetting(ezConfig, Ez2Setting.PixivTagExclude, EzSettingsStrings.PIXIV_TAG_EXCLUDE, EzSettingsStrings.PIXIV_TAG_EXCLUDE_TOOLTIP,
                    new[] { "pixiv", "tag", "exclude", "filter" }),
                createListSetting(ezConfig, Ez2Setting.PixivSkipSaveAccountPrefixes, EzSettingsStrings.PIXIV_SKIP_SAVE_ACCOUNT_PREFIXES,
                    EzSettingsStrings.PIXIV_SKIP_SAVE_ACCOUNT_PREFIXES_TOOLTIP, new[] { "pixiv", "save", "prefix", "account", "naming" }),
                createListSetting(ezConfig, Ez2Setting.PixivSkipSaveTags, EzSettingsStrings.PIXIV_SKIP_SAVE_TAGS, EzSettingsStrings.PIXIV_SKIP_SAVE_TAGS_TOOLTIP,
                    new[] { "pixiv", "save", "tag", "naming" }),
            };

            backgroundSource.BindValueChanged(change =>
            {
                if (change.NewValue == BackgroundSource.PixivFollow)
                    Show();
                else
                    Hide();
            }, true);
        }

        private static SettingsButton createActionButton(LocalisableString text, LocalisableString tooltip, string[] keywords, MarginPadding spacingPadding)
        {
            return new SettingsButton
            {
                Text = text,
                TooltipText = tooltip,
                Keywords = keywords,
                RelativeSizeAxes = Axes.X,
                Width = 1 / 3f,
                Margin = new MarginPadding { Vertical = 0 },
                Padding = spacingPadding,
            };
        }

        private static SettingsItemV2 createListSetting(
            Ez2ConfigManager ezConfig,
            Ez2Setting setting,
            EzLocalizationManager.EzLocalisableString caption,
            EzLocalizationManager.EzLocalisableString hint,
            string[] keywords)
        {
            return new SettingsItemV2(new FormTextBox
            {
                Caption = caption,
                HintText = hint,
                Current = ezConfig.GetBindable<string>(setting),
            })
            {
                Keywords = keywords
            };
        }

        private static void post(INotificationOverlay? notifications, LocalisableString text)
        {
            notifications?.Post(new SimpleNotification { Text = text });
        }
    }
}
