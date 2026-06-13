// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using WebRequest = osu.Framework.IO.Network.WebRequest;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivWebRequest
    {
        /// <summary>
        /// OAuth / JSON API calls: fail fast without blocking the UI for a retry cycle.
        /// </summary>
        public static void ConfigureApi(WebRequest request)
        {
            request.Timeout = PixivConstants.API_REQUEST_TIMEOUT_MS;
            request.AllowRetryOnTimeout = false;
        }

        /// <summary>
        /// Image downloads may be large; allow a longer idle timeout but still skip retry.
        /// </summary>
        public static void ConfigureImageDownload(WebRequest request)
        {
            request.Timeout = PixivConstants.IMAGE_REQUEST_TIMEOUT_MS;
            request.AllowRetryOnTimeout = false;
        }
    }
}
