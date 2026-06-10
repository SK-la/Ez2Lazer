// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzEditorStrings
    {
        #region Settings (Skin section)

        public static readonly LocalisableString SETTINGS_SKIN_EDITOR_BUTTON = new EzLocalizationManager.EzLocalisableString(
            "Skin Editor(Madding)",
            "Skin Editor (WIP)");

        public static readonly LocalisableString SETTINGS_SKIN_EDITOR_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(画饼)长期施工中，目标："
            + "\n1.游戏内完整Skin.ini编辑"
            + "\n2.实现完整Ez特有皮肤设置，并可覆写进Skin.ini"
            + "\n3.游戏内PS、图片导出(包括渐变动画)",
            "(WIP) Long-term goals:"
            + "\n1. Full in-game skin.ini editing"
            + "\n2. Full Ez skin settings with skin.ini export"
            + "\n3. In-game image export (including gradient animations)");

        public static readonly LocalisableString SETTINGS_AUTO_APPLY_SKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "自动切换皮肤配置",
            "Auto-apply skin configuration");

        public static readonly LocalisableString SETTINGS_AUTO_APPLY_SKIN_JSON_NOTE = new EzLocalizationManager.EzLocalisableString(
            "开启后换肤将皮肤的 EzSkin.json 导入内存（不写 EzSkinSettings.ini）；关闭后恢复磁盘全局配置。",
            "When enabled, switching skins imports EzSkin.json into memory (does not write EzSkinSettings.ini). When disabled, reloads global settings from disk.");

        public static readonly LocalisableString SETTINGS_RELOAD_SCRIPTED_SKINS = new EzLocalizationManager.EzLocalisableString(
            "重载脚本皮肤",
            "Reload scripted skins");

        public static readonly LocalisableString SETTINGS_RELOAD_SCRIPTED_SKINS_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "手动重载 ScriptedSkin 目录下的脚本并刷新列表",
            "Manually reload scripts under ScriptedSkin and refresh the list");

        #endregion

        #region Menu bar

        public static readonly LocalisableString MENU_APPLY = new EzLocalizationManager.EzLocalisableString("应用", "Apply");

        public static readonly LocalisableString MENU_EXPORT_OSK = new EzLocalizationManager.EzLocalisableString("导出 .osk", "Export .osk");

        public static readonly LocalisableString MENU_CONFIG = new EzLocalizationManager.EzLocalisableString("配置", "Config");

        public static readonly LocalisableString MENU_CREATE_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "创建 EzSkin.json",
            "Create EzSkin.json");

        public static readonly LocalisableString MENU_UPDATE_EZSKIN_JSON_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "更新 EzSkin.json 快照",
            "Update EzSkin.json snapshot");

        public static readonly LocalisableString MENU_REMOVE_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "移除 EzSkin.json",
            "Remove EzSkin.json");

        public static readonly LocalisableString MENU_IMPORT_JSON = new EzLocalizationManager.EzLocalisableString("导入 JSON", "Import JSON");

        public static readonly LocalisableString MENU_IMPORT_FROM_SKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "从皮肤导入 EzSkin.json",
            "Import EzSkin.json from skin");

        public static readonly LocalisableString MENU_EXPORT_JSON = new EzLocalizationManager.EzLocalisableString("导出 JSON", "Export JSON");

        public static readonly LocalisableString MENU_WRITE_COLOURS_TO_SKIN_INI = new EzLocalizationManager.EzLocalisableString(
            "将 EzSkin 颜色写入 Skin.ini",
            "Write EzSkin colours to skin.ini");

        public static readonly LocalisableString MENU_WRITE_SIZES_TO_SKIN_INI = new EzLocalizationManager.EzLocalisableString(
            "将 EzSkin 尺寸写入 Skin.ini",
            "Write EzSkin sizes to skin.ini");

        #endregion

        #region Notifications

        public static readonly LocalisableString NOTIFY_BEATMAP_NOT_FOUND = new EzLocalizationManager.EzLocalisableString(
            "未找到可用谱面",
            "No beatmap available");

        public static readonly LocalisableString NOTIFY_CANNOT_CREATE_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "无法创建 EzSkin.json",
            "Could not create EzSkin.json");

        public static readonly LocalisableString NOTIFY_CREATED_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "已创建 EzSkin.json",
            "Created EzSkin.json");

        public static readonly LocalisableString NOTIFY_CANNOT_UPDATE_EZSKIN_JSON_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "无法更新 EzSkin.json 快照",
            "Could not update EzSkin.json snapshot");

        public static readonly LocalisableString NOTIFY_UPDATED_EZSKIN_JSON_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "已更新 EzSkin.json 快照",
            "Updated EzSkin.json snapshot");

        public static readonly LocalisableString NOTIFY_CANNOT_REMOVE_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "无法移除 EzSkin.json",
            "Could not remove EzSkin.json");

        public static readonly LocalisableString NOTIFY_REMOVED_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "已移除 EzSkin.json",
            "Removed EzSkin.json");

        public static readonly LocalisableString NOTIFY_NO_EZSKIN_JSON_ON_SKIN = new EzLocalizationManager.EzLocalisableString(
            "当前皮肤没有 EzSkin.json",
            "Current skin has no EzSkin.json");

        public static readonly LocalisableString NOTIFY_CANNOT_READ_EZSKIN_JSON = new EzLocalizationManager.EzLocalisableString(
            "无法读取 EzSkin.json",
            "Could not read EzSkin.json");

        public static readonly LocalisableString NOTIFY_IMPORTED_FROM_SKIN_TO_MEMORY = new EzLocalizationManager.EzLocalisableString(
            "已从皮肤导入配置到内存",
            "Imported skin configuration to memory");

        public static readonly LocalisableString NOTIFY_FILE_SELECTOR_NOT_SUPPORTED = new EzLocalizationManager.EzLocalisableString(
            "当前平台不支持文件选择",
            "File selection is not supported on this platform");

        public static readonly LocalisableString NOTIFY_IMPORTED_JSON = new EzLocalizationManager.EzLocalisableString(
            "已导入 JSON 配置",
            "Imported JSON configuration");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_IMPORT_FAILED = new EzLocalizationManager.EzLocalisableString("导入失败: {0}", "Import failed: {0}");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_EXPORTED_TO = new EzLocalizationManager.EzLocalisableString("已导出到 {0}", "Exported to {0}");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_EXPORT_FAILED = new EzLocalizationManager.EzLocalisableString("导出失败: {0}", "Export failed: {0}");

        public static readonly LocalisableString NOTIFY_CANNOT_WRITE_SKIN_INI_COLOURS = new EzLocalizationManager.EzLocalisableString(
            "无法写入 skin.ini 列色",
            "Could not write skin.ini column colours");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_COLOURS_WRITTEN_TO_DRAFT = new EzLocalizationManager.EzLocalisableString("已将 {0}K 列色写入 skin.ini 草稿，请应用保存",
            "Wrote {0}K column colours to skin.ini draft — use Apply to save");

        public static readonly LocalisableString NOTIFY_CANNOT_WRITE_SKIN_INI_SIZES = new EzLocalizationManager.EzLocalisableString(
            "无法写入 skin.ini 尺寸",
            "Could not write skin.ini sizes");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_SIZES_WRITTEN_TO_DRAFT = new EzLocalizationManager.EzLocalisableString(
            "已将 {0}K 尺寸写入 skin.ini 草稿，请应用保存", "Wrote {0}K sizes to skin.ini draft — use Apply to save");

        public static readonly LocalisableString NOTIFY_CANNOT_EXPORT_SKIN = new EzLocalizationManager.EzLocalisableString(
            "当前皮肤无法导出",
            "Current skin cannot be exported");

        public static readonly LocalisableString NOTIFY_EXPORT_NOT_SUPPORTED_PLATFORM = new EzLocalizationManager.EzLocalisableString(
            "当前平台不支持导出",
            "Export is not supported on this platform");

        public static readonly LocalisableString NOTIFY_EXPORTING_OSK = new EzLocalizationManager.EzLocalisableString(
            "正在导出 .osk…",
            "Exporting .osk…");

        public static readonly LocalisableString NOTIFY_SAVED_SKIN_INI_AND_EXPORTING_OSK = new EzLocalizationManager.EzLocalisableString(
            "已保存 skin.ini 并导出 .osk…",
            "Saved skin.ini and exporting .osk…");

        #endregion

        #region Dialogs

        public static readonly LocalisableString EXIT_CONFIRM_APPLY_TO_SKIN = new EzLocalizationManager.EzLocalisableString(
            "应用更改到皮肤？",
            "Apply changes to skin?");

        #endregion

        #region Scene bar and tabs

        public static readonly LocalisableString SCENE_LIBRARY_LABEL = new EzLocalizationManager.EzLocalisableString("场景", "Scenes");

        public static readonly LocalisableString TAB_APPEARANCE = new EzLocalizationManager.EzLocalisableString("外观", "Appearance");

        public static readonly LocalisableString TAB_SIZE = new EzLocalizationManager.EzLocalisableString("尺寸", "Size");

        public static readonly LocalisableString TAB_COLOUR = new EzLocalizationManager.EzLocalisableString("颜色", "Colour");

        public static readonly LocalisableString TAB_SKIN_INI = new EzLocalizationManager.EzLocalisableString("skin.ini", "skin.ini");

        #endregion

        #region Sidebar groups

        public static readonly LocalisableString GROUP_TEXTURE = new EzLocalizationManager.EzLocalisableString("纹理", "Texture");

        public static readonly LocalisableString GROUP_STAGE = new EzLocalizationManager.EzLocalisableString("舞台", "Stage");

        public static readonly LocalisableString GROUP_SIZE = new EzLocalizationManager.EzLocalisableString("尺寸", "Size");

        public static readonly LocalisableString GROUP_SKIN_SPECIFIC = new EzLocalizationManager.EzLocalisableString("皮肤专用", "Skin-specific");

        public static readonly LocalisableString GROUP_BASE_COLOURS = new EzLocalizationManager.EzLocalisableString("基础颜色", "Base colours");

        public static readonly LocalisableString GROUP_COLUMN_COLOURS = new EzLocalizationManager.EzLocalisableString("列配色", "Column colours");

        public static readonly LocalisableString GROUP_GENERAL = new EzLocalizationManager.EzLocalisableString("General", "General");

        public static readonly LocalisableString GROUP_COLOURS = new EzLocalizationManager.EzLocalisableString("Colours", "Colours");

        public static readonly LocalisableString GROUP_MODE = new EzLocalizationManager.EzLocalisableString("模式", "Mode");

        #endregion

        #region Toolbar and preview

        public static readonly LocalisableString TOOLBAR_PAUSE_BEATMAP = new EzLocalizationManager.EzLocalisableString("暂停谱面", "Pause beatmap");

        public static readonly LocalisableString TOOLBAR_PLAY_BEATMAP = new EzLocalizationManager.EzLocalisableString("播放谱面", "Play beatmap");

        public static readonly LocalisableString TOOLBAR_SELECT_MODE = new EzLocalizationManager.EzLocalisableString("选择模式", "Select mode");

        public static readonly LocalisableString TOOLBAR_PREVIEW_NOT_SUPPORTED = new EzLocalizationManager.EzLocalisableString(
            "预览尚未支持",
            "Preview not supported yet");

        public static readonly LocalisableString PLACEHOLDER_VIRTUAL_PLAYFIELD_NOT_SUPPORTED = new EzLocalizationManager.EzLocalisableString(
            "Virtual playfield not supported",
            "Virtual playfield not supported");

        public static readonly LocalisableString PLACEHOLDER_COMPARISON_NOT_SUPPORTED = new EzLocalizationManager.EzLocalisableString(
            "Comparison preview not supported",
            "Comparison preview not supported");

        public static readonly LocalisableString PLACEHOLDER_BEATMAP_NOT_LOADED = new EzLocalizationManager.EzLocalisableString(
            "未加载谱面",
            "Beatmap not loaded");

        public static readonly LocalisableString PLACEHOLDER_RULESET_PREVIEW_NOT_SUPPORTED = new EzLocalizationManager.EzLocalisableString(
            "该规则集预览尚未支持",
            "Beatmap preview is not supported for this ruleset");

        public static readonly LocalisableString PLACEHOLDER_BEATMAP_LOAD_FAILED = new EzLocalizationManager.EzLocalisableString(
            "谱面加载失败",
            "Failed to load beatmap");

        #endregion

        #region Sidebar

        public static readonly LocalisableString SIDEBAR_PIN_LABEL = new EzLocalizationManager.EzLocalisableString("固定显示", "Pin sidebar");

        #endregion

        #region Skin.ini editor

        public static readonly LocalisableString SKIN_INI_GENERAL_HINT = new EzLocalizationManager.EzLocalisableString(
            "编辑当前皮肤的 skin.ini。保存前会自动备份到 Backup/。",
            "Edit the current skin's skin.ini. A backup is created under Backup/ before saving.");

        public static readonly LocalisableString SKIN_INI_RAW_EDITOR_PLACEHOLDER = new EzLocalizationManager.EzLocalisableString(
            "编辑原始 skin.ini",
            "Edit raw skin.ini");

        public static readonly LocalisableString SKIN_INI_COLOURS_HINT = new EzLocalizationManager.EzLocalisableString(
            "skin.ini 颜色（[Colours]）。与 Ez 颜色设置无关。",
            "skin.ini colours ([Colours]). Independent from Ez colour settings.");

        public static readonly LocalisableString SKIN_INI_MANIA_HINT = new EzLocalizationManager.EzLocalisableString(
            "Mania 规则集配置。按 Keys 分组编辑；未列出的键仍保留在文件中。",
            "Mania ruleset settings. Edit by Keys; unlisted key blocks remain in the file.");

        public static readonly LocalisableString SKIN_INI_MANIA_KEYS_LABEL = new EzLocalizationManager.EzLocalisableString("Keys", "Keys");

        public static readonly LocalisableString SKIN_INI_FIELD_GROUP_LAYOUT = new EzLocalizationManager.EzLocalisableString("布局", "Layout");

        public static readonly LocalisableString SKIN_INI_FIELD_GROUP_POSITION = new EzLocalizationManager.EzLocalisableString("位置", "Position");

        public static readonly LocalisableString SKIN_INI_FIELD_GROUP_DISPLAY = new EzLocalizationManager.EzLocalisableString("显示", "Display");

        public static readonly LocalisableString SKIN_INI_FIELD_GROUP_EXPLOSION = new EzLocalizationManager.EzLocalisableString("爆炸", "Explosion");

        public static readonly LocalisableString SKIN_INI_PER_KEY_COLOURS = new EzLocalizationManager.EzLocalisableString("键位颜色", "Key colours");

        public static readonly LocalisableString SKIN_INI_SAVE_BUTTON = new EzLocalizationManager.EzLocalisableString("保存 Skin.ini", "Save skin.ini");

        #endregion

        #region Colour settings

        public static readonly LocalisableString KEY_MODE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "Key Mode (键位数)",
            "Key mode");

        public static readonly LocalisableString SELECT_KEY_MODE_FIRST = new EzLocalizationManager.EzLocalisableString(
            "请先选择键位数模式",
            "Select a key mode first");

        public static readonly EzLocalizationManager.EzLocalisableString COLUMN_TYPE_HEADER = new EzLocalizationManager.EzLocalisableString("{0}K ColumnType 列类型", "{0}K column types");

        public static readonly LocalisableString BASE_COLOURS_HEADER = new EzLocalizationManager.EzLocalisableString(
            "Base Colors (基础颜色)",
            "Base colours");

        #endregion
    }
}
