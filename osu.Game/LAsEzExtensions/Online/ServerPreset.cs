// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Localization;

namespace osu.Game.LAsEzExtensions.Online
{
    /// <summary>
    /// 服务器预设选项
    /// </summary>
    public enum ServerPreset
    {
        /// <summary>
        /// 官方服务器（默认）
        /// </summary>
        [LocalisableDescription(typeof(ServerPresetStrings), nameof(ServerPresetStrings.OFFICIAL))]
        Official = 0,

        /// <summary>
        /// 手动输入服务器地址
        /// </summary>
        [LocalisableDescription(typeof(ServerPresetStrings), nameof(ServerPresetStrings.MANUAL))]
        Manual = 1,

        /// <summary>
        /// Gu 预设服务器
        /// </summary>
        [LocalisableDescription(typeof(ServerPresetStrings), nameof(ServerPresetStrings.GU))]
        Gu = 2,
    }

    public static class ServerPresetStrings
    {
        public static readonly LocalisableString OFFICIAL = new EzLocalizationManager.EzLocalisableString("官方服务器", "Official Server");
        public static readonly LocalisableString MANUAL = new EzLocalizationManager.EzLocalisableString("手动输入", "Manual");
        public static readonly LocalisableString GU = new EzLocalizationManager.EzLocalisableString("Gu 服务器", "Gu Server");
    }

    /// <summary>
    /// 服务器配置信息
    /// </summary>
    public class ServerConfig
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string SpectatorUrl { get; set; } = string.Empty;
        public string MultiplayerUrl { get; set; } = string.Empty;
        public string MetadataUrl { get; set; } = string.Empty;

        public ServerConfig()
        {
        }

        public ServerConfig(string apiUrl, string websiteUrl, string clientId = "", string clientSecret = "",
                            string spectatorUrl = "", string multiplayerUrl = "", string metadataUrl = "")
        {
            ApiUrl = apiUrl;
            WebsiteUrl = websiteUrl;
            ClientId = clientId;
            ClientSecret = clientSecret;
            SpectatorUrl = spectatorUrl;
            MultiplayerUrl = multiplayerUrl;
            MetadataUrl = metadataUrl;
        }
    }

    public static class ServerPresetExtensions
    {
        public static LocalisableString GetDescription(this ServerPreset preset)
        {
            return preset.GetLocalisableDescription();
        }

        public static ServerConfig GetServerConfig(this ServerPreset preset)
        {
            return preset switch
            {
                ServerPreset.Official => new ServerConfig(
                    apiUrl: "https://osu.ppy.sh",
                    websiteUrl: "https://osu.ppy.sh"
                ),
                ServerPreset.Gu => new ServerConfig(
                    apiUrl: "https://lazer-api.g0v0.top",
                    websiteUrl: "https://lazer.g0v0.top"
                ),
                _ => new ServerConfig()
            };
        }
    }
}
