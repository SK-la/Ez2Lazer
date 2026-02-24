// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.LAsEzExtensions.Localization
{
    public static class EzCommonModStrings
    {
        public static readonly LocalisableString APPLY_ORDER_LABEL = new EzLocalizationManager.EzLocalisableString("应用顺序", "Apply Order");

        public static readonly LocalisableString APPLY_ORDER_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "此mod在转换过程中应用的顺序。数字越小越先运行。",
            "The order this mod is applied during transformation. The smaller the number, the earlier it runs.");

        public static readonly LocalisableString SEED_LABEL = new EzLocalizationManager.EzLocalisableString("随机种子", "Random Seed");

        public static readonly LocalisableString SEED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "指定自定义种子后，会按这个种子进行可复现的固定随机",
            "With a custom seed specified, a fixed random with this seed will be applied.");

        public static readonly LocalisableString SPEED_CHANGE_LABEL = new EzLocalizationManager.EzLocalisableString("改变倍速", "Speed Change");

        public static readonly LocalisableString SPEED_CHANGE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "改变倍速。不允许叠加其他变速mod。",
            "Speed Change. The actual decrease to apply. Don't add other rate-mod.");

        public static readonly LocalisableString ADJUST_PITCH_LABEL = new EzLocalizationManager.EzLocalisableString("调整音调", "Adjust pitch");

        public static readonly LocalisableString ADJUST_PITCH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "速度改变时是否调整音调。（变速又变调）",
            "Whether to adjust pitch when changing speed. (Pitch changes with rate)");

        public static readonly LocalisableString MIRROR_LABEL = new EzLocalizationManager.EzLocalisableString("镜像", "Mirror");
        public static readonly LocalisableString MIRROR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("左右镜像处理", "Mirror the beatmap horizontally.");

        public static readonly LocalisableString KEY_LABEL = new EzLocalizationManager.EzLocalisableString("按键数", "Key");
        public static readonly LocalisableString KEY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("目标按键数（只能从低按键数转换为高按键数）", "Key. To Keys(Can only convert lower keys to higher keys.)");

        public static readonly LocalisableString PROBABILITY_LABEL = new EzLocalizationManager.EzLocalisableString("概率", "Probability");
        public static readonly LocalisableString PROBABILITY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("转换概率", "Probability. The Probability of convertion.");
        public static readonly LocalisableString INTERVAL_LABEL = new EzLocalizationManager.EzLocalisableString("间隔", "Interval");
        public static readonly LocalisableString INTERVAL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("音符的最小间隔（防止重叠）", "Interval. The minimum interval of note(To prevent overlap).");

        public static readonly LocalisableString PERCENTAGE_LABEL = new EzLocalizationManager.EzLocalisableString("百分比", "Percentage");
        public static readonly LocalisableString PERCENTAGE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("LN内容", "LN Content");
        public static readonly LocalisableString ORIGINAL_LN_LABEL = new EzLocalizationManager.EzLocalisableString("原始LN", "Original LN");
        public static readonly LocalisableString ORIGINAL_LN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("原始LN不会被转换", "Original LN won't be converted.");
        public static readonly LocalisableString COLUMN_NUM_LABEL = new EzLocalizationManager.EzLocalisableString("列数", "Column Num");
        public static readonly LocalisableString COLUMN_NUM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择要转换的列数", "Select the number of column to transform.");
        public static readonly LocalisableString GAP_LABEL = new EzLocalizationManager.EzLocalisableString("间隙", "Gap");
        public static readonly LocalisableString GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("转换后改变随机列的音符数量间隙", "For changing random columns after transforming the gap's number of notes.");
        public static readonly LocalisableString LINE_SPACING_LABEL = new EzLocalizationManager.EzLocalisableString("行间距", "Line Spacing");
        public static readonly LocalisableString LINE_SPACING_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("设置为0时转换每一行", "Transform every line when set to 0.");
        public static readonly LocalisableString INVERT_LINE_SPACING_LABEL = new EzLocalizationManager.EzLocalisableString("反转行间距", "Invert Line Spacing");
        public static readonly LocalisableString INVERT_LINE_SPACING_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("反转行间距", "Invert the Line Spacing.");
        public static readonly LocalisableString DURATION_LIMIT_LABEL = new EzLocalizationManager.EzLocalisableString("持续时间限制", "Duration Limit");
        public static readonly LocalisableString DURATION_LIMIT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("LN的最大持续时间（秒）。（设置为0时无限制）", "The max duration(second) of a LN.(No limit when set to 0)");
    }
}
