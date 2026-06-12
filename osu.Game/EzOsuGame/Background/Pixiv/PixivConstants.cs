// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivConstants
    {
        public const string CLIENT_ID = "MOBrBDS8blbauo1uch9Z4AXbbf";
        public const string CLIENT_SECRET = "ttIDt8NdJJMxTCWRMTtPArt";

        public const string AUTH_TOKEN_URL = "https://oauth.secure.pixiv.net/auth/token";
        public const string API_BASE_URL = "https://app-api.pixiv.net";
        public const string IMAGE_REFERER = "https://www.pixiv.net/";

        public const string USER_AGENT = "PixivAndroidApp/5.0.234 (Android 11; Pixel 5)";
        public const string APP_OS = "android";
        public const string APP_OS_VERSION = "11";
        public const string APP_VERSION = "5.0.234";

        public const double AUTO_DOWNLOAD_INTERVAL_MS = 3 * 60 * 1000;

        /// <summary>
        /// Pixiv API: 1 = AI-generated work.
        /// </summary>
        public const int ILLUST_AI_TYPE_AI = 1;

        public static readonly string[] AI_GENERATED_TAG_PATTERNS =
        {
            "AI生成",
            "AI Generated",
            "Stable Diffusion",
            "StableDiffusion",
            "novelAI",
            "NovelAI",
            "Midjourney",
            "DALL·E",
            "DALL-E",
            "生成AI",
        };
    }
}
