// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;

namespace osu.Game.LAsEzExtensions.Online
{
    public partial class ServerSettings : SettingsSubsection
    {
        private static readonly LocalisableString header_text = new EzLocalizationManager.EzLocalisableString("服务器配置", "Server Settings");
        private static readonly LocalisableString server_preset_caption = new EzLocalizationManager.EzLocalisableString("服务器选择", "Server Preset");

        private static readonly LocalisableString server_preset_restart_prompt = new EzLocalizationManager.EzLocalisableString(
            "服务器预设已更改，需要重启客户端才能生效。\n是否立即重启？",
            "Server preset changed. A restart is required to apply changes.\nRestart now?");

        private static readonly LocalisableString api_url_caption = new EzLocalizationManager.EzLocalisableString("API 地址", "API URL");
        private static readonly LocalisableString website_url_caption = new EzLocalizationManager.EzLocalisableString("网站地址", "Website URL");
        private static readonly LocalisableString client_id_caption = new EzLocalizationManager.EzLocalisableString("客户端 ID", "Client ID");
        private static readonly LocalisableString client_secret_caption = new EzLocalizationManager.EzLocalisableString("客户端密钥", "Client Secret");
        private static readonly LocalisableString spectator_url_caption = new EzLocalizationManager.EzLocalisableString("观战服务器地址", "Spectator Server URL");
        private static readonly LocalisableString multiplayer_url_caption = new EzLocalizationManager.EzLocalisableString("多人游戏服务器地址", "Multiplayer Server URL");
        private static readonly LocalisableString metadata_url_caption = new EzLocalizationManager.EzLocalisableString("元数据服务器地址", "Metadata Server URL");

        protected override LocalisableString Header => header_text;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private Bindable<ServerPreset> serverPresetBindable = null!;

        private FormTextBox apiUrlTextBox = null!;
        private FormTextBox websiteUrlTextBox = null!;
        private FormTextBox clientIdTextBox = null!;
        private FormTextBox clientSecretTextBox = null!;
        private FormTextBox spectatorUrlTextBox = null!;
        private FormTextBox multiplayerUrlTextBox = null!;
        private FormTextBox metadataUrlTextBox = null!;

