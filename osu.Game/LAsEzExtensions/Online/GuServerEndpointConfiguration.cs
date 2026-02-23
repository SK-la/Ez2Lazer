// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online;

namespace osu.Game.LAsEzExtensions.Online
{
    public class GuServerEndpointConfiguration : EndpointConfiguration
    {
        public GuServerEndpointConfiguration()
        {
            // Gu服务器配置 (g0v0演示服务器)
            APIUrl = @"https://lazer-api.g0v0.top";
            WebsiteUrl = @"https://lazer.g0v0.top";

            // OAuth应用ID和密钥 - 来自g0v0-server默认配置
            APIClientID = "5";
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk"; // g0v0演示服务器默认OAuth应用密钥

            // 从APIUrl自动生成SignalR端点（遵循g0v0服务器部署方式）
            SpectatorUrl = APIUrl + "/signalr/spectator";
            MultiplayerUrl = APIUrl + "/signalr/multiplayer";
            MetadataUrl = APIUrl + "/signalr/metadata";
            BeatmapSubmissionServiceUrl = APIUrl + "/beatmap-submission";
        }
    }
}
