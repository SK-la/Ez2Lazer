// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Utils;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivImageStore
    {
        private const int max_cached_pick_attempts = 32;

        private readonly Storage storage;

        public PixivImageStore(Storage storage)
        {
            this.storage = storage;
        }

        public bool TryGetRandomCachedIllust(PixivFilterService filters, out PixivIllustInfo illust, out string resourcePath)
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

                    if (!PixivFileNamer.TryParseFileName(Path.GetFileName(file), out string account, out long illustId, out int page))
                        continue;

                    account = PixivAccountNormalizer.Normalize(account);

                    if (!filters.AllowsCachedAccount(account))
                        continue;

                    resourcePath = file.Replace('\\', '/');
                    illust = new PixivIllustInfo(account, illustId, page, string.Empty);
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

        public bool IsCached(PixivIllustInfo illust)
        {
            string resourcePath = PixivFileNamer.BuildRelativePath(
                illust.Account,
                illust.IllustId,
                illust.Page,
                PixivFileNamer.GetExtensionFromUrl(illust.ImageUrl));

            return storage.Exists(resourcePath);
        }

        public bool TryEnsureCached(PixivIllustInfo illust, out string resourcePath, out string? error)
        {
            resourcePath = PixivFileNamer.BuildRelativePath(
                illust.Account,
                illust.IllustId,
                illust.Page,
                PixivFileNamer.GetExtensionFromUrl(illust.ImageUrl));

            if (storage.Exists(resourcePath))
            {
                error = null;
                return true;
            }

            return tryDownload(illust, resourcePath, out error);
        }

        private bool tryDownload(PixivIllustInfo illust, string resourcePath, out string? error)
        {
            error = null;

            try
            {
                ensureCacheDirectory();

                using var request = new Framework.IO.Network.WebRequest(illust.ImageUrl)
                {
                    Method = HttpMethod.Get,
                };

                request.AddHeader("Referer", PixivConstants.IMAGE_REFERER);
                request.AddHeader("User-Agent", PixivConstants.USER_AGENT);

                request.Perform();

                if (request.ResponseStatusCode != HttpStatusCode.OK)
                {
                    error = request.GetResponseString() ?? "Failed to download Pixiv image.";
                    return false;
                }

                byte[]? data = request.GetResponseData();

                if (data != null && data.Length == 0)
                {
                    error = "Downloaded Pixiv image was empty.";
                    return false;
                }

                using var stream = storage.CreateFileSafely(resourcePath);
                if (data != null) stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void ensureCacheDirectory()
        {
            string fullPath = storage.GetFullPath(EzModifyPath.BG_PIXIV_PATH);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }
}
