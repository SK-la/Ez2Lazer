// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public partial class YuComFastSlowDisplay
    {
        public enum YuColumnPosition
        {
            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.NONE))]
            None,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.LEFT_HALF))]
            LeftHalf,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.RIGHT_HALF))]
            RightHalf,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.MIDDLE))]
            Middle
        }

        public enum YuColourStyle
        {
            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SINGLE_COLOUR))]
            SingleColour,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.HORIZONTAL_GRADIENT))]
            HorizontalGradient,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.VERTICAL_GRADIENT))]
            VerticalGradient
        }

        protected static class FastSlowDisplayStrings
        {
            public static readonly LocalisableString SHOW_JUDGEMENT = new EzLocalizationManager.EzLocalisableString("判定", "Judgement");
            public static readonly LocalisableString SHOW_STYLE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("何时显示快/慢", "When to show fast/slow.");

            public static readonly LocalisableString GAP = new EzLocalizationManager.EzLocalisableString("间隙", "Gap");
            public static readonly LocalisableString GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("快和慢之间的间隙", "The gap between fast and slow.");

            public static readonly LocalisableString FADE_DURATION = new EzLocalizationManager.EzLocalisableString("淡出持续时间", "Fade Duration");
            public static readonly LocalisableString FADE_DURATION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("淡出效果的持续时间", "The duration of the fade out effect.");

            public static readonly LocalisableString FONT_SIZE = new EzLocalizationManager.EzLocalisableString("字体大小", "Font Size");
            public static readonly LocalisableString FONT_SIZE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("文本的大小", "The size of the text.");

            public static readonly LocalisableString FAST_TEXT = new EzLocalizationManager.EzLocalisableString("快文本", "Fast Text");
            public static readonly LocalisableString SLOW_TEXT = new EzLocalizationManager.EzLocalisableString("慢文本", "Slow Text");
            public static readonly LocalisableString TEXT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("要显示的文本", "The text to be displayed.");

            public static readonly LocalisableString FAST_COLOUR_STYLE = new EzLocalizationManager.EzLocalisableString("快颜色风格", "Fast Colour Style");
            public static readonly LocalisableString FAST_COLOUR_STYLE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("快颜色的风格", "The style of the fast colour.");

            public static readonly LocalisableString FAST_COLOUR = new EzLocalizationManager.EzLocalisableString("快颜色", "Fast Colour");
            public static readonly LocalisableString TEXT_COLOUR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("文本的颜色", "The colour of the text.");

            public static readonly LocalisableString SLOW_COLOUR_STYLE = new EzLocalizationManager.EzLocalisableString("慢颜色风格", "Slow Colour Style");
            public static readonly LocalisableString SLOW_COLOUR_STYLE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("慢颜色的风格", "The style of the slow colour.");

            public static readonly LocalisableString SLOW_COLOUR = new EzLocalizationManager.EzLocalisableString("慢颜色", "Slow Colour");

            public static readonly LocalisableString DISPLAY_STYLE = new EzLocalizationManager.EzLocalisableString("水平/垂直", "Horizontal / Vertical");
            public static readonly LocalisableString DISPLAY_STYLE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("水平或垂直显示文本", "Display the text horizontally or vertically.");

            public static readonly LocalisableString LOWER_COLUMN = new EzLocalizationManager.EzLocalisableString("列下界", "Lower Column Bound");
            public static readonly LocalisableString LOWER_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("显示文本的列下界", "The lower bound of the column to display the text.");

            public static readonly LocalisableString UPPER_COLUMN = new EzLocalizationManager.EzLocalisableString("列上界", "Upper Column Bound");
            public static readonly LocalisableString UPPER_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("显示文本的列上界", "The upper bound of the column to display the text.");

            public static readonly LocalisableString ONLY_DISPLAY_ONE = new EzLocalizationManager.EzLocalisableString("仅显示一个", "Only Display One");
            public static readonly LocalisableString ONLY_DISPLAY_ONE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("一次只显示一个文本", "Display only one text at a time.");

            public static readonly LocalisableString SELECT_COLUMN = new EzLocalizationManager.EzLocalisableString("选择列", "Select Column");
            public static readonly LocalisableString SELECT_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择要显示文本的列", "Select the column to display the text.");

            public static readonly LocalisableString NONE = new EzLocalizationManager.EzLocalisableString("无", "None");
            public static readonly LocalisableString LEFT_HALF = new EzLocalizationManager.EzLocalisableString("左半", "Left Half");
            public static readonly LocalisableString RIGHT_HALF = new EzLocalizationManager.EzLocalisableString("右半", "Right Half");
            public static readonly LocalisableString MIDDLE = new EzLocalizationManager.EzLocalisableString("中间", "Middle");

            public static readonly LocalisableString SINGLE_COLOUR = new EzLocalizationManager.EzLocalisableString("单色", "Single Colour");
            public static readonly LocalisableString HORIZONTAL_GRADIENT = new EzLocalizationManager.EzLocalisableString("水平渐变", "Horizontal Gradient");
            public static readonly LocalisableString VERTICAL_GRADIENT = new EzLocalizationManager.EzLocalisableString("垂直渐变", "Vertical Gradient");

            public static readonly LocalisableString LN_SWITCH = new EzLocalizationManager.EzLocalisableString("长条切换", "LN Switch");
            public static readonly LocalisableString LN_SWITCH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("单独显示长条尾部", "Display LN tail individually.");

            public static readonly LocalisableString FAST_TEXT_LN = new EzLocalizationManager.EzLocalisableString("快文本长条", "Fast Text LN");
            public static readonly LocalisableString SLOW_TEXT_LN = new EzLocalizationManager.EzLocalisableString("慢文本长条", "Slow Text LN");

            public static readonly LocalisableString TEST = new EzLocalizationManager.EzLocalisableString("测试", "Test");
            public static readonly LocalisableString TEST_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("预览快/慢的显示", "Preview the display of fast/slow.");
        }
    }
}
