// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Online;

namespace osu.Game.EzOsuGame.Online
{
    public class ManualServerEndpointConfiguration : EndpointConfiguration
    {
        public ManualServerEndpointConfiguration(Ez2ConfigManager ez2Config)
        {
            // 从配置读取手动输入的服务器地址 - 空值检查
            string webUrl = ez2Config.Get<string>(Ez2Setting.CustomWebsiteUrl);
            WebsiteUrl = !string.IsNullOrEmpty(webUrl) ? webUrl.TrimEnd('/') : @"https://osu.ppy.sh";

            string apiUrl = ez2Config.Get<string>(Ez2Setting.CustomApiUrl);
            APIUrl = !string.IsNullOrEmpty(apiUrl) ? apiUrl.TrimEnd('/') : @"https://osu.ppy.sh";

            // Client凭证 - 空值检查
            string clientSecret = ez2Config.Get<string>(Ez2Setting.CustomClientSecret);
            APIClientSecret = !string.IsNullOrEmpty(clientSecret) ? clientSecret : @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk";

            string clientId = ez2Config.Get<string>(Ez2Setting.CustomClientId);
            APIClientID = !string.IsNullOrEmpty(clientId) ? clientId : "5";

            // 如果用户提供了特定的SignalR URLs，就使用它们
            // 否则从APIUrl自动生成（遵循g0v0/LazerAuthlibInjection的方式）
            string customSpectatorUrl = ez2Config.Get<string>(Ez2Setting.CustomSpectatorUrl);
            SpectatorUrl = !string.IsNullOrEmpty(customSpectatorUrl)
                ? customSpectatorUrl
                : APIUrl + "/signalr/spectator";

            string customMultiplayerUrl = ez2Config.Get<string>(Ez2Setting.CustomMultiplayerUrl);
            MultiplayerUrl = !string.IsNullOrEmpty(customMultiplayerUrl)
                ? customMultiplayerUrl
                : APIUrl + "/signalr/multiplayer";

            string customMetadataUrl = ez2Config.Get<string>(Ez2Setting.CustomMetadataUrl);
            MetadataUrl = !string.IsNullOrEmpty(customMetadataUrl)
                ? customMetadataUrl
                : APIUrl + "/signalr/metadata";

            BeatmapSubmissionServiceUrl = APIUrl + "/beatmap-submission";
        }
    }
}
