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
            "扫描 EzResources/Pets 下带 pet.json 的文件夹。PNG 包直接可用；Live2D 需 renderer:live2d、live2d/ 模型，以及 _cubism/<平台>/ 下的 Cubism Core 动态库。",
            "Scans folders with pet.json under EzResources/Pets. PNG packs work as-is; Live2D needs renderer:live2d, a live2d/ model, and the Cubism Core dynamic library under _cubism/<platform>/.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_STATUS_PNG_OK = new EzLocalizationManager.EzLocalisableString(
            "当前包：PNG 帧可用。",
            "Current pack: PNG frames ready.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_STATUS_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "当前包没有可用帧。把 PNG 放进动作文件夹，或改用 Live2D（见文档）。",
            "Current pack has no frames. Add PNGs to action folders, or use Live2D (see docs).");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_STATUS_MISSING_PACK = new EzLocalizationManager.EzLocalisableString(
            "找不到该桌宠包文件夹。",
            "Pet pack folder not found.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_STATUS_LIVE2D_READY = new EzLocalizationManager.EzLocalisableString(
            "当前包：Live2D 模型与 Cubism Core 均已就绪。",
            "Current pack: Live2D model and Cubism Core are ready.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_STATUS_LIVE2D_MISSING_CORE = new EzLocalizationManager.EzLocalisableString(
            "当前包是 Live2D，但缺少当前平台的 Cubism Core（从 Cubism SDK for Native 的 Core/dll 复制到下方路径）。",
            "Live2D pack selected, but the Cubism Core for this platform is missing (copy from Cubism SDK for Native Core/dll to the path below).");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_STATUS_LIVE2D_MISSING_MODEL = new EzLocalizationManager.EzLocalisableString(
            "当前包声明了 Live2D，但 live2d/ 下没有 .model3.json 或 .moc3。",
            "Pack asks for Live2D, but no .model3.json or .moc3 was found under live2d/.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SCALE = new EzLocalizationManager.EzLocalisableString("桌宠缩放", "Pet scale");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SCALE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "桌宠立绘缩放。按住 Left Alt 拖动改位置；未按时点击穿透，悬浮仍可触发反应。",
            "Scale of the pet sprite. Hold Left Alt and drag to move; without Alt, clicks pass through while hover reactions still work.");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_LIP_SYNC = new EzLocalizationManager.EzLocalisableString(
            "Live2D 口型关联音乐", "Live2D lip-sync to music");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_LIP_SYNC_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后按音频振幅开合嘴巴（最低不完全闭合）。仅对 Live2D 包生效。",
            "When enabled, mouth open follows track amplitude (never fully closed). Live2D packs only.");
    }
}
