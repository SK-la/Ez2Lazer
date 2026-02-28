// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.LAsEzExtensions.Localization
{
    public static class EzSkinStrings
    {
        public static readonly LocalisableString GLOBAL_TEXTURE_NAME = new EzLocalizationManager.EzLocalisableString("全局纹理名称", "Global Texture Name");

        public static readonly LocalisableString GLOBAL_TEXTURE_NAME_TOOLTIP =
            new EzLocalizationManager.EzLocalisableString("(全局纹理名称)统一修改当前皮肤中所有组件的纹理名称", "Set a global texture name for all components in the current skin");

        public static readonly LocalisableString STAGE_SET = new EzLocalizationManager.EzLocalisableString("Stage套图", "Stage Set");

        public static readonly LocalisableString STAGE_SET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "统一指定主面板, 如果有动效, 则关联实时BPM。"
            + "\n支持在本地EzResources/Stage中增减子文件夹来自定义, 选项会在重载时重新读取文件夹名称。"
            + "\n子文件夹可以自己改名, 但内容文件夹及文件的名称必须完全一致。",
            "Set a stage set for Stage Bottom, related to real-time BPM"
            + "\nSupport adding or removing subfolders in the local EzResources/Stage for customization. Options will be reloaded when reloading."
            + "\nSubfolders can be renamed, but the names of content folders and files must be exactly the same.");

        public static readonly LocalisableString NOTE_SET = new EzLocalizationManager.EzLocalisableString("Note套图", "Note Set Sprite");

        public static readonly LocalisableString NOTE_SET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "统一指定整组note套图, 含note和打击光效。"
            + "\n支持在本地EzResources/Stage中增减子文件夹来自定义, 选项会在重载时重新读取文件夹名称。"
            + "\n子文件夹可以自己改名, 但内容文件夹及文件的名称必须完全一致。",
            "Set a note set for all notes and hit effects. "
            + "\nSupport adding or removing subfolders in the local EzResources/Stage for customization. Options will be reloaded when reloading."
            + "\nSubfolders can be renamed, but the names of content folders and files must be exactly the same.");

        public static readonly LocalisableString COLUMN_WIDTH_STYLE = new EzLocalizationManager.EzLocalisableString("列宽计算风格", "Column Width Calculation Style");

        public static readonly LocalisableString COLUMN_WIDTH_STYLE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "全局设置可以用在所有皮肤上。"
            + "\n全局总列宽=设置值×10, 单列宽度=key数/总列宽。"
            + "\n其他是字面意思 (功能不完善！) ",
            "Global is can be applied to all skins. "
            + "\nGlobal Total Column Width = Configured Value × 10"
            + "\nOther styles are literal meaning (functionality not perfect!)");

        public static readonly LocalisableString COLUMN_WIDTH = new EzLocalizationManager.EzLocalisableString("单轨宽度", "Column Width");
        public static readonly LocalisableString COLUMN_WIDTH_TOOLTIP = new EzLocalizationManager.EzLocalisableString("设置每列轨道的宽度", "Set the width of each column");

        public static readonly LocalisableString SPECIAL_FACTOR = new EzLocalizationManager.EzLocalisableString("特殊轨宽度倍率", "Special Column Width Factor");

        public static readonly LocalisableString SPECIAL_FACTOR_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "关联ColumnType设置, S列类型为特殊列, 以此实现两种宽度的区分。",
            "The S column type are Special columns, achieving a distinction between two widths.");

        public static readonly LocalisableString GLOBAL_HIT_POSITION = new EzLocalizationManager.EzLocalisableString("全局判定线位置", "Global HitPosition");
        public static readonly LocalisableString GLOBAL_HIT_POSITION_TOOLTIP = new EzLocalizationManager.EzLocalisableString("全局判定线位置开关", "Global HitPosition Toggle");

        public static readonly LocalisableString HIT_POSITION = new EzLocalizationManager.EzLocalisableString("判定线位置", "Hit Position");
        public static readonly LocalisableString HIT_POSITION_TOOLTIP = new EzLocalizationManager.EzLocalisableString("设置可视的判定线位置", "Set the visible hit position");

        public static readonly LocalisableString HIT_TARGET_ALPHA = new EzLocalizationManager.EzLocalisableString("note命中靶透明度(EzPro专用)", "Hit Target Alpha");

        public static readonly LocalisableString HIT_TARGET_ALPHA_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Ez Style Pro皮肤中note命中靶的透明度, 可见判定线上与note一样的判定板",
            "Set the transparency of the note Hit Target in Ez Style Pro skin, making the hit plate on the hit position visible like the note");

        public static readonly LocalisableString HIT_TARGET_FLOAT_FIXED = new EzLocalizationManager.EzLocalisableString("命中靶的浮动修正(EzPro专用)", "Hit Target Float Fixed");

        public static readonly LocalisableString HIT_TARGET_FLOAT_FIXED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Ez Style Pro皮肤中note命中靶, 修改浮动效果的正弦函数运动范围",
            "Set the note Hit Target in Ez Style Pro skin, modifying the sine function motion range of the floating effect");

        public static readonly LocalisableString NOTE_HEIGHT_SCALE = new EzLocalizationManager.EzLocalisableString("note 高度比例", "Note Height Scale");
        public static readonly LocalisableString NOTE_HEIGHT_SCALE_TOOLTIP = new EzLocalizationManager.EzLocalisableString("统一修改note的高度的比例", "Fixed Height for square notes");

        public static readonly LocalisableString LN_TAIL_ALPHA = new EzLocalizationManager.EzLocalisableString("Tail面尾透明度", "Mania Hold Tail Alpha");

        public static readonly LocalisableString LN_TAIL_ALPHA_TOOLTIP =
            new EzLocalizationManager.EzLocalisableString("Mania Tail面尾的透明度，当前只用于Ez Pro皮肤", "Modify the transparency of the Mania hold tail");

        public static readonly LocalisableString LN_TAIL_MASK_GRADIENT_HEIGHT = new EzLocalizationManager.EzLocalisableString("调整缩短面尾的距离(投)", "Adjust LN Tail Length (Opportunistic)");

        public static readonly LocalisableString LN_TAIL_MASK_GRADIENT_HEIGHT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(投皮) 缩短面条中部实现, 不改变面尾形状",
            "(Opportunistic) Shorten the middle of the hold tail without changing its shape");

        public static readonly LocalisableString NOTE_TRACK_LINE = new EzLocalizationManager.EzLocalisableString("Note辅助线", "Note Track Line");
        public static readonly LocalisableString NOTE_TRACK_LINE_TOOLTIP = new EzLocalizationManager.EzLocalisableString("(Ez风格)note两侧辅助轨道线的高度", "(Ez Style)note side auxiliary track line height");

        public static readonly LocalisableString REFRESH_SAVE_SKIN = new EzLocalizationManager.EzLocalisableString("强制刷新&保存", "Force Refresh & Save Skin");

        public static readonly LocalisableString REFRESH_SAVE_SKIN_TOOLTIP =
            new EzLocalizationManager.EzLocalisableString("没遇到问题不要点，调整用按钮", "If you haven't encountered any issues, don't click this. Use it for adjustments.");

        public static readonly LocalisableString SWITCH_TO_ABSOLUTE = new EzLocalizationManager.EzLocalisableString("强制刷新, 并切换至 绝对位置 (不稳定) ", "Refresh, Switch to Absolute(Unstable)");
        public static readonly LocalisableString SWITCH_TO_RELATIVE = new EzLocalizationManager.EzLocalisableString("强制刷新, 并切换至 相对位置 (不稳定) ", "Refresh, Switch to Relative(Unstable)");
    }
}