        private SettingsItemV2 apiUrlItem = null!;
        private SettingsItemV2 websiteUrlItem = null!;
        private SettingsItemV2 clientIdItem = null!;
        private SettingsItemV2 clientSecretItem = null!;
        private SettingsItemV2 spectatorUrlItem = null!;
        private SettingsItemV2 multiplayerUrlItem = null!;
        private SettingsItemV2 metadataUrlItem = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager config)
        {
            serverPresetBindable = config.GetBindable<ServerPreset>(Ez2Setting.ServerPreset);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<ServerPreset>
                {
                    Caption = server_preset_caption,
                    Current = serverPresetBindable,
                }),
                // 手动输入服务器配置项 - 默认隐藏
                apiUrlItem = new SettingsItemV2(apiUrlTextBox = new FormTextBox
                {
                    Caption = api_url_caption,
                    PlaceholderText = "https://osu.ppy.sh",
                    Current = config.GetBindable<string>(Ez2Setting.CustomApiUrl)
                })
                {
                    Alpha = 0,
                },
                websiteUrlItem = new SettingsItemV2(websiteUrlTextBox = new FormTextBox
                {
                    Caption = website_url_caption,
                    PlaceholderText = "https://osu.ppy.sh",
                    Current = config.GetBindable<string>(Ez2Setting.CustomWebsiteUrl)
                })
                {
                    Alpha = 0,
                },
                clientIdItem = new SettingsItemV2(clientIdTextBox = new FormTextBox
                {
                    Caption = client_id_caption,
                    PlaceholderText = "Client ID",
                    Current = config.GetBindable<string>(Ez2Setting.CustomClientId)
                })
                {
                    Alpha = 0,
                },
                clientSecretItem = new SettingsItemV2(clientSecretTextBox = new FormTextBox
                {
                    Caption = client_secret_caption,
                    PlaceholderText = "Client Secret",
                    Current = config.GetBindable<string>(Ez2Setting.CustomClientSecret)
                })
                {
                    Alpha = 0,
                },
                spectatorUrlItem = new SettingsItemV2(spectatorUrlTextBox = new FormTextBox
                {
                    Caption = spectator_url_caption,
                    PlaceholderText = "Spectator Url",
                    Current = config.GetBindable<string>(Ez2Setting.CustomSpectatorUrl)
                })
                {
                    Alpha = 0,
                },
                multiplayerUrlItem = new SettingsItemV2(multiplayerUrlTextBox = new FormTextBox
                {
                    Caption = multiplayer_url_caption,
                    PlaceholderText = "Multiplayer Url",
                    Current = config.GetBindable<string>(Ez2Setting.CustomMultiplayerUrl)
                })
                {
                    Alpha = 0,
                },
                metadataUrlItem = new SettingsItemV2(metadataUrlTextBox = new FormTextBox
                {
                    Caption = metadata_url_caption,
                    PlaceholderText = "Metadata Url",
                    Current = config.GetBindable<string>(Ez2Setting.CustomMetadataUrl)
                })
                {
                    Alpha = 0,
                },
            };

            // 根据服务器预设显示/隐藏手动输入框
            serverPresetBindable.BindValueChanged(presetChanged, true);
            serverPresetBindable.BindValueChanged(onServerPresetChanged);
        }

        private void onServerPresetChanged(ValueChangedEvent<ServerPreset> e)
        {
            if (e.NewValue == ServerPreset.Official || e.NewValue == ServerPreset.Gu)
                dialogOverlay?.Push(new ConfirmDialog(server_preset_restart_prompt, () => game.Exit()));
        }

        private void presetChanged(ValueChangedEvent<ServerPreset> e)
        {
            bool showManualInputs = e.NewValue == ServerPreset.Manual;

            // 显示或隐藏手动输入框
            apiUrlItem.FadeTo(showManualInputs ? 1 : 0, 200);
            websiteUrlItem.FadeTo(showManualInputs ? 1 : 0, 200);
            clientIdItem.FadeTo(showManualInputs ? 1 : 0, 200);
            clientSecretItem.FadeTo(showManualInputs ? 1 : 0, 200);
            spectatorUrlItem.FadeTo(showManualInputs ? 1 : 0, 200);
            multiplayerUrlItem.FadeTo(showManualInputs ? 1 : 0, 200);
            metadataUrlItem.FadeTo(showManualInputs ? 1 : 0, 200);

            // 根据不同的服务器预设加载对应的URL（如果预设不是手动）
            if (e.NewValue != ServerPreset.Manual)
            {
                ServerConfig serverConfig = e.NewValue.GetServerConfig();

                if (!string.IsNullOrEmpty(serverConfig.ApiUrl))
                    apiUrlTextBox.Current.Value = serverConfig.ApiUrl;

                if (!string.IsNullOrEmpty(serverConfig.WebsiteUrl))
                    websiteUrlTextBox.Current.Value = serverConfig.WebsiteUrl;

                if (!string.IsNullOrEmpty(serverConfig.ClientId))
                    clientIdTextBox.Current.Value = serverConfig.ClientId;

                if (!string.IsNullOrEmpty(serverConfig.ClientSecret))
                    clientSecretTextBox.Current.Value = serverConfig.ClientSecret;

                if (!string.IsNullOrEmpty(serverConfig.SpectatorUrl))
                    spectatorUrlTextBox.Current.Value = serverConfig.SpectatorUrl;

                if (!string.IsNullOrEmpty(serverConfig.MultiplayerUrl))
                    multiplayerUrlTextBox.Current.Value = serverConfig.MultiplayerUrl;

                if (!string.IsNullOrEmpty(serverConfig.MetadataUrl))
                    metadataUrlTextBox.Current.Value = serverConfig.MetadataUrl;
            }
        }
    }
}
