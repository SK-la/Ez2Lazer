// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.Rulesets.Mania.EzMania.Localization
{
    public static class EzManiaSettingsStrings
    {
        public static readonly LocalisableString MANIA_BAR_LINES_BOOL = new EzLocalizationManager.EzLocalisableString("启用强制显示小节线", "Force Display Bar Lines");

        public static readonly LocalisableString MANIA_BAR_LINES_BOOL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "强制显示Mania小节线功能的开关, 关闭后仅由皮肤控制", "Toggle to force display of bar lines, when off only controlled by skin");

        public static readonly LocalisableString TIMING_BASED_NOTE_TARGET_GRAYSCALE = new EzLocalizationManager.EzLocalisableString(
            "Timing着色目标灰度亮度", "Timing-based note target grayscale");

        public static readonly LocalisableString TIMING_BASED_NOTE_TARGET_GRAYSCALE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "应用Timing着色前使用的灰度基底亮度。", "Brightness of the grayscale base used before applying timing-based note colours.");

        public static readonly LocalisableString TIMING_BASED_NOTE_COLOUR_ALPHA = new EzLocalizationManager.EzLocalisableString(
            "Timing着色颜色强度", "Timing-based note colour alpha");

        public static readonly LocalisableString TIMING_BASED_NOTE_COLOUR_ALPHA_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "混入灰度note基底的Timing颜色强度。", "Strength of the timing colour mixed into the grayscale note base.");

        public static readonly LocalisableString SCROLLING_STYLE = new EzLocalizationManager.EzLocalisableString("滚动速度风格", "Scrolling Style");

        public static readonly LocalisableString SCROLLING_STYLE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "滚动速度风格, 时间速度是note从屏幕开始的边缘到目标相对位置（具体看风格）的时间。\n"
            + "游戏中，可以在原本调速快捷键的基础上, 增加按住LAlt键，切换为5x调速，即：±1 → ±5。",
            "Scrolling style, the time speed is the time from the edge of the screen where the note starts to the target relative position (see style for details).\n"
            + "In the game, you can increase the speed adjustment to 5x by holding down the LAlt key on top of the original speed adjustment shortcut, that is: ±1 → ±5.");

        public static readonly LocalisableString SCROLL_BASE_SPEED = new EzLocalizationManager.EzLocalisableString("基础速度", "Scroll Base Speed");

        public static readonly LocalisableString SCROLL_BASE_SPEED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "在200级速度中间档位时的默认速度, 不会影响到滚动速度的等级划分。\n"
            + "在此基础上可进行 ±200级的速度微调。每级速度的变化幅度由MPS决定。\n"
            + "推荐设置在400（低K）到500（高K）之间, 可以满足大部分玩家的舒适区。",
            "The default speed at the middle level of 200-speed, does not affect the level division of scroll speed.\n"
            + "On top of this, ±200-speed fine-tuning is available. The amount of change per speed level is determined by MPS.\n"
            + "It is recommended to set it between 400 (low K) and 500 (high K) to meet the comfort zone of most players.");

        public static readonly LocalisableString SCROLL_TIME_PER_SPEED = new EzLocalizationManager.EzLocalisableString("每级速度的变化幅度", "Time Change Per Speed");

        public static readonly LocalisableString SCROLL_TIME_PER_SPEED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "每级速度的变化幅度, 以ms为单位, 代表每变化1级速度时滚动时间的变化量。\n"
            + "例如设置为5ms时, 在游戏中每加减一次速度档位，滚动速度变化5ms。\n"
            + "推荐设置在5ms到20ms之间, 过高或过低的数值可能会导致在游戏中难以及时的调整速度到舒适区。",
            "The amount of change per speed level, in ms, represents the amount of change in scroll time when changing 1 speed level.\n"
            + "For example, when set to 5ms, every time you increase or decrease the speed level in the game, the scroll speed changes by 5ms.\n"
            + "It is recommended to set it between 5ms and 20ms, as values that are too high or too low may make it difficult to adjust the speed to a comfortable range in time during the game.");
    }
}
