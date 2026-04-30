// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.Rulesets.Mania.EzMania.Localization
{
    public static class EzHUDManiaStrings
    {
        // EzHUDComboCounter & EzHUDComboTitle
        public static readonly LocalisableString FONT_LABEL = new EzLocalizationManager.EzLocalisableString("字体", "Font");
        public static readonly LocalisableString FONT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("加载来自EzResources的自定义资源", "Load custom resources from EzResources.");

        public static readonly LocalisableString EFFECT_TYPE_LABEL = new EzLocalizationManager.EzLocalisableString("动画效果", "Animation Effect");
        public static readonly LocalisableString EFFECT_TYPE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择动画效果类型", "Select animation effect type.");

        public static readonly LocalisableString EFFECT_ORIGIN_LABEL = new EzLocalizationManager.EzLocalisableString("动效原点", "Effect Origin");
        public static readonly LocalisableString EFFECT_ORIGIN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("设置动画效果的原点位置", "Set the origin position of the animation effect.");

        public static readonly LocalisableString EFFECT_START_FACTOR_LABEL = new EzLocalizationManager.EzLocalisableString("动效起始系数", "Effect Start Factor");
        public static readonly LocalisableString EFFECT_START_FACTOR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动画开始时的缩放或位移系数", "Scaling or displacement factor at animation start.");

        public static readonly LocalisableString EFFECT_END_FACTOR_LABEL = new EzLocalizationManager.EzLocalisableString("动效结束系数", "Effect End Factor");
        public static readonly LocalisableString EFFECT_END_FACTOR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动画结束时的缩放或位移系数", "Scaling or displacement factor at animation end.");

        public static readonly LocalisableString EFFECT_START_DURATION_LABEL = new EzLocalizationManager.EzLocalisableString("动效起始时长", "Effect Start Duration");
        public static readonly LocalisableString EFFECT_START_DURATION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动画开始阶段的持续时间（毫秒）", "Duration of the animation start phase (milliseconds).");

        public static readonly LocalisableString EFFECT_END_DURATION_LABEL = new EzLocalizationManager.EzLocalisableString("动效结束时长", "Effect End Duration");
        public static readonly LocalisableString EFFECT_END_DURATION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动画结束阶段的持续时间（毫秒）", "Duration of the animation end phase (milliseconds).");

        // EzHUDHitTiming
        public static readonly LocalisableString OFFSET_NUMBER_FONT_LABEL = new EzLocalizationManager.EzLocalisableString("偏移数字字体", "Offset Number Font");
        public static readonly LocalisableString OFFSET_NUMBER_FONT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("设置偏移数值的字体样式", "Set the font style for offset numbers.");

        public static readonly LocalisableString OFFSET_TEXT_FONT_LABEL = new EzLocalizationManager.EzLocalisableString("偏移文本字体", "Offset Text Font");
        public static readonly LocalisableString OFFSET_TEXT_FONT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("设置偏移文本的字体样式", "Set the font style for offset text.");

        public static readonly LocalisableString SINGLE_SHOW_EL_LABEL = new EzLocalizationManager.EzLocalisableString("单独显示E/L", "Single Show E/L");
        public static readonly LocalisableString SINGLE_SHOW_EL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("仅单独显示Early或Late", "Show only Early or Late separately.");

        public static readonly LocalisableString DISPLAYING_THRESHOLD_LABEL = new EzLocalizationManager.EzLocalisableString("显示阈值", "Displaying Threshold");
        public static readonly LocalisableString DISPLAYING_THRESHOLD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("小于此阈值的判定将不显示", "Judgments below this threshold will not be displayed.");

        public static readonly LocalisableString DISPLAY_DURATION_LABEL = new EzLocalizationManager.EzLocalisableString("持续时间", "Display Duration");
        public static readonly LocalisableString DISPLAY_DURATION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("判定显示的持续时间（毫秒）", "Duration before the display disappears (milliseconds).");

        public static readonly LocalisableString SYMMETRY_OFFSET_LABEL = new EzLocalizationManager.EzLocalisableString("对称间距", "Symmetrical Spacing");
        public static readonly LocalisableString SYMMETRY_OFFSET_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("左右文本之间的对称间距", "Symmetrical spacing between left and right text.");

        public static readonly LocalisableString TEXT_ALPHA_LABEL = new EzLocalizationManager.EzLocalisableString("文本透明度", "Text Alpha");
        public static readonly LocalisableString TEXT_ALPHA_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("偏移文本的透明度值", "The alpha value of the offset text.");

        public static readonly LocalisableString NUMBER_ALPHA_LABEL = new EzLocalizationManager.EzLocalisableString("数字透明度", "Number Alpha");
        public static readonly LocalisableString NUMBER_ALPHA_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("偏移数字的透明度值", "The alpha value of the offset number.");

        // EzComHitTimingColumns
        public static readonly LocalisableString MINIMUM_HIT_RESULT_LABEL = new EzLocalizationManager.EzLocalisableString("最小判定等级", "Minimum Hit Result");
        public static readonly LocalisableString MINIMUM_HIT_RESULT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("过滤掉低于此等级的判定", "Filter out judgments worse than this.");

        public static readonly LocalisableString MARKERS_HEIGHT_LABEL = new EzLocalizationManager.EzLocalisableString("标记高度", "Markers Height");
        public static readonly LocalisableString MARKERS_HEIGHT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("判定标记的高度", "Height of the judgement markers.");

        public static readonly LocalisableString MOVE_HEIGHT_LABEL = new EzLocalizationManager.EzLocalisableString("移动高度", "Move Height");
        public static readonly LocalisableString MOVE_HEIGHT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("标记移动的垂直范围", "Vertical range for marker movement.");

        public static readonly LocalisableString BACKGROUND_ALPHA_LABEL = new EzLocalizationManager.EzLocalisableString("背景透明度", "Background Alpha");
        public static readonly LocalisableString BACKGROUND_ALPHA_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("背景框的透明度", "Alpha value of the background box.");

        public static readonly LocalisableString BACKGROUND_COLOUR_LABEL = new EzLocalizationManager.EzLocalisableString("背景颜色", "Background Colour");
        public static readonly LocalisableString BACKGROUND_COLOUR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("背景框的颜色", "Colour of the background box.");
    }
}
