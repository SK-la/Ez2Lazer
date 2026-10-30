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
        public const string PANEL_PATH = @"EzResources/Panel";
        public const string COLUMN_PATH = @"EzResources/ColumnLight";
        public const string GAME_THEME_PATH = @"EzResources/GameTheme";

        /// <summary>
        /// 舞台判定线贴图，文件名形如 <c>JudgementLine_{name}.png</c>，不带后缀的 <c>{name}.png</c> 也接受。
        /// </summary>
        public const string JUDGEMENT_LINE_PATH = @"EzResources/JudgementLine";
        public const string FULL_COMBO = @"EzResources/Modify/FullCombo";

        public const string VIDEO_PATH = @"EzResources/Video";
        public const string BG_PATH = @"EzResources/BG";
        public const string BG_PIXIV_PATH = @"EzResources/BG_PIXIV";
        public const string CONFIG_LAYOUT_PATH = @"EzResources/Config/layout";

        /// <summary>
        /// Root directory holding one layout set. Multiple sets (for example <c>layout</c>,
        /// <c>layout-O2jam</c>) live side by side under <see cref="CONFIG_ROOT_PATH"/> and can be
        /// switched as a whole later.
        /// </summary>
        public const string CONFIG_ROOT_PATH = @"EzResources/Config";

        /// <summary>Default layout set name; resolves to <see cref="CONFIG_LAYOUT_PATH"/>.</summary>
        public const string LAYOUT_SET_DEFAULT = @"layout";

        /// <summary>
        /// Directory of a named layout set, e.g. <c>EzResources/Config/layout-O2jam</c>.
        /// </summary>
        public static string LayoutSetPath(string layoutSetName) => $"{CONFIG_ROOT_PATH}/{layoutSetName}";

        public const string PETS_PATH = @"EzResources/Pets";

        /// <summary>
        /// Local custom avatars: <c>{Username}.png</c> or <c>{Username}/{clip}/000.png</c>.
        /// </summary>
        public const string AVATARS_PATH = @"EzResources/Modify/avatars";

        /// <summary>
        /// Pixiv OAuth refresh token (same directory as client.realm / framework.ini).
        /// </summary>
        public const string PIXIV_AUTH_FILE = @"pixiv_auth.json";
    }
}
