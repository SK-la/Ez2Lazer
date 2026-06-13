// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using WebRequest = osu.Framework.IO.Network.WebRequest;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivImageStore
    {
        private const int max_cached_pick_attempts = 32;

        private readonly Storage storage;
        private readonly Ez2ConfigManager ezConfig;

        public PixivImageStore(Storage storage, Ez2ConfigManager ezConfig)
        {
            this.storage = storage;
            this.ezConfig = ezConfig;
        }

        public bool TryGetRandomCachedIllust(PixivFilterService filters, out PixivIllustInfo illust, out string resourcePath) =>
            TryGetRandomCachedIllust(filters, excludeResourcePath: null, excludeIllustId: null, out illust, out resourcePath);

        public bool TryGetRandomCachedIllust(PixivFilterService filters, string? excludeResourcePath, out PixivIllustInfo illust, out string resourcePath) =>
            TryGetRandomCachedIllust(filters, excludeResourcePath, excludeIllustId: null, out illust, out resourcePath);

        public bool TryGetRandomCachedIllust(PixivFilterService filters, string? excludeResourcePath, long? excludeIllustId, out PixivIllustInfo illust, out string resourcePath)
        {
            illust = default;
            resourcePath = string.Empty;

            try
            {
                ensureCacheDirectory();

                string[] files = storage.GetFiles(EzModifyPath.BG_PIXIV_PATH)
                                        .Where(PixivFileNamer.IsSupportedImageExtension)
                                        .ToArray();

                if (files.Length == 0)
                    return false;

                int attempts = Math.Min(files.Length, max_cached_pick_attempts);

                for (int i = 0; i < attempts; i++)
                {
                    string file = files[RNG.Next(files.Length)];
                    string normalizedPath = file.Replace('\\', '/');

                    if (pathsEqual(normalizedPath, excludeResourcePath))
                        continue;

                    if (!PixivFileNamer.TryParseFileName(Path.GetFileName(file), out long illustId, out int page))
                        continue;

                    if (excludeIllustId is long excludedIllustId && illustId == excludedIllustId)
                        continue;

                    string account = string.Empty;
                    string userName = string.Empty;

                    if (PixivFileNamer.TryParseFileLabel(Path.GetFileName(file), out string fileLabel))
                        userName = PixivAccountNormalizer.Normalize(fileLabel);

                    if (!filters.AllowsCachedIllust(account, userName))
                        continue;

                    if (string.IsNullOrEmpty(userName))
                        userName = account;

                    resourcePath = normalizedPath;
                    illust = new PixivIllustInfo(account, illustId, page, string.Empty, userName: userName);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to enumerate Pixiv cache: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
                return false;
            }
        }

        public bool IsCached(PixivIllustInfo illust) => tryResolveExistingPath(illust, out _);

        public bool TryEnsureCached(PixivIllustInfo illust, out string resourcePath, out LocalisableString? error)
        {
            if (tryResolveExistingPath(illust, out resourcePath))
            {
                error = null;
                return true;
            }

            resourcePath = PixivFileNamer.BuildDownloadRelativePath(illust);
            return tryDownload(illust, resourcePath, out error);
        }

        private bool tryResolveExistingPath(PixivIllustInfo illust, out string resourcePath)
        {
            string extension = PixivFileNamer.GetExtensionFromUrl(illust.ImageUrl);

            string downloadPath = PixivFileNamer.BuildDownloadRelativePath(illust, extension);

            if (storage.Exists(downloadPath))
            {
                resourcePath = downloadPath;
                return true;
            }

            string idKeyPath = Path.Combine(EzModifyPath.BG_PIXIV_PATH, PixivFileNamer.BuildIdKeyFileName(illust.IllustId, illust.Page, extension))
                                   .Replace('\\', '/');

            if (storage.Exists(idKeyPath))
            {
                resourcePath = idKeyPath;
                return true;
            }

            foreach (string legacyLabel in getLegacyLabels(illust))
            {
                string legacyPath = Path.Combine(
                                            EzModifyPath.BG_PIXIV_PATH,
                                            $"{PixivFileNamer.SanitizeFileLabel(legacyLabel)}_{illust.IllustId}_p{illust.Page}{extension}")
                                        .Replace('\\', '/');

                if (storage.Exists(legacyPath))
                {
                    resourcePath = legacyPath;
                    return true;
                }
            }

            resourcePath = string.Empty;
            return false;
        }

        private static IEnumerable<string> getLegacyLabels(PixivIllustInfo illust)
        {
            if (!string.IsNullOrWhiteSpace(illust.Account))
                yield return illust.Account;

            if (!string.IsNullOrWhiteSpace(illust.UserName)
                && !string.Equals(illust.UserName, illust.Account, StringComparison.OrdinalIgnoreCase))
                yield return illust.UserName;
        }

        private bool tryDownload(PixivIllustInfo illust, string resourcePath, out LocalisableString? error)
        {
            error = null;

            try
            {
                ensureCacheDirectory();

                string downloadUrl = PixivApiProxy.RewriteImageUrl(
                    illust.ImageUrl,
                    ezConfig.Get<string>(Ez2Setting.PixivApiProxyBaseUrl));

                using var request = new WebRequest(downloadUrl)
                {
                    Method = HttpMethod.Get,
                };

                request.AddHeader("Referer", PixivConstants.IMAGE_REFERER);
                request.AddHeader("User-Agent", PixivConstants.USER_AGENT);
                PixivWebRequest.ConfigureImageDownload(request);

                request.Perform();

                if (request.ResponseStatusCode != HttpStatusCode.OK)
                {
                    error = EzSettingsStrings.PIXIV_ERROR_IMAGE_DOWNLOAD_FAILED;
                    Logger.Log($"[Pixiv] image download HTTP {request.ResponseStatusCode}: {request.GetResponseString()}", LoggingTarget.Network, LogLevel.Important);
                    return false;
                }

                byte[]? data = request.GetResponseData();

                if (data != null && data.Length == 0)
                {
                    error = EzSettingsStrings.PIXIV_ERROR_IMAGE_EMPTY;
                    return false;
                }

                using var stream = storage.CreateFileSafely(resourcePath);
                if (data != null) stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                error = EzSettingsStrings.PIXIV_ERROR_REQUEST_FAILED;
                Logger.Log($"[Pixiv] image download: {ex.Message}", LoggingTarget.Network, LogLevel.Important);
                return false;
            }
        }

        private void ensureCacheDirectory()
        {
            string fullPath = storage.GetFullPath(EzModifyPath.BG_PIXIV_PATH);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }

        private static bool pathsEqual(string left, string? right) => !string.IsNullOrWhiteSpace(right)
                                                                      && string.Equals(left.Replace('\\', '/'), right.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
