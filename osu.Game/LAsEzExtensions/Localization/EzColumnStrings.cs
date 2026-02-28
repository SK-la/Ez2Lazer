// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.LAsEzExtensions.Localization
{
    public static class EzColumnStrings
    {
        public static readonly LocalisableString MANIA_PSEUDO_3D_ROTATION = new EzLocalizationManager.EzLocalisableString("Mania 轨道旋转角", "(Mania) Lane Perspective Angle");

        public static readonly LocalisableString MANIA_PSEUDO_3D_ROTATION_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "通过透视映射模拟轨道旋转。0° 为原始效果，角度越大越明显（上窄下宽）。",
            "Simulate lane rotation using perspective mapping. 0° is the original look, larger angles increase the effect (narrower top and wider bottom).");

        public static readonly LocalisableString STAGE_BACKGROUND_DIM = new EzLocalizationManager.EzLocalisableString("轨道暗度", "Column Dim");

        public static readonly LocalisableString STAGE_BACKGROUND_DIM_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Stage面板背景的暗化程度, 0为完全透明, 1为完全黑色", "Set the dim of each column, 0 is fully transparent, 1 is fully black");

        public static readonly LocalisableString STAGE_BACKGROUND_BLUR = new EzLocalizationManager.EzLocalisableString("轨道虚化", "Column Blur");

        public static readonly LocalisableString STAGE_BACKGROUND_BLUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置Stage面板背景的虚化程度, 0为不模糊, 1为完全模糊\n"
            + "注意，如果铺面中有视频，面板", "Set the blur of each column, 0 is no blur, 1 is fully blurred");

        public static readonly LocalisableString STAGE_PANEL = new EzLocalizationManager.EzLocalisableString("Stage前景面板", "Stage Panel");

        public static readonly LocalisableString STAGE_PANEL_TOOLTIP = new EzLocalizationManager.EzLocalisableString("切换Stage前景面板可见性", "Toggle visibility of the stage foreground");

        public static readonly LocalisableString COLOUR_ENABLE_BUTTON = new EzLocalizationManager.EzLocalisableString("启用颜色配置", "Enable Colour Config");

        public static readonly LocalisableString COLOUR_ENABLE_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "仅支持EzPro, Ez2, Strong Box, 3个皮肤.\n"
            + "先修改Base基础颜色，然后定义每一列的类型（一个5种类型，S为Special特殊列，同时还关联特殊列宽度倍率设置）\n"
            + "切换tab栏或保存后, 将重置默认颜色为当前设置值。",
            "Only support EzPro, Ez2, Strong Box skins.\n"
            + "First modify the Base color, then define the type of each column (5 types for one column, S is Special column, also related to Special Column Width Factor setting)\n"
            + "Switching tabs or saving will reset the default color to the current setting value.");

        public static readonly LocalisableString SAVE_COLOUR_BUTTON = new EzLocalizationManager.EzLocalisableString("保存颜色配置", "Save Colour Config");

        public static readonly LocalisableString SAVE_COLOUR_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "保存当前颜色，并刷新默认值为当前设置值，下次修改设置时，重置控件的目标为本次保存值"
            + "\n注意！切换Tab视同保存，如果你不喜欢修改结果，请重置颜色后再切换Tab",
            "Save the current color and refresh the default value to the current setting value. "
            + "\nThe next time you modify the setting, the target of the control will be reset to this saved value.");
    }
}
