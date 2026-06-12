// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame
{
    public static class EzModifyPath
    {
        // Ez-Stage 的默认判定线高度
        public const float EZ_STAGE_BODY_HEIGHT = 247f;

        public const string RESOURCES_PATH = @"EzResources";
        public const string NOTE_PATH = @"EzResources/note";
        public const string STAGE_PATH = @"EzResources/Stage";
        public const string GAME_THEME_PATH = @"EzResources/GameTheme";
        public const string FULL_COMBO = @"EzResources/Modify/FullCombo";

        public const string VIDEO_PATH = @"EzResources/Video";
        public const string BG_PATH = @"EzResources/BG";
        public const string BG_PIXIV_PATH = @"EzResources/BG_PIXIV";

        /// <summary>
        /// Pixiv OAuth refresh token (same directory as client.realm / framework.ini).
        /// </summary>
        public const string PIXIV_AUTH_FILE = @"pixiv_auth.json";
    }
}
