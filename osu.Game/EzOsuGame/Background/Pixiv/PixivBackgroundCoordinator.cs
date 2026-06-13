// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivBackgroundCoordinator
    {
        private readonly Ez2ConfigManager ezConfig;
        private int songChangeDownloadInFlight;

        private long lastIllustId;
        private string lastResourcePath = string.Empty;

        public PixivAuthService Auth { get; }
        public PixivApiClient Api { get; }
        public PixivImageStore Images { get; }
        public PixivFilterService Filters { get; }
        internal PixivFollowFeedCatalog Catalog { get; }

        public PixivBackgroundCoordinator(Storage storage, Ez2ConfigManager ezConfig)
        {
            this.ezConfig = ezConfig;
            Auth = new PixivAuthService(storage);
            Filters = new PixivFilterService(ezConfig);
            Api = new PixivApiClient(Auth, Filters, ezConfig);
            Images = new PixivImageStore(storage);
            Catalog = new PixivFollowFeedCatalog(Api, Auth, Images);

            ezConfig.GetBindable<string>(Ez2Setting.PixivApiProxyBaseUrl).BindValueChanged(_ => Catalog.Invalidate());
        }

        /// <summary>
        /// Fast path for menu background switches: local BG_PIXIV only, no network.
        /// </summary>
        public bool TryPickImmediateLocalBackground(out PixivIllustInfo illust, out string resourcePath)
        {
            illust = default;
            resourcePath = string.Empty;

            long? excludeIllustId = lastIllustId > 0 ? lastIllustId : null;
            string excludeResourcePath = lastResourcePath;

            if (Images.TryGetRandomCachedIllust(Filters, excludeResourcePath, excludeIllustId, out illust, out resourcePath)
                || Images.TryGetRandomCachedIllust(Filters, excludeResourcePath: null, excludeIllustId, out illust, out resourcePath)
                || Images.TryGetRandomCachedIllust(Filters, out illust, out resourcePath))
            {
                rememberDisplay(illust, resourcePath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Queues one follow-feed download for the next switch. Never blocks the caller.
        /// </summary>
        public void EnqueueSongChangeDownload()
        {
            if (Interlocked.CompareExchange(ref songChangeDownloadInFlight, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    if (!tryDownloadNextCatalogIllust(out string? error))
                        LogFailure("Background prefetch", error);
                }
                finally
                {
                    Interlocked.Exchange(ref songChangeDownloadInFlight, 0);
                }
            });
        }

        public bool RunBackgroundPrefetch(out string? error)
        {
            if (!ezConfig.Get<bool>(Ez2Setting.PixivAutoDownloadEnabled))
            {
                error = null;
                return false;
            }

            return tryDownloadNextCatalogIllust(out error);
        }

        public void LogFailure(string context, string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return;

            Logger.Log($"[Pixiv] {context}: {error}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
        }

        private bool tryDownloadNextCatalogIllust(out string? error)
        {
            error = null;

            if (!Auth.HasRefreshToken)
            {
                error = "Pixiv refresh token is not configured.";
                return false;
            }

            if (!Catalog.EnsureMinimum(out error))
                return false;

            if (Catalog.GetUncachedCount() < PixivConstants.FOLLOW_FEED_MIN_CATALOG_SIZE)
                Catalog.AppendNextPage(out error);

            if (!Catalog.TryGetNextUncached(lastIllustId > 0 ? lastIllustId : null, out PixivIllustInfo illust))
                return false;

            return Images.TryEnsureCached(illust, out _, out error);
        }

        private void rememberDisplay(PixivIllustInfo illust, string resourcePath)
        {
            lastIllustId = illust.IllustId;
            lastResourcePath = resourcePath;
        }
    }
}
