// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivBackgroundCoordinator
    {
        private const int max_resolve_attempts = 5;

        public PixivAuthService Auth { get; }
        public PixivApiClient Api { get; }
        public PixivImageStore Images { get; }
        public PixivFilterService Filters { get; }

        public PixivBackgroundCoordinator(Storage storage, Ez2ConfigManager ezConfig)
        {
            Auth = new PixivAuthService(storage);
            Filters = new PixivFilterService(ezConfig);
            Api = new PixivApiClient(Auth, Filters);
            Images = new PixivImageStore(storage);
        }

        public bool TryResolveBackground(out PixivIllustInfo illust, out string resourcePath, out string? error)
        {
            illust = default;
            resourcePath = string.Empty;
            error = null;

            if (!Auth.HasRefreshToken)
            {
                error = "Pixiv refresh token is not configured.";
                return false;
            }

            if (Images.TryGetRandomCachedIllust(Filters, out illust, out resourcePath))
                return true;

            for (int attempt = 0; attempt < max_resolve_attempts; attempt++)
            {
                if (!Api.TryGetRandomFollowIllust(out illust, out error))
                    return false;

                if (!Filters.ShouldSaveToDisk(illust))
                    continue;

                if (Images.TryEnsureCached(illust, out resourcePath, out error))
                    return true;

                if (!string.IsNullOrWhiteSpace(error))
                    return false;
            }

            error = "No Pixiv illustrations matched the current filter rules.";
            return false;
        }

        public bool TryDownloadNextUncached(out string? error)
        {
            error = null;

            if (!Auth.HasRefreshToken)
            {
                error = "Pixiv refresh token is not configured.";
                return false;
            }

            for (int attempt = 0; attempt < max_resolve_attempts; attempt++)
            {
                if (!Api.TryGetUncachedFollowIllust(Images, out PixivIllustInfo illust, out error))
                    return false;

                if (!Filters.ShouldSaveToDisk(illust))
                    continue;

                return Images.TryEnsureCached(illust, out _, out error);
            }

            return false;
        }

        public void LogFailure(string context, string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return;

            Logger.Log($"[Pixiv] {context}: {error}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
        }
    }
}
