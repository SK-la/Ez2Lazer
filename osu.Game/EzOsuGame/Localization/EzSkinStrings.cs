// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzSkinStrings
    {
        public static readonly LocalisableString GLOBAL_TEXTURE_NAME = new EzLocalizationManager.EzLocalisableString(
            "[EzHUD] 全局纹理名称", "[EzHUD] Global Texture Name");

        public static readonly LocalisableString GLOBAL_TEXTURE_NAME_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "统一修改当前皮肤中所有Ez-HUD组件的纹理名称, 如score、combo、判定等纹理。"
            + "\n在本地EzResources/GameTheme/中，不支持增减子文件夹。",
            "Set a global texture name for all EZ-HUD components in the current skin. Such as: score, combo, judgement, etc.");

        public static readonly LocalisableString STAGE_SET = new EzLocalizationManager.EzLocalisableString(
            "[EzPro] Stage Set", "[EzPro] Stage Set");

        public static readonly LocalisableString STAGE_SET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "修改EzPro皮肤中的整套前景面板, 如果有动效, 则关联实时BPM。"
            + "\n在本地EzResources/Stage/中，支持自定义子文件夹增减修改, 选项会在重载时重新读取文件夹名称。"
            + "\nStage/下的文件夹可以自己改名, 但子文件夹及文件的名称必须完全一致。",
            "Modify the entire foreground stage set in the EzPro skin; animated sets may be linked to real-time BPM."
            + "\nSupports adding or removing subfolders under local EzResources/Stage for customization. Options will be reloaded when the skin is reloaded."
            + "\nFolders under Stage/ can be renamed, but subfolder and file names must match exactly.");

        public static readonly LocalisableString NOTE_SET = new EzLocalizationManager.EzLocalisableString(
            "[EzPro] Note Set", "[EzPro] Note Set");

        public static readonly LocalisableString NOTE_SET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "修改EzPro皮肤中的整套note, 含所有note和打击光效。"
            + "\n在本地EzResources/note中，支持自定义子文件夹增减修改, 选项会在重载时重新读取文件夹名称。"
            + "\n最低限度必须包含whitenote/000一张基础note，EzPro皮肤能够自动创建完整LN，并搭配着色系统使用。",
            "Set a note set for all notes and hit effects."
            + "\nSupport adding or removing subfolders in the local EzResources/note for customization. Options will be reloaded when the skin is reloaded."
            + "\nAt minimum, include a base note such as 'whitenote/000'. EzPro can auto-create LN and integrates with the colour system.");

        public static readonly LocalisableString COLUMN_WIDTH_STYLE = new EzLocalizationManager.EzLocalisableString(
            "列宽风格", "Column Width Style");

        public static readonly LocalisableString COLUMN_WIDTH_STYLE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "全局设置可以用在所有皮肤上。"
            + "\n全局总列宽=设置值×10, 每列宽度=key数/总列宽。"
            + "\n其他选项均为设置单列宽度。",
            "Global setting can be applied to all skins."
            + "\nGlobal total column width = configured value × 10."
            + "\nOther styles are literal (functionality may be imperfect).");

        public static readonly LocalisableString COLUMN_WIDTH = new EzLocalizationManager.EzLocalisableString(
            "单轨宽度", "Column Width");

        public static readonly LocalisableString COLUMN_WIDTH_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置每列轨道的宽度", "Set the width of each column");

        public static readonly LocalisableString SPECIAL_FACTOR = new EzLocalizationManager.EzLocalisableString(
            "特殊轨宽度倍率", "Special Column Width Factor");

        public static readonly LocalisableString SPECIAL_FACTOR_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "关联ColumnType设置, S列类型为特殊列, 以此实现两种宽度的区分。",
            "S column type denotes Special columns, allowing a distinction between two widths.");

        public static readonly LocalisableString GLOBAL_HIT_POSITION = new EzLocalizationManager.EzLocalisableString(
            "启用全局判定线", "Enable Global HitPosition");

        public static readonly LocalisableString GLOBAL_HIT_POSITION_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，所有皮肤应用下方判定线位置设置", "When enabled, applies the below hit position setting to all skins.");

        public static readonly LocalisableString HIT_POSITION = new EzLocalizationManager.EzLocalisableString(
            "判定线位置", "Hit Position");

        public static readonly LocalisableString HIT_POSITION_TOOLTIP = new EzLocalizationManager.EzLocalisableString("设置可视的判定线位置", "Set the visible hit position");

        public static readonly LocalisableString HIT_TARGET_ALPHA = new EzLocalizationManager.EzLocalisableString(
            "[EzPro] 命中靶透明度", "[EzPro] Hit-Target Alpha");

        public static readonly LocalisableString HIT_TARGET_ALPHA_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Ez Style Pro皮肤中note命中靶的透明度, 可见判定线上与note一样的判定板",
            "Set the transparency of the note Hit-Target in Ez Style Pro skin, making the hit plate on the hit position visible like the note");

        public static readonly LocalisableString HIT_TARGET_FLOAT_FIXED = new EzLocalizationManager.EzLocalisableString("[EzPro] 命中靶浮动修正", "[EzPro] Hit-Target Float Fixed");

        public static readonly LocalisableString HIT_TARGET_FLOAT_FIXED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Ez Style Pro皮肤中note命中靶, 修改浮动效果的正弦函数运动范围",
            "Set the note Hit-Target in Ez Style Pro skin, modifying the sine function motion range of the floating effect");

        public static readonly LocalisableString NOTE_HEIGHT_SCALE = new EzLocalizationManager.EzLocalisableString(
            "note 高度比例", "Note Height Scale");

        public static readonly LocalisableString NOTE_HEIGHT_SCALE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "统一修改note的高度的比例", "Scale factor to uniformly adjust note height.");

        public static readonly LocalisableString NOTE_CORNER_RADIUS = new EzLocalizationManager.EzLocalisableString(
            "[SBI] note圆角", "[SBI] Note Corner Radius");

        public static readonly LocalisableString NOTE_CORNER_RADIUS_TOOLTIP = new EzLocalizationManager.EzLocalisableString("目前只用于[SBI]皮肤", "Currently only used by SBI skins.");

        public static readonly LocalisableString LN_TAIL_ALPHA = new EzLocalizationManager.EzLocalisableString("LN面尾透明度", "LN Tail Alpha");

        public static readonly LocalisableString LN_TAIL_ALPHA_TOOLTIP = new EzLocalizationManager.EzLocalisableString("Mania LN-Tail 面尾的透明度，当前只用于EzPro、SBI皮肤",
            "Adjust the transparency of Mania LN tails; currently only used in EzPro and SBI skins.");

        public static readonly LocalisableString MANIA_LN_GRADIENT_ENABLE = new EzLocalizationManager.EzLocalisableString("LN 伪面尾开关", "LN Gradient Enable");

        public static readonly LocalisableString MANIA_LN_GRADIENT_ENABLE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "启用后，原Tail面尾隐藏，由Body面身的末端作为面尾。"
            + "\n此举不会修改判定线实际位置，需要重载皮肤生效。"
            + "\n必须开启此开关后才能使用下方真投皮面尾。",
            "When enabled, the original hold tail will be hidden, and the end of the hold body will serve as the tail. "
            + "This will not modify the actual position of the hit, and requires a skin reload to take effect. "
            + "You must enable this switch to use the true opportunistic tail below.");

        public static readonly LocalisableString LN_GRADIENT_TAIL_HEIGHT = new EzLocalizationManager.EzLocalisableString(
            "调整投皮面尾的幅度", "Adjust LN Tail Gradient Length");

        public static readonly LocalisableString LN_GRADIENT_TAIL_HEIGHT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(投皮) 缩短面条中部, 不改变面尾形状",
            "(Gradient LN) Shorten the middle of the hold tail without changing its shape");

        public static readonly LocalisableString NOTE_TRACK_LINE = new EzLocalizationManager.EzLocalisableString(
            "[EzPro] Note侧轨道线", "EzPro] Note Track Line");

        public static readonly LocalisableString NOTE_TRACK_LINE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "EzPro皮肤中, note两侧辅助轨道线的高度",
            "Height of note-side auxiliary track lines in EzPro skins.");

        public static readonly LocalisableString REFRESH_SAVE_SKIN = new EzLocalizationManager.EzLocalisableString(
            "强制刷新&保存", "Force Refresh & Save Skin");

        public static readonly LocalisableString REFRESH_SAVE_SKIN_TOOLTIP = new EzLocalizationManager.EzLocalisableString("没遇到问题不需要点，测试用按钮，触发皮肤重载。",
            "If you haven't encountered any issues, don't click this. Use it for adjustments.");

        public static readonly LocalisableString SWITCH_TO_ABSOLUTE = new EzLocalizationManager.EzLocalisableString(
            "强制刷新, 并切换至 绝对位置 (不稳定) ", "Refresh, Switch to Absolute(Unstable)");

        public static readonly LocalisableString SWITCH_TO_RELATIVE = new EzLocalizationManager.EzLocalisableString(
            "强制刷新, 并切换至 相对位置 (不稳定) ", "Refresh, Switch to Relative(Unstable)");
    }
}
