// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Rewrites Pixiv API and image CDN requests to a user-provided reverse proxy base.
    /// Compatible with proxies such as <see href="https://github.com/vmoranv/pixiv-proxy">pixiv-proxy</see>.
    /// OAuth (<c>oauth.secure.pixiv.net</c>) is never rewritten.
    /// Authenticated calls and image downloads must also pass <see cref="IsAllowedApiUrl"/> or <see cref="IsAllowedImageUrl"/>:
    /// HTTPS on the official host, or the configured proxy's scheme, host, and port.
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

        /// <summary>
        /// Whether an authenticated app-API request may be sent to <paramref name="url"/>.
        /// </summary>
        public static bool IsAllowedApiUrl(string? url, string? proxyBase)
            => isAllowedRequestUrl(url, proxyBase, official_api_host);

        /// <summary>
        /// Whether an image download may be sent to <paramref name="url"/>.
        /// </summary>
        public static bool IsAllowedImageUrl(string? url, string? proxyBase)
            => isAllowedRequestUrl(url, proxyBase, official_image_host);

        private static bool isAllowedRequestUrl(string? url, string? proxyBase, string officialHost)
        {
            Uri? uri = parseRequestUri(url);

            if (uri == null)
                return false;

            if (isOfficialHttpsHost(uri, officialHost))
                return true;

            return matchesConfiguredProxy(uri, proxyBase);
        }

        private static Uri? parseRequestUri(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !hasSafeUrlText(url))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri == null)
                return null;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            if (!string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.IdnHost))
                return null;

            return uri;
        }

        private static bool hasSafeUrlText(string url)
        {
            foreach (char c in url)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c) || c is '\\' or '@')
                    return false;
            }

            return true;
        }

        private static bool isOfficialHttpsHost(Uri uri, string officialHost)
            => uri.Scheme == Uri.UriSchemeHttps
               && uri.IsDefaultPort
               && uri.IdnHost.Equals(officialHost, StringComparison.OrdinalIgnoreCase);

        private static bool matchesConfiguredProxy(Uri uri, string? proxyBase)
        {
            string normalizedBase = NormalizeBase(proxyBase);

            if (string.IsNullOrEmpty(normalizedBase))
                return false;

            if (!Uri.TryCreate(normalizedBase, UriKind.Absolute, out Uri? proxyUri) || proxyUri == null)
                return false;

            if (proxyUri.Scheme != Uri.UriSchemeHttp && proxyUri.Scheme != Uri.UriSchemeHttps)
                return false;

            if (string.IsNullOrEmpty(proxyUri.IdnHost))
                return false;

            if (!uri.Scheme.Equals(proxyUri.Scheme, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!uri.IdnHost.Equals(proxyUri.IdnHost, StringComparison.OrdinalIgnoreCase))
                return false;

            return uri.Port == proxyUri.Port;
        }

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
