// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Security.Cryptography;
using System.Text;
using osu.Framework.IO.Network;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivRequestHeaders
    {
        public static void ApplyAppApiHeaders(WebRequest request, string? accessToken = null)
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
                request.AddHeader("Authorization", $"Bearer {accessToken}");

            request.AddHeader("User-Agent", PixivConstants.USER_AGENT);
            request.AddHeader("App-OS", PixivConstants.APP_OS);
            request.AddHeader("App-OS-Version", PixivConstants.APP_OS_VERSION);
            request.AddHeader("App-Version", PixivConstants.APP_VERSION);
            request.AddHeader("Accept-Language", "zh-CN");
            request.AddHeader("Referer", PixivConstants.API_REFERER);

            string clientTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
            request.AddHeader("X-Client-Time", clientTime);
            request.AddHeader("X-Client-Hash", computeClientHash(clientTime));
        }

        public static void ApplyOAuthHeaders(WebRequest request)
        {
            request.AddHeader("User-Agent", PixivConstants.USER_AGENT);
            request.AddHeader("App-OS", PixivConstants.APP_OS);
            request.AddHeader("App-OS-Version", PixivConstants.APP_OS_VERSION);
            request.AddHeader("App-Version", PixivConstants.APP_VERSION);

            string clientTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
            request.AddHeader("X-Client-Time", clientTime);
            request.AddHeader("X-Client-Hash", computeClientHash(clientTime));
        }

        private static string computeClientHash(string clientTime)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(clientTime + PixivConstants.CLIENT_HASH_SECRET));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
