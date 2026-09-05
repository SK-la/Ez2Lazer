// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Localization
{
    public class EzSettingsDesktopPet
    {
        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("桌宠", "Desktop pet");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_ENABLED = new EzLocalizationManager.EzLocalisableString("启用桌宠", "Enable desktop pet");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "总开关。打开后还要勾下面的场景；pet.json 里的 hide/show 规则也能临时显隐。"
            + "\n不用在皮肤编辑器或 Ez 布局编辑器里添加。未按 Left Alt 时点击穿透；按住 Left Alt 可点击互动或拖动位置。",
            "Master toggle. Also enable the scene checkboxes below; pet.json hide/show rules can override visibility."
            + "\nDo not add it in the skin editor or Ez layout editor. Clicks pass through unless Left Alt is held; hold Left Alt to click or drag.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_MENU = new EzLocalizationManager.EzLocalisableString("主菜单显示", "Show on main menu");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_MENU_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "主菜单是否显示桌宠。",
            "Show the pet on the main menu.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_SONG_SELECT = new EzLocalizationManager.EzLocalisableString("选歌界面显示", "Show on song select");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_SONG_SELECT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "选歌界面是否显示桌宠。",
            "Show the pet on song select.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_GAMEPLAY = new EzLocalizationManager.EzLocalisableString("游戏中显示", "Show during gameplay");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_GAMEPLAY_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "游玩时是否允许显示。仍可被 pet.json 的 hide/show 临时关掉或再打开，例如进图隐藏、combo 200 再出现。",
            "Allow the pet during gameplay. pet.json hide/show rules can still hide it on enter and show it again at a combo threshold.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_RESULTS = new EzLocalizationManager.EzLocalisableString("结算显示", "Show on results");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SHOW_ON_RESULTS_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "结算界面是否显示桌宠。可用 pet.json 的 resultsRank 规则配合舞台 motion 走到分数旁。",
            "Show the pet on the results screen. Use pet.json resultsRank rules with stage motions to walk toward the rank.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_PACK = new EzLocalizationManager.EzLocalisableString("桌宠包", "Pet pack");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_PACK_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "扫描 EzResources/Pets 下带 pet.json 的文件夹。PNG 社区包直接可用；Live2D 仅官方预设白名单。",
            "Scans folders with pet.json under EzResources/Pets. PNG community packs work freely; Live2D is official presets only.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SCALE = new EzLocalizationManager.EzLocalisableString("桌宠缩放", "Pet scale");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SCALE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "桌宠立绘缩放。按住 Left Alt 拖动改位置；未按时点击穿透，悬浮仍可触发反应。",
            "Scale of the pet sprite. Hold Left Alt and drag to move; without Alt, clicks pass through while hover reactions still work.");
    }
}
