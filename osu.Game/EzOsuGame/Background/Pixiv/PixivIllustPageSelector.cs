// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json.Linq;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivIllustPageSelector
    {
        public const int display_page = 0;

        /// <summary>
        /// Multi-page works always use page 0 (p0) for menu backgrounds.
        /// </summary>
        public static bool TrySelectDisplayPage(JToken token, bool landscapeOnly, out int width, out int height)
        {
            width = 0;
            height = 0;

            (width, height) = getPageDimensions(token, display_page);

            if (width <= 0 || height <= 0)
                return false;

            if (landscapeOnly && width <= height)
                return false;

            return true;
        }

        internal static (int width, int height) getPageDimensions(JToken token, int page)
        {
            if (page == 0)
            {
                return (
                    PixivJsonHelper.IntValue(token, "width"),
                    PixivJsonHelper.IntValue(token, "height"));
            }

            var pages = PixivJsonHelper.Field(token, "meta_pages") as JArray;

            if (pages == null || page >= pages.Count)
                return (0, 0);

            JToken pageToken = pages[page];

            return (
                PixivJsonHelper.IntValue(pageToken, "width") > 0 ? PixivJsonHelper.IntValue(pageToken, "width") : PixivJsonHelper.IntValue(token, "width"),
                PixivJsonHelper.IntValue(pageToken, "height") > 0 ? PixivJsonHelper.IntValue(pageToken, "height") : PixivJsonHelper.IntValue(token, "height"));
        }
    }
}
