// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Localization;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Localization
{
    public static class EzManiaSettingsStrings
    {
        public static readonly LocalisableString HIT_MODE = new EzLocalizationManager.EzLocalisableString("Mania 判定系统", "(Mania) Hit Mode");

        public static readonly LocalisableString HIT_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Mania 判定系统, 获得不同音游的打击体验, 但是不保证所有模式都完全一比一复刻"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\n 305     300      200      100     50     Miss    Poor"
            + "\n16.67    33.33    116.67      -      250    250    500    IIDX"
            + "\n15.00   30.00   60.00      -     200    1000  1000   LR2 Hard"
            + "\n15.00   45.00    112.00     -      165     500    500   Raja Normal"
            + "\n20.00  60.00   150.00    -      500    500    500   Raja Easy",
            "(Mania) Hit Mode, get different rhythm game hit experiences, but not guaranteed to perfectly replicate all modes"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\n 305     300      200      100     50     Miss    Poor"
            + "\n16.67    33.33    116.67      -      250    250    500    IIDX"
            + "\n15.00   30.00   60.00      -     200    1000  1000   LR2 Hard"
            + "\n15.00   45.00    112.00     -      165     500    500   Raja Normal"
            + "\n20.00  60.00   150.00    -      500    500    500   Raja Easy");

        public static readonly LocalisableString HEALTH_MODE = new EzLocalizationManager.EzLocalisableString("Mania 血量系统", "(Mania) Health Mode");

        public static readonly LocalisableString HEALTH_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "\n——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\n 305    300    200   100    50   Miss       -"
            + "\n0.4%   0.3%   0.1%    0%   -1%   - 6%     -0%  Lazer"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\nKool        -   Good       -   Bad   Miss         -"
            + "\n0.3%   0.0%   0.2%    0%   -1%   - 5%     -0%  O2 Easy"
            + "\n0.2%   0.0%   0.1%    0%   -7%   - 4%     -0%  O2 Normal"
            + "\n0.1%   0.0%   0.0%    0%   -5%   - 3%     -0%  O2 Hard"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\nKool   Cool   Good      -   Bad   Poor  []Poor"
            + "\n0.4%   0.3%    0.1%    0%   -1%   - 5%      -5%  Ez2Ac"
            + "\n1.6%   1.6%    0.0%    0%   -5%   - 9%      -5%  IIDX Hard"
            + "\n1.0%   1.0%    0.5%    0%   -6%   -10%      -2%  LR2 Hard"
            + "\n1.2%   1.2%    0.6%    0%   -3%   - 6%      -2%  raja normal");

        public static readonly LocalisableString POOR_HIT_RESULT = new EzLocalizationManager.EzLocalisableString("增加 Poor 判定类型", "Additional Poor HitResult");

        public static readonly LocalisableString POOR_HIT_RESULT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Pool判定类型只在BMS系血量系统下生效, 用于严格扣血, 不影响Combo、Score\n"
            + "一个note可触发多个Pool判定, 只有早于Miss时才会触发, 不存在晚Pool",
            "The Poor HitResult type only takes effect under the BMS Health Mode, used for strict health deduction, does not affect Combo or Score\n"
            + "One note can trigger multiple Poor hit results, and it will only trigger if it is earlier than Miss, there is no late Poor");

        public static readonly LocalisableString MANIA_BAR_LINES_BOOL = new EzLocalizationManager.EzLocalisableString("启用强制显示小节线", "Force Display Bar Lines");

        public static readonly LocalisableString MANIA_BAR_LINES_BOOL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "强制显示Mania小节线功能的开关, 关闭后仅由皮肤控制", "Toggle to force display of bar lines, when off only controlled by skin");

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
