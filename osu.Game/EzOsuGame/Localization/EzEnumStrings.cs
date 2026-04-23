// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    /// <summary>
    /// 提供 LocalisableString 本地化
    /// </summary>
    public static class EzEnumStrings
    {
        public static readonly LocalisableString EZ_STYLE_PRO_ONLY = new EzLocalizationManager.EzLocalisableString("Ez 内置皮肤专用", "Ez Skin Only");
        public static readonly LocalisableString GLOBAL_WIDTH = new EzLocalizationManager.EzLocalisableString("全局宽度", "Global Width");
        public static readonly LocalisableString GLOBAL_TOTAL_WIDTH = new EzLocalizationManager.EzLocalisableString("全局总宽度", "Global Total Width");

        public static readonly LocalisableString SCALE = new EzLocalizationManager.EzLocalisableString("缩放动效", "Scale");
        public static readonly LocalisableString BOUNCE = new EzLocalizationManager.EzLocalisableString("跳跃动效", "Bounce");

        public static readonly LocalisableString AUTO_PREVIEW = new EzLocalizationManager.EzLocalisableString("全K音预览", "Auto Preview");
        public static readonly LocalisableString AUTO_PLAY_PLUS = new EzLocalizationManager.EzLocalisableString("全K音预览+游戏内自动播放", "Auto Preview + In-game Auto Play");

        public static readonly LocalisableString STATIC = new EzLocalizationManager.EzLocalisableString("静态预览", "Static Preview");
        public static readonly LocalisableString DYNAMIC = new EzLocalizationManager.EzLocalisableString("动态预览", "Dynamic Preview");
        public static readonly LocalisableString STATIC_FULL_MAP = new EzLocalizationManager.EzLocalisableString("全图预览", "Full-map Preview");
        public static readonly LocalisableString STATIC_SCROLL = new EzLocalizationManager.EzLocalisableString("卷轴预览", "Scroll Preview");
    }
}
