// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class DynamicSpeedHUDStrings
    {
        public static readonly LocalisableString LINK_SPEED_HUD_LABEL = new EzLocalizationManager.EzLocalisableString("联动速度 HUD", "Link Speed HUD");

        public static readonly LocalisableString LINK_SPEED_HUD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "开启后在游玩界面显示动态变速 HUD。",
            "Show a dynamic speed HUD overlay during gameplay.");

        public static readonly LocalisableString SHOW_SPEED_TEXT_LABEL = new EzLocalizationManager.EzLocalisableString("显示速度文字", "Show Speed Text");

        public static readonly LocalisableString SHOW_SPEED_TEXT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "在 HUD 中显示当前速度倍率文字。",
            "Display the current speed multiplier as text in the HUD.");

        public static readonly LocalisableString SHOW_SPEED_LINE_LABEL = new EzLocalizationManager.EzLocalisableString("显示动态速度线", "Show Dynamic Speed Line");

        public static readonly LocalisableString SHOW_SPEED_LINE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "在速度文字右侧绘制向右滚动的速度折线图。",
            "Draw a scrolling speed line chart to the right of the speed text.");
    }
}
