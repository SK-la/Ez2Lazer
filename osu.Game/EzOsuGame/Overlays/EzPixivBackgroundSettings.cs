// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
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
        private readonly PixivBackgroundCoordinator coordinator;
        private readonly INotificationOverlay? notifications;
        private readonly Bindable<SettingsNote.Data?> statusNote = new Bindable<SettingsNote.Data?>();
        private readonly Bindable<string> tokenInput = new Bindable<string>(string.Empty);
        private readonly BindableBool showAdvancedSettings = new BindableBool();

        private readonly SettingsButton checkButton;
        private int loginRequestInFlight;

        public EzPixivBackgroundSettings(
            Ez2ConfigManager ezConfig,
            PixivBackgroundCoordinator coordinator,
            INotificationOverlay? notifications,
            IBindable<BackgroundSource> backgroundSource)
        {
            this.coordinator = coordinator;
            this.notifications = notifications;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);

            var advancedSection = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                Children = new Drawable[]
                {
                    new SettingsItemV2(new PixivTokenFormTextBox
                    {
                        Caption = EzSettingsStrings.PIXIV_REFRESH_TOKEN,
                        HintText = EzSettingsStrings.PIXIV_MANUAL_TOKEN_TOOLTIP,
                        Current = tokenInput,
                    })
                    {
                        Keywords = new[] { "pixiv", "background", "refresh", "token", "auth", "manual" }
                    },
                    new SettingsButtonV2
                    {
                        Text = EzSettingsStrings.PIXIV_SAVE_TOKEN,
                        TooltipText = EzSettingsStrings.PIXIV_SAVE_TOKEN_TOOLTIP,
                        Keywords = new[] { "pixiv", "save", "token" },
                        Action = () =>
                        {
                            string token = tokenInput.Value?.Trim() ?? string.Empty;

                            if (string.IsNullOrWhiteSpace(token))
                            {
                                post(notifications, EzSettingsStrings.PIXIV_TOKEN_EMPTY);
                                return;
                            }

                            coordinator.Auth.SaveRefreshToken(token);
                            tokenInput.Value = string.Empty;
                            refreshLocalStatus();
                            post(notifications, EzSettingsStrings.PIXIV_TOKEN_SAVED);
                        },
                    },
                    new SettingsItemV2(new FormTextBox
                    {
                        Caption = EzSettingsStrings.PIXIV_API_PROXY_BASE_URL,
                        HintText = EzSettingsStrings.PIXIV_API_PROXY_BASE_URL_TOOLTIP,
                        Current = ezConfig.GetBindable<string>(Ez2Setting.PixivApiProxyBaseUrl),
                    })
                    {
                        Keywords = new[] { "pixiv", "background", "proxy", "api", "reverse", "反代" }
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
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = EzSettingsStrings.PIXIV_LANDSCAPE_ONLY,
                        HintText = EzSettingsStrings.PIXIV_LANDSCAPE_ONLY_TOOLTIP,
                        Current = ezConfig.GetBindable<bool>(Ez2Setting.PixivLandscapeOnly),
                    })
                    {
                        Keywords = new[] { "pixiv", "landscape", "horizontal", "横图", "filter", "aspect" }
                    },
                    createListSetting(ezConfig, Ez2Setting.PixivAccountWhitelist, EzSettingsStrings.PIXIV_ACCOUNT_WHITELIST, EzSettingsStrings.PIXIV_ACCOUNT_WHITELIST_TOOLTIP,
                        new[] { "pixiv", "whitelist", "account", "artist", "filter" }),
                    createListSetting(ezConfig, Ez2Setting.PixivAccountBlacklist, EzSettingsStrings.PIXIV_ACCOUNT_BLACKLIST, EzSettingsStrings.PIXIV_ACCOUNT_BLACKLIST_TOOLTIP,
                        new[] { "pixiv", "blacklist", "account", "artist", "filter" }),
                    createListSetting(ezConfig, Ez2Setting.PixivTagInclude, EzSettingsStrings.PIXIV_TAG_INCLUDE, EzSettingsStrings.PIXIV_TAG_INCLUDE_TOOLTIP,
                        new[] { "pixiv", "tag", "include", "filter" }),
                    createListSetting(ezConfig, Ez2Setting.PixivTagExclude, EzSettingsStrings.PIXIV_TAG_EXCLUDE, EzSettingsStrings.PIXIV_TAG_EXCLUDE_TOOLTIP,
                        new[] { "pixiv", "tag", "exclude", "filter" }),
                },
            };

            showAdvancedSettings.BindValueChanged(change =>
            {
                if (change.NewValue)
                    advancedSection.Show();
                else
                {
                    advancedSection.Hide();
                    tokenInput.Value = string.Empty;
                }
            }, true);

            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = { BindTarget = statusNote },
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new EzPixivAuthToolHintNote(),
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        checkButton = new SettingsButton
                        {
                            Text = EzSettingsStrings.PIXIV_CHECK_LOGIN,
                            TooltipText = EzSettingsStrings.PIXIV_CHECK_LOGIN_TOOLTIP,
                            Keywords = new[] { "pixiv", "login", "verify", "auth" },
                            Width = 1 / 3f,
                            Margin = new MarginPadding { Vertical = 0 },
                            Padding = new MarginPadding { Right = 2.5f },
                            Action = checkLogin,
                        },
                        new SettingsButton
                        {
                            Text = EzSettingsStrings.PIXIV_CLEAR_TOKEN,
                            TooltipText = EzSettingsStrings.PIXIV_CLEAR_TOKEN_TOOLTIP,
                            Keywords = new[] { "pixiv", "clear", "logout", "token" },
                            Width = 1 / 3f,
                            Margin = new MarginPadding { Vertical = 0 },
                            Padding = new MarginPadding { Horizontal = 2.5f },
                            Action = () =>
                            {
                                coordinator.Auth.ClearRefreshToken();
                                tokenInput.Value = string.Empty;
                                refreshLocalStatus();
                                post(notifications, EzSettingsStrings.PIXIV_TOKEN_CLEARED);
                            },
                        },
                        new SettingsButton
                        {
                            Text = EzSettingsStrings.PIXIV_CUSTOM_TOOL_HINT,
                            TooltipText = EzSettingsStrings.PIXIV_CUSTOM_TOOL_TOOLTIP,
                            Keywords = new[] { "pixiv", "custom", "advanced", "filter", "proxy", "token" },
                            Width = 1 / 3f,
                            Margin = new MarginPadding { Vertical = 0 },
                            Padding = new MarginPadding { Left = 2.5f },
                            Action = () => showAdvancedSettings.Value = !showAdvancedSettings.Value,
                        },
                    },
                },
                advancedSection,
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_ENABLED,
                    HintText = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.PixivAutoDownloadEnabled),
                })
                {
                    Keywords = new[] { "pixiv", "background", "download", "auto", "cache", "bg_pixiv" }
                },
            };

            backgroundSource.BindValueChanged(change =>
            {
                if (change.NewValue == BackgroundSource.PixivFollow)
                {
                    Show();
                    refreshLocalStatus();
                }
                else
                {
                    Hide();
                }
            }, true);
        }

        private void checkLogin()
        {
            if (Interlocked.CompareExchange(ref loginRequestInFlight, 1, 0) != 0)
                return;

            string token = tokenInput.Value?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(token))
            {
                coordinator.Auth.SaveRefreshToken(token);
                tokenInput.Value = string.Empty;
            }

            if (!coordinator.Auth.HasRefreshToken)
            {
                Interlocked.Exchange(ref loginRequestInFlight, 0);
                refreshLocalStatus();
                post(notifications, EzSettingsStrings.PIXIV_STATUS_NOT_CONFIGURED);
                return;
            }

            checkButton.Enabled.Value = false;

            Task.Run(() =>
            {
                bool success = coordinator.TryVerifyLogin(out string? account, out LocalisableString? error);

                Schedule(() =>
                {
                    Interlocked.Exchange(ref loginRequestInFlight, 0);
                    checkButton.Enabled.Value = true;

                    if (success)
                    {
                        statusNote.Value = new SettingsNote.Data(EzSettingsStrings.PIXIV_STATUS_LOGGED_IN.Format(account ?? "?"), SettingsNote.Type.Informational);
                        post(notifications, EzSettingsStrings.PIXIV_VERIFY_SUCCESS.Format(account ?? "?"));
                    }
                    else
                    {
                        statusNote.Value = new SettingsNote.Data(EzSettingsStrings.PIXIV_STATUS_INVALID, SettingsNote.Type.Warning);
                        post(notifications, error ?? EzSettingsStrings.PIXIV_VERIFY_FAILED);
                    }
                });
            });
        }

        private void refreshLocalStatus()
        {
            if (!coordinator.Auth.HasRefreshToken)
            {
                statusNote.Value = new SettingsNote.Data(EzSettingsStrings.PIXIV_STATUS_NOT_CONFIGURED, SettingsNote.Type.Informational);
                return;
            }

            statusNote.Value = new SettingsNote.Data(
                EzSettingsStrings.PIXIV_STATUS_LOGGED_IN.Format(coordinator.Auth.LoadAccountName() ?? "?"),
                SettingsNote.Type.Informational);
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

        private partial class PixivTokenFormTextBox : FormTextBox
        {
            internal override InnerTextBox CreateTextBox() => new PixivTokenInnerTextBox();

            private partial class PixivTokenInnerTextBox : InnerTextBox
            {
                public PixivTokenInnerTextBox()
                {
                    InputProperties = new TextInputProperties(TextInputType.Password, false);
                }
            }
        }
    }
}
