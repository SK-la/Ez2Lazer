// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzHUDStrings
    {
        public static readonly LocalisableString RADAR_BASE_LINE_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达底板线色", "Radar Base Line Colour");

        public static readonly LocalisableString RADAR_BASE_LINE_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("底板网格线和轴线的颜色", "Colour of base grid and axis lines.");

        public static readonly LocalisableString RADAR_BASE_AREA_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达底板区色", "Radar Base Area Colour");

        public static readonly LocalisableString RADAR_BASE_AREA_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("底板填充区域的颜色", "Colour of base filled area.");

        public static readonly LocalisableString RADAR_DATA_LINE_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达数据线色", "Radar Data Line Colour");

        public static readonly LocalisableString RADAR_DATA_LINE_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("数据轮廓线和顶点标记的颜色", "Colour of data outline and point markers.");

        public static readonly LocalisableString RADAR_DATA_AREA_COLOUR = new EzLocalizationManager.EzLocalisableString("雷达数据区色", "Radar Data Area Colour");

        public static readonly LocalisableString RADAR_DATA_AREA_COLOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString("数据填充区域的颜色", "Colour of data filled area.");

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
    }
}
