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
