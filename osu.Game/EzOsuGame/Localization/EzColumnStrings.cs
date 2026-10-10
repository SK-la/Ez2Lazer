// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzColumnStrings
    {
        public static readonly LocalisableString STAGE_DIAGONAL_LANE_ANGLE = new EzLocalizationManager.EzLocalisableString(
            "Mania 斜轨角度",
            "Mania Stage Diagonal lane angle");

        public static readonly LocalisableString STAGE_DIAGONAL_LANE_ANGLE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "通过透视映射模拟轨道旋转。0为关闭（需要重载游戏场景），角度越大越明显（上窄下宽）。",
            "Simulate lane rotation using perspective mapping. 0° is the original look, larger angles increase the effect (narrower top and wider bottom).");

        public static readonly LocalisableString COLUMN_BACKGROUND_DIM = new EzLocalizationManager.EzLocalisableString(
            "Column背景暗化",
            "Column BackGround Dim");

        public static readonly LocalisableString COLUMN_BACKGROUND_DIM_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Column轨道背景的暗化程度, 0为完全透明, 1为完全黑色",
            "Set the dim of each column, 0 is fully transparent, 1 is fully black");

        public static readonly LocalisableString COLUMN_BACKGROUND_BLUR = new EzLocalizationManager.EzLocalisableString(
            "Column背景虚化",
            "Column BackGround Blur");

        public static readonly LocalisableString COLUMN_BACKGROUND_BLUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Column轨道背景的模糊程度, 0为不模糊, 1为完全模糊\n"
            + "调整为0时，将不加载虚化容器，重载游戏场景生效",
            "Set the blur of each column, 0 is no blur, 1 is fully blurred."
            + "\nWhen set to 0, the blur container will not be loaded; requires reloading the gameplay screen to take effect.");

        public static readonly LocalisableString STAGE_BACKGROUND = new EzLocalizationManager.EzLocalisableString(
            "Stage-Background",
            "Stage-Background");

        public static readonly LocalisableString STAGE_BACKGROUND_NONE = new EzLocalizationManager.EzLocalisableString(
            "无",
            "None");

        public static readonly LocalisableString STAGE_BACKGROUND_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "从 EzResources/Panel 加载一张图，作为整段 Stage 的背景（不拆到各列）。"
            + "\n默认无，与现在一致。选中后按列总宽等比例缩放，图片底部对齐判定线，并进入 Stage 虚化。"
            + "\n选项在打开设置时重新读取。",
            "Load one image from EzResources/Panel as a single stage background (not split per column)."
            + "\nDefault is none. When selected, the image scales to the total column width, sits with its bottom on the judgement line, and is included in the stage blur."
            + "\nThe list is refreshed when settings are opened.");

        public static readonly LocalisableString COLUMN_SET = new EzLocalizationManager.EzLocalisableString(
            "Column Set",
            "Column Set");

        public static readonly LocalisableString COLUMN_SET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "按键光只在选中的 EzResources/Column 子文件夹里查找。先按列类型取图，没有再按 S、E、P、B、A、ColumnLight。"
            + "\n这一串都没有就不显示。内置资源只在 EzColumnLight 仍找不到时使用。"
            + "\n选项在打开设置时重新读取。",
            "Column lights are loaded only from the selected EzResources/Column subfolder: the column type first, then lower types in order S, E, P, B, A, then ColumnLight."
            + "\nIf none of those exist, nothing is shown. Built-in resources are used only when EzColumnLight still has no match."
            + "\nThe list is refreshed when settings are opened.");

        public static readonly LocalisableString STAGE_PANEL = new EzLocalizationManager.EzLocalisableString(
            "显示Stage前景面板",
            "Stage Panel");

        public static readonly LocalisableString STAGE_PANEL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "此开关影响stable导入皮肤中的mania-stage显示。"
            + "\nEzPro皮肤的面板不受此开关控制", "Toggle visibility of the stage foreground");

        public static readonly LocalisableString COLOUR_ENABLE_BUTTON = new EzLocalizationManager.EzLocalisableString(
            "启用颜色配置",
            "Enable Colour Config");

        public static readonly LocalisableString COLOUR_ENABLE_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "仅支持EzPro, Ez2, SBI, 3个皮肤.\n"
            + "先修改Base基础颜色，然后定义每一列的类型（一个5种类型，S为Special特殊列，同时还关联特殊列宽度倍率设置）\n"
            + "切换tab栏或保存后, 将重置默认颜色为当前设置值。",
            "Only supports EzPro, Ez2 and SBI skins.\n"
            + "First modify the base colour, then define the type of each column (5 types; S is the Special column and is related to the Special Column Width Factor setting).\n"
            + "Switching tabs or saving will reset the default colours to the current saved values.");

        public static readonly LocalisableString SAVE_COLOUR_BUTTON = new EzLocalizationManager.EzLocalisableString(
            "保存颜色配置",
            "Save Colour Config");

        public static readonly LocalisableString SAVE_COLOUR_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "保存当前颜色，并刷新默认值为当前设置值，下次修改设置时，重置控件的目标为本次保存值"
            + "\n注意！切换Tab视同保存，如果你不喜欢修改结果，请重置颜色后再切换Tab",
            "Save the current colour and update the default value to the current setting."
            + "\nThe next time you modify the setting, the control target will reset to this saved value.");
    }
}
