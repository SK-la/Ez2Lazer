// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivBackgroundCoordinator
    {
        public PixivAuthService Auth { get; }
        public PixivApiClient Api { get; }
        public PixivImageStore Images { get; }

        public PixivBackgroundCoordinator(Storage storage)
        {
            Auth = new PixivAuthService(storage);
            Api = new PixivApiClient(Auth);
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

            if (Images.TryGetRandomCachedIllust(out illust, out resourcePath))
                return true;

            if (!Api.TryGetRandomFollowIllust(out illust, out error))
                return false;

            if (!Images.TryEnsureCached(illust, out resourcePath, out error))
                return false;

            return true;
        }

        public bool TryDownloadNextUncached(out string? error)
        {
            error = null;

            if (!Auth.HasRefreshToken)
            {
                error = "Pixiv refresh token is not configured.";
                return false;
            }

            if (!Api.TryGetUncachedFollowIllust(Images, out PixivIllustInfo illust, out error))
                return false;

            return Images.TryEnsureCached(illust, out _, out error);
        }

        public void LogFailure(string context, string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return;

            Logger.Log($"[Pixiv] {context}: {error}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
        }
    }
}
