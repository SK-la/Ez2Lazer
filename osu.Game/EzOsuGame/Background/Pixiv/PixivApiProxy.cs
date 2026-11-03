// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Rewrites Pixiv API and image CDN requests to a user-provided reverse proxy base.
    /// Compatible with proxies such as <see href="https://github.com/vmoranv/pixiv-proxy">pixiv-proxy</see>.
    /// OAuth (<c>oauth.secure.pixiv.net</c>) is never rewritten.
    /// </summary>
    internal static class PixivApiProxy
    {
        private const string official_api_host = "app-api.pixiv.net";
        private const string official_image_host = "i.pximg.net";
        private const string image_proxy_path_prefix = "/image";

        public static string NormalizeBase(string? proxyBase)
        {
            if (string.IsNullOrWhiteSpace(proxyBase))
                return string.Empty;

            return proxyBase.Trim().TrimEnd('/');
        }

        public static string GetInitialFollowFeedUrl(string? proxyBase)
            => RewriteApiUrl(PixivConstants.ILLUST_FOLLOW_INITIAL_URL, proxyBase);

        public static string RewriteApiUrl(string url, string? proxyBase)
            => rewriteUrl(url, proxyBase, official_api_host, additionalPathPrefix: null);

        public static string RewriteImageUrl(string url, string? proxyBase)
            => rewriteUrl(url, proxyBase, official_image_host, image_proxy_path_prefix);

        private static string rewriteUrl(string url, string? proxyBase, string officialHost, string? additionalPathPrefix)
        {
            string normalizedBase = NormalizeBase(proxyBase);

            if (string.IsNullOrEmpty(normalizedBase) || string.IsNullOrWhiteSpace(url))
                return url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? sourceUri))
                return url;

            if (!sourceUri.Host.Equals(officialHost, StringComparison.OrdinalIgnoreCase))
                return url;

            if (!Uri.TryCreate(normalizedBase, UriKind.Absolute, out Uri? proxyUri))
                return url;

            string proxyPath = proxyUri.AbsolutePath.TrimEnd('/');
            string requestPath = sourceUri.AbsolutePath;

            if (!string.IsNullOrEmpty(additionalPathPrefix))
                requestPath = additionalPathPrefix + requestPath;

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
