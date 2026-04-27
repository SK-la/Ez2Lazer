// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzHUDStrings
    {
        public static readonly LocalisableString TEST_MODE_LABEL = new EzLocalizationManager.EzLocalisableString("测试模式", "Test Mode");
        public static readonly LocalisableString TEST_MODE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("强制显示内容，用于测试", "Force display content for testing.");

        public static readonly LocalisableString RADAR_BASE_LINE_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达底板线色", "Radar Base Line Colour");
        public static readonly LocalisableString RADAR_BASE_LINE_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("底板网格线和轴线的颜色", "Colour of base grid and axis lines.");

        public static readonly LocalisableString RADAR_BASE_AREA_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达底板区色", "Radar Base Area Colour");
        public static readonly LocalisableString RADAR_BASE_AREA_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("底板填充区域的颜色", "Colour of base filled area.");

        public static readonly LocalisableString RADAR_DATA_LINE_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达数据线色", "Radar Data Line Colour");
        public static readonly LocalisableString RADAR_DATA_LINE_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("数据轮廓线和顶点标记的颜色", "Colour of data outline and point markers.");

        public static readonly LocalisableString RADAR_DATA_AREA_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达数据区色", "Radar Data Area Colour");
        public static readonly LocalisableString RADAR_DATA_AREA_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("数据填充区域的颜色", "Colour of data filled area.");

        public static readonly LocalisableString BACKGROUND_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达背景色", "Radar Background Colour");
        public static readonly LocalisableString RADAR_BOX_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("雷达图圆角背景的颜色，设置为透明可隐藏背景", "Colour of radar chart rounded background. Set to transparent to hide background.");

        public static readonly LocalisableString RADAR_LABEL_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达标签颜色", "Radar Label Colour");
        public static readonly LocalisableString RADAR_LABEL_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("雷达图轴标签文字的颜色", "Colour of radar chart axis label text.");

        public static readonly LocalisableString RADAR_DISPLAY_MODE = new EzLocalizationManager.EzLocalisableString("雷达显示模式", "Radar Display Mode");

        public static readonly LocalisableString RADAR_DISPLAY_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "切换不同数据源的显示模式："
            + "\n- 全局：显示常规Metadate数据。"
            + "\n- Key Pattern: 显示类PS Mod风格的并行键型数据。衡量谱面中不同键型的相对难度关系。"
            + "\n- xxySR Pattern: 显示xxySR星级分析的键型数据。衡量谱面中不同键型变化的难度系数，提现谱中键型变化差异程度，这里的bracket除了切指外还视为常规类型。",
            "Switch between different data sources to display:"
            + "\n- Global: Display the standard Metadata data."
            + "\n- Key Pattern: Display the parallel key type data of the PS Mod style."
            + "\n- xxySR Pattern: Display the key type data of the xxySR star rating analysis."
            + "\n- xxySR Pattern: Display the key type data of the xxySR star rating analysis.");

        public static readonly LocalisableString RADAR_USE_ABSOLUTE_VALUE = new EzLocalizationManager.EzLocalisableString("使用星数绝对值", "Use Star Absolute Value");

        public static readonly LocalisableString RADAR_USE_ABSOLUTE_VALUE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，会把指标结果视为比例，乘上xxySR，变成星级难度指标。"
            + "\n注意这样并不可靠，只是提供一种视觉策略。",
            "When enabled, the result of each metric will be treated as a ratio and multiplied by xxySR to become a star rating metric."
            + "\nNote that this is not reliable and just provides a visual strategy.");

        // 通用设置（所有模式共享）
        public static readonly LocalisableString ALPHA_LABEL = new EzLocalizationManager.EzLocalisableString("透明度", "Alpha");
        public static readonly LocalisableString ALPHA_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("组件的透明度值", "The alpha value of this component.");

        // EzComO2JamPillUI
        public static readonly LocalisableString PILL_SPRITE_LABEL = new EzLocalizationManager.EzLocalisableString("药丸图标", "Pill Sprite");
        public static readonly LocalisableString PILL_SPRITE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择药丸图标的样式", "Select the pill sprite style.");

        public static readonly LocalisableString PILL_DIRECTION_LABEL = new EzLocalizationManager.EzLocalisableString("药丸方向", "Pill Direction");
        public static readonly LocalisableString PILL_DIRECTION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择药丸排列的方向", "Select the pill arrangement direction.");

        public static readonly LocalisableString BACKGROUND_ALPHA_LABEL = new EzLocalizationManager.EzLocalisableString("背景透明度", "Box Element Alpha");
        public static readonly LocalisableString BOX_ELEMENT_ALPHA_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("背景框的透明度值", "The alpha value of background.");

        // EzHUDAccuracyCounter
        public static readonly LocalisableString FILL_DIRECTION_LABEL = new EzLocalizationManager.EzLocalisableString("排列方向", "Fill Direction");
        public static readonly LocalisableString FILL_DIRECTION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择组件的排列方向", "Select the arrangement direction of components.");

        public static readonly LocalisableString ACCURACY_DISPLAY_MODE_LABEL = new EzLocalizationManager.EzLocalisableString("准确率显示模式", "Accuracy Display Mode");
        public static readonly LocalisableString ACCURACY_DISPLAY_MODE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择准确率的显示方式", "Select how accuracy is displayed.");

        // EzComHitResultScore
        public static readonly LocalisableString HITRESULT_TEXT_FONT_LABEL = new EzLocalizationManager.EzLocalisableString("判定文本字体", "HitResult Text Font");
        public static readonly LocalisableString HITRESULT_TEXT_FONT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择判定文本的字体样式", "Select the font style for hit result text.");

        public static readonly LocalisableString PLAYBACK_FPS_LABEL = new EzLocalizationManager.EzLocalisableString("播放帧率", "Playback FPS");
        public static readonly LocalisableString PLAYBACK_FPS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动画的帧率值", "The FPS value of this animation.");

        // EzHUDScoreCounter
        public static readonly LocalisableString SCORE_FONT_LABEL = new EzLocalizationManager.EzLocalisableString("分数文本字体", "Score Font");
        public static readonly LocalisableString SCORE_FONT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择分数文本的字体样式", "Select the font style for score text.");
    }
}
