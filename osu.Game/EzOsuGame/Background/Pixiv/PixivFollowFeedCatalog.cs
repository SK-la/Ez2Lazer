// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Filtered follow-feed illustration list shared by menu backgrounds and optional prefetch.
    /// </summary>
    internal class PixivFollowFeedCatalog
    {
        private readonly PixivApiClient api;
        private readonly PixivAuthService auth;
        private readonly PixivImageStore images;

        private readonly object gate = new();
        private readonly List<PixivIllustInfo> entries = new();

        private string? nextPageUrl;
        private bool feedExhausted;

        public PixivFollowFeedCatalog(PixivApiClient api, PixivAuthService auth, PixivImageStore images)
        {
            this.api = api;
            this.auth = auth;
            this.images = images;
        }

        public int Count
        {
            get
            {
                lock (gate)
                    return entries.Count;
            }
        }

        public int GetUncachedCount()
        {
            lock (gate)
                return entries.Count(entry => !images.IsCached(entry));
        }

        public bool EnsureMinimum(out string? error)
            => EnsureMinimum(PixivConstants.FOLLOW_FEED_MIN_CATALOG_SIZE, out error);

        public bool EnsureMinimum(int minimum, out string? error)
        {
            lock (gate)
                return ensureMinimumLocked(minimum, out error);
        }

        public bool AppendNextPage(out string? error)
        {
            lock (gate)
            {
                if (feedExhausted)
                {
                    error = null;
                    return false;
                }

                return appendPageLocked(out error);
            }
        }

        public bool TryPickRandom(long? excludeIllustId, out PixivIllustInfo illust)
        {
            illust = default;

            lock (gate)
            {
                if (entries.Count == 0)
                    return false;

                List<PixivIllustInfo> pool = excludeIllustId is long excludedId
                    ? entries.Where(entry => entry.IllustId != excludedId).ToList()
                    : entries.ToList();

                if (pool.Count == 0)
                    pool = entries.ToList();

                illust = pool[RNG.Next(pool.Count)];
                return true;
            }
        }

        public bool TryGetNextUncached(long? excludeIllustId, out PixivIllustInfo illust)
        {
            illust = default;

            lock (gate)
            {
                IEnumerable<PixivIllustInfo> uncached = entries.Where(entry => !images.IsCached(entry));

                if (excludeIllustId is long excludedId)
                    uncached = uncached.Where(entry => entry.IllustId != excludedId);

                var list = uncached.ToList();

                if (list.Count == 0)
                    return false;

                illust = list[RNG.Next(list.Count)];
                return true;
            }
        }

        public void Invalidate()
        {
            lock (gate)
            {
                entries.Clear();
                nextPageUrl = null;
                feedExhausted = false;
            }
        }

        private bool ensureMinimumLocked(int minimum, out string? error)
        {
            error = null;

            if (!refreshAccessToken(out error))
                return false;

            int pagesFetched = 0;

            while (entries.Count < minimum && !feedExhausted && pagesFetched < PixivConstants.FOLLOW_FEED_MAX_PAGES_PER_BUILD)
            {
                if (!appendPageLocked(out error))
                    return false;

                pagesFetched++;
            }

            logCatalogState();

            if (entries.Count == 0)
            {
                error = "Pixiv follow feed returned no illustrations after filtering.";
                return false;
            }

            return true;
        }

        private bool appendPageLocked(out string? error)
        {
            error = null;

            if (feedExhausted)
                return false;

            if (!refreshAccessToken(out error))
                return false;

            string requestUrl = nextPageUrl ?? PixivConstants.ILLUST_FOLLOW_INITIAL_URL;

            if (!api.TryFetchFollowFeedPage(requestUrl, entries, out string? newNextUrl, out error))
                return false;

            nextPageUrl = newNextUrl;
            feedExhausted = string.IsNullOrWhiteSpace(newNextUrl);

            logCatalogState();
            return true;
        }

        private bool refreshAccessToken(out string? error)
        {
            if (!auth.TryRefreshAccessToken(out string? accessToken, out error))
                return false;

            api.SetAccessToken(accessToken!);
            return true;
        }

        private void logCatalogState()
        {
            int cached = entries.Count(entry => images.IsCached(entry));
            Logger.Log(
                $"[Pixiv] Catalog: {entries.Count} filtered entries ({cached} cached locally).",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);
        }
    }
}
