// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivConstants
    {
        public const string CLIENT_ID = "MOBrBDS8blbauoSck0ZfDbtuzpyT";
        public const string CLIENT_SECRET = "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj";
        public const string CLIENT_HASH_SECRET = "28c1fdd170a5204386cb1313c7077b34f83e4aaf4aa829ce78c231e05b0bae2c";

        public const string AUTH_TOKEN_URL = "https://oauth.secure.pixiv.net/auth/token";
        public const string API_BASE_URL = "https://app-api.pixiv.net";
        public const string ILLUST_FOLLOW_INITIAL_URL = $"{API_BASE_URL}/v2/illust/follow?restrict=public";
        public const string API_REFERER = "https://app-api.pixiv.net/";
        public const string IMAGE_REFERER = "https://www.pixiv.net/";

        public const string USER_AGENT = "PixivAndroidApp/5.0.234 (Android 11; Pixel 5)";
        public const string APP_OS = "android";
        public const string APP_OS_VERSION = "11";
        public const string APP_VERSION = "5.0.234";

        public const int FOLLOW_FEED_MIN_CATALOG_SIZE = 10;

        /// <summary>
        /// Maximum follow-feed pages fetched in one catalog fill burst.
        /// </summary>
        public const int FOLLOW_FEED_MAX_PAGES_PER_BUILD = 8;

        public const double AUTO_PREFETCH_INTERVAL_MS = 60 * 1000;

        /// <summary>
        /// Pixiv API illust_ai_type: 0 = unspecified, 1 = human-created, 2 = AI-generated.
        /// </summary>
        public const int ILLUST_AI_TYPE_UNSPECIFIED = 0;

        public const int ILLUST_AI_TYPE_HUMAN = 1;
        public const int ILLUST_AI_TYPE_AI = 2;

        /// <summary>
        /// Official / community AI disclosure tags (exact match). Used when illust_ai_type is unknown.
        /// </summary>
        public static readonly string[] AI_GENERATED_TAGS =
        {
            "AI生成",
            "AI-generated",
            "AIイラスト",
            "AI生成作品",
            "AI 画作",
            "AI生成イラスト",
            "AI 생성",
            "сгенерированный ИИ",
            "สร้างโดย AI",
            "Janaan AI",
        };
    }
}
