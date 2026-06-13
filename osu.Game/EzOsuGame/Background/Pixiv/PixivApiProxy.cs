// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Rewrites <c>app-api.pixiv.net</c> requests to a user-provided reverse proxy base (API only).
    /// Compatible with proxies such as <see href="https://github.com/vmoranv/pixiv-proxy">pixiv-proxy</see>.
    /// </summary>
    internal static class PixivApiProxy
    {
        private const string official_api_host = "app-api.pixiv.net";

        public static string NormalizeBase(string? proxyBase)
        {
            if (string.IsNullOrWhiteSpace(proxyBase))
                return string.Empty;

            return proxyBase.Trim().TrimEnd('/');
        }

        public static string GetInitialFollowFeedUrl(string? proxyBase)
            => RewriteApiUrl(PixivConstants.ILLUST_FOLLOW_INITIAL_URL, proxyBase);

        public static string RewriteApiUrl(string url, string? proxyBase)
        {
            string normalizedBase = NormalizeBase(proxyBase);

            if (string.IsNullOrEmpty(normalizedBase) || string.IsNullOrWhiteSpace(url))
                return url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? sourceUri))
                return url;

            if (!sourceUri.Host.Equals(official_api_host, StringComparison.OrdinalIgnoreCase))
                return url;

            if (!Uri.TryCreate(normalizedBase, UriKind.Absolute, out Uri? proxyUri))
                return url;

            string proxyPath = proxyUri.AbsolutePath.TrimEnd('/');
            string requestPath = sourceUri.AbsolutePath;

            string combinedPath = string.IsNullOrEmpty(proxyPath) || proxyPath == "/"
                ? requestPath
                : proxyPath + requestPath;

            var builder = new UriBuilder(sourceUri)
            {
                Scheme = proxyUri.Scheme,
                Host = proxyUri.Host,
                Port = proxyUri.IsDefaultPort ? -1 : proxyUri.Port,
                Path = combinedPath,
            };

            return builder.Uri.ToString();
        }
    }
}
