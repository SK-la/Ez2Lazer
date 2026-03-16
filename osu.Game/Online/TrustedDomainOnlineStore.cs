// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Online;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        protected override string GetLookupUrl(string url)
        {
            ServerPreset customApiUrl = GlobalConfigStore.EzConfig.Get<ServerPreset>(Ez2Setting.ServerPreset);

            switch (customApiUrl)
            {
                case ServerPreset.Manual:
                case ServerPreset.Gu:
                    #if DEBUG
                    // 任何从服务器获取资源的事件都会引发这个日志输出
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri1) || !uri1.Host.EndsWith(@".ppy.sh", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log($@"[Ez2Lazer] Using Custom ApiUrl {url}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    }
                    #endif

                    return url;

                default:
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || !uri.Host.EndsWith(@".ppy.sh", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log($@"Blocking resource lookup from external website: {url}", LoggingTarget.Network, LogLevel.Important);
                        return string.Empty;
                    }

                    return url;
            }
        }
    }
}
