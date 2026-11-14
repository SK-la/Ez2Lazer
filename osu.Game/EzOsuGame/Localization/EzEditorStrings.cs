// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzEditorStrings
    {
        #region Settings (Skin section)

        public static readonly LocalisableString SETTINGS_SKIN_EDITOR_BUTTON = new EzLocalizationManager.EzLocalisableString(
            "Ez Skin 编辑器",
            "Ez Skin Editor");

        public static readonly LocalisableString SETTINGS_SKIN_EDITOR_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "此编辑器中的HUD均修改Ez布局图层，不会修改皮肤HUD图层"
            + "\n主题 HUD 布局存于 EzResources/Config/layout 与 layout-{主题} 文件夹",
            "Editor HUDs in this editor modify the Ez layout layer, not the skin HUD layer"
            + "\nThemed HUD layouts live under EzResources/Config/layout and layout-{theme} folders");

        public static readonly LocalisableString LAYOUT_SAVED = new EzLocalizationManager.EzLocalisableString(
            "布局已保存",
            "Layout saved");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_SKIN_LAYOUT_EXIT_SAVE_FAILED = new EzLocalizationManager.EzLocalisableString(
            "皮肤 HUD 布局未写入磁盘，请退出前再试保存。",
            "Skin HUD layout was not written to disk. Try saving again before exiting.");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_SKIN_LAYOUT_EXIT_CONTAINERS_LOADING = new EzLocalizationManager.EzLocalisableString(
            "皮肤 HUD 容器仍在加载，未写入布局。",
            "Skin HUD layout was not written because skinnable containers were still loading.");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFY_SKIN_LAYOUT_SAVE_CONTAINERS_LOADING = new EzLocalizationManager.EzLocalisableString(
            "HUD 容器仍在加载，已跳过保存。",
            "Skipped saving skin HUD layout because skinnable containers are still loading.");

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

        public static readonly LocalisableString SETTINGS_ENABLE_SCRIPTED_SKINS = new EzLocalizationManager.EzLocalisableString(
            "启用脚本皮肤",
            "Enable scripted skins");

        public static readonly LocalisableString SETTINGS_ENABLE_SCRIPTED_SKINS_NOTE = new EzLocalizationManager.EzLocalisableString(
            "关闭时不扫描 ScriptedSkin 目录、不监视脚本文件、不做脚本编译，可省下启动期开销；修改后需重启生效。"
            + "\n开启后列表显示目录名（脚本声明的名称需选中该皮肤时才编译读取）。",
            "When disabled the ScriptedSkin directory is not scanned, watched, or compiled, saving startup cost. Requires a restart to take effect."
            + "\nWhen enabled the list shows folder names; a script's declared name is only read once that skin is selected.");

        #endregion

        #region Menu bar

        public static readonly LocalisableString MENU_APPLY = new EzLocalizationManager.EzLocalisableString("应用", "Apply");

        public static readonly LocalisableString MENU_EXPORT_OSK = new EzLocalizationManager.EzLocalisableString("导出 .osk", "Export .osk");

        public static readonly LocalisableString MENU_CONFIG = new EzLocalizationManager.EzLocalisableString("配置", "Config");

        public static readonly LocalisableString MENU_LAYOUT = new EzLocalizationManager.EzLocalisableString("布局", "Layout");

        public static readonly LocalisableString MENU_LAYOUT_THEME_SELECT = new EzLocalizationManager.EzLocalisableString(
            "全局层选择",
            "Theme pack");

        public static readonly LocalisableString MENU_LAYOUT_EXPORT_PACK = new EzLocalizationManager.EzLocalisableString(
            "全局层导出",
            "Export theme pack");

        public static readonly LocalisableString MENU_LAYOUT_IMPORT_PACK = new EzLocalizationManager.EzLocalisableString(
            "全局层导入",
            "Import theme pack");

        public static readonly LocalisableString MENU_LAYOUT_EXPORT_LAYER = new EzLocalizationManager.EzLocalisableString(
            "当前层导出",
            "Export current layer");

        public static readonly LocalisableString MENU_LAYOUT_IMPORT_LAYER = new EzLocalizationManager.EzLocalisableString(
            "当前层导入",
            "Import current layer");

        public static readonly LocalisableString MENU_LAYOUT_THEME_RESCAN = new EzLocalizationManager.EzLocalisableString(
            "重扫 layout 文件夹",
            "Rescan layout folders");

        public static readonly LocalisableString LAYOUT_IMPORT_THEME_HEADER = new EzLocalizationManager.EzLocalisableString(
            "导入全局层",
            "Import theme pack");

        public static readonly LocalisableString LAYOUT_IMPORT_THEME_BODY = new EzLocalizationManager.EzLocalisableString(
            "输入主题名（将保存为 layout-主题）。下一步请选择该包内任意一个 JSON 文件以定位文件夹。",
            "Enter a theme name (saved as layout-{name}). Next, pick any JSON file inside that pack folder.");

        public static readonly LocalisableString LAYOUT_IMPORT_THEME_PLACEHOLDER = new EzLocalizationManager.EzLocalisableString(
            "主题名",
            "Theme name");

        public static readonly LocalisableString LAYOUT_IMPORT_THEME_CONFIRM = new EzLocalizationManager.EzLocalisableString(
            "选择文件夹",
            "Choose folder");

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

        public static readonly LocalisableString MENU_CREATE_CONFIG_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "创建配置快照",
            "Create config snapshot");

        public static readonly LocalisableString MENU_RESTORE_CONFIG_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "回到上一个快照",
            "Restore previous snapshot");

        public static readonly LocalisableString MENU_EXPORT_PREVIEW_IMAGE = new EzLocalizationManager.EzLocalisableString(
            "导出预览图片",
            "Export preview image");

        public static readonly LocalisableString TOOLBAR_CLEAR_BEATMAP_PREVIEW = new EzLocalizationManager.EzLocalisableString(
            "清除谱面预览",
            "Clear beatmap preview");

        public static readonly LocalisableString MENU_EZ_SETTINGS = new EzLocalizationManager.EzLocalisableString(
            "Ez设置",
            "Ez Settings");

        public static readonly LocalisableString MENU_FIXED_NOTE_GRID = new EzLocalizationManager.EzLocalisableString(
            "固定note网格",
            "Fixed note grid");

        public static readonly LocalisableString MENU_HUD_SNAP = new EzLocalizationManager.EzLocalisableString(
            "HUD磁吸",
            "HUD snap");

        public static readonly LocalisableString MENU_HUD_SNAP_DISTANCE = new EzLocalizationManager.EzLocalisableString(
            "吸附距离",
            "Snap distance");

        public static readonly LocalisableString MENU_HUD_SNAP_PIXEL_FLUSH = new EzLocalizationManager.EzLocalisableString(
            "像素吸附",
            "Pixel snap (rendered bounds; off = editor container box)");

        #endregion

        #region Notifications

        public static readonly LocalisableString NOTIFY_CREATED_CONFIG_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "已创建配置快照",
            "Created config snapshot");

        public static readonly LocalisableString NOTIFY_RESTORED_CONFIG_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "已恢复到上一个配置快照",
            "Restored previous config snapshot");

        public static readonly LocalisableString NOTIFY_EXPORTED_PREVIEW_IMAGE = new EzLocalizationManager.EzLocalisableString(
            "已导出预览图片",
            "Exported preview image");

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

        public static readonly LocalisableString NOTIFY_LAYOUT_NO_CURRENT_LAYER = new EzLocalizationManager.EzLocalisableString(
            "当前场景没有可导入导出的布局层",
            "No editable layout layer in this scene");

        public static readonly LocalisableString NOTIFY_LAYOUT_IMPORTED_PACK = new EzLocalizationManager.EzLocalisableString(
            "已导入全局层",
            "Theme pack imported");

        public static readonly LocalisableString NOTIFY_LAYOUT_IMPORTED_LAYER = new EzLocalizationManager.EzLocalisableString(
            "已导入当前层",
            "Current layer imported");

        public static readonly LocalisableString NOTIFY_LAYOUT_THEME_NAME_REQUIRED = new EzLocalizationManager.EzLocalisableString(
            "请输入主题名",
            "Enter a theme name");

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

        public static readonly LocalisableString SWITCH_EDITOR_SAVE_OFFICIAL_LAYOUT = new EzLocalizationManager.EzLocalisableString(
            "保存 HUD 布局到当前皮肤？",
            "Save HUD layout to the current skin?");

        #endregion

        #region Mania editor extension columns

        public static readonly LocalisableString EXTENSION_COLUMN_EDIT_MODE = new EzLocalizationManager.EzLocalisableString(
            "列编辑",
            "Column edit");

        public static readonly LocalisableString EXTENSION_COLUMN_INSERT = new EzLocalizationManager.EzLocalisableString(
            "在此列右侧插入扩展列",
            "Insert an extension column after this column");
        public static readonly LocalisableString EXTENSION_COLUMN_REMOVE = new EzLocalizationManager.EzLocalisableString(
            "移除本列扩展列",
            "Remove this extension column");

        public static readonly LocalisableString EXTENSION_COLUMN_SELECT_ALL_NOTES = new EzLocalizationManager.EzLocalisableString(
            "全选本列 note",
            "Select every note on this column");

        public static readonly EzLocalizationManager.EzLocalisableString EXTENSION_COLUMN_REMOVE_WITH_NOTES = new EzLocalizationManager.EzLocalisableString(
            "扩展列 #{0} 有 {1} 个 note，移除该列会连同 note 一起删除。",
            "Extension column #{0} holds {1} note(s); removing it also removes them.");

        #endregion

        #region Scene bar and tabs

        public static readonly LocalisableString SCENE_LIBRARY_LABEL = new EzLocalizationManager.EzLocalisableString("场景库", "Scene library");

        public static readonly LocalisableString SCENE_GROUP_SONG_SELECT = new EzLocalizationManager.EzLocalisableString("选歌", "Song select");

        public static readonly LocalisableString SCENE_GROUP_GAMEPLAY = new EzLocalizationManager.EzLocalisableString("游玩", "Gameplay");

        public static readonly LocalisableString SCENE_GROUP_BEATMAP_EDITOR = new EzLocalizationManager.EzLocalisableString("谱面编辑器", "Beatmap editor");

        public static readonly LocalisableString SCENE_OFFICIAL_HUD = new EzLocalizationManager.EzLocalisableString("官方 HUD", "Official HUD");

        public static readonly LocalisableString SCENE_EZ_SKIN = new EzLocalizationManager.EzLocalisableString("Ez Skin", "Ez Skin");

        public static readonly LocalisableString TOOLBAR_HUD_LAYER = new EzLocalizationManager.EzLocalisableString("HUD layer", "HUD layer");

        public static readonly LocalisableString LAYER_HUD_OFF = new EzLocalizationManager.EzLocalisableString("隐藏 HUD", "Hide HUD");

        public static readonly LocalisableString LAYER_EZ_HUD = new EzLocalizationManager.EzLocalisableString("Ez HUD", "Ez HUD");

        public static readonly LocalisableString LAYER_EZ_HUD_RULESET = new EzLocalizationManager.EzLocalisableString("Ez HUD（{0}）", "Ez HUD ({0})");

        public static readonly LocalisableString LAYER_EZ_PLAYFIELD_RULESET = new EzLocalizationManager.EzLocalisableString("Ez Playfield（{0}）", "Ez Playfield ({0})");

        public static readonly LocalisableString LAYER_SKIN_HUD = new EzLocalizationManager.EzLocalisableString("Skin HUD", "Skin HUD");

        public static readonly LocalisableString LAYER_SKIN_HUD_RULESET = new EzLocalizationManager.EzLocalisableString("Skin HUD（{0}）", "Skin HUD ({0})");

        public static readonly LocalisableString LAYER_SKIN_PLAYFIELD_RULESET = new EzLocalizationManager.EzLocalisableString("Skin Playfield（{0}）", "Skin Playfield ({0})");

        /// <summary>
        /// Fallback used in place of the ruleset name when no ruleset is active yet.
        /// </summary>
        public static readonly LocalisableString LAYER_MODE_UNKNOWN = new EzLocalizationManager.EzLocalisableString("模式", "mode");

        public static readonly LocalisableString TAB_APPEARANCE = new EzLocalizationManager.EzLocalisableString("外观", "Appearance");

        public static readonly LocalisableString TAB_SIZE = new EzLocalizationManager.EzLocalisableString("尺寸", "Size");

        public static readonly LocalisableString TAB_COLOUR = new EzLocalizationManager.EzLocalisableString("颜色", "Colour");

        public static readonly LocalisableString TAB_SKIN_INI = new EzLocalizationManager.EzLocalisableString("skin.ini", "skin.ini");

        public static readonly LocalisableString TAB_SKIN_SPECIFIC = new EzLocalizationManager.EzLocalisableString("皮肤专用", "Skin-specific");

        public static readonly LocalisableString TAB_NOTE = new EzLocalizationManager.EzLocalisableString("Note", "Note");

        public static readonly LocalisableString TAB_BEATMAP_PREVIEW = new EzLocalizationManager.EzLocalisableString("谱面预览", "Beatmap preview");

        public static readonly LocalisableString TAB_EDITOR_COLOURING = new EzLocalizationManager.EzLocalisableString("配色", "Colouring");

        #endregion

        #region Sidebar groups

        public static readonly LocalisableString GROUP_TEXTURE = new EzLocalizationManager.EzLocalisableString("纹理", "Texture");

        public static readonly LocalisableString GROUP_STAGE = new EzLocalizationManager.EzLocalisableString("舞台", "Stage");

        public static readonly LocalisableString GROUP_SIZE = new EzLocalizationManager.EzLocalisableString("尺寸", "Size");

        public static readonly LocalisableString GROUP_SKIN_SPECIFIC = new EzLocalizationManager.EzLocalisableString("专用", "Specific");

        public static readonly LocalisableString GROUP_SCRIPTED_SKIN = new EzLocalizationManager.EzLocalisableString("脚本皮肤", "Scripted skin");

        public static readonly LocalisableString COLUMN_COLOURING = new EzLocalizationManager.EzLocalisableString("列配色", "Column colours");

        public static readonly LocalisableString COLUMN_TYPE = new EzLocalizationManager.EzLocalisableString("列类型", "Column type");

        public static readonly LocalisableString GROUP_BEATMAP_PREVIEW_MANIA = new EzLocalizationManager.EzLocalisableString("全谱外观", "Full-map appearance");

        public static readonly LocalisableString GROUP_GENERAL = new EzLocalizationManager.EzLocalisableString("General", "General");

        public static readonly LocalisableString GROUP_COLOURS = new EzLocalizationManager.EzLocalisableString("Colours", "Colours");

        public static readonly LocalisableString GROUP_MODE = new EzLocalizationManager.EzLocalisableString("模式", "Mode");

        public static readonly LocalisableString GROUP_NOTE_RULESET = new EzLocalizationManager.EzLocalisableString("规则集", "Ruleset");

        public static readonly LocalisableString GROUP_NOTE_EDIT = new EzLocalizationManager.EzLocalisableString("Note 编辑", "Note edit");

        public static readonly LocalisableString GROUP_BEAT_SNAP_LINES = new EzLocalizationManager.EzLocalisableString("节拍线", "Beat snap lines");

        public static readonly LocalisableString GROUP_COMPOSE_GRID_PREVIEW = new EzLocalizationManager.EzLocalisableString("网格预览", "Grid preview");

        public static readonly LocalisableString GROUP_EDITOR_DIVISOR_NOTE_COLOURING = new EzLocalizationManager.EzLocalisableString("Note 分母色", "Note divisor colours");

        public static readonly LocalisableString BEATMAP_EDITOR_COMPOSE_READONLY_WATERMARK = new EzLocalizationManager.EzLocalisableString(
            "此界面不会修改谱面",
            "This view will not modify the beatmap");

        public static LocalisableString GetBeatSnapLineDivisorLabel(int divisor) =>
            new EzLocalizationManager.EzLocalisableString($"分母 {divisor}", $"Divisor {divisor}");

        #endregion

        #region Note scene

        public static readonly LocalisableString NOTE_RULESET_LABEL = new EzLocalizationManager.EzLocalisableString("规则集", "Ruleset");

        public static readonly LocalisableString NOTE_PART_LABEL = new EzLocalizationManager.EzLocalisableString("部件类型", "Part type");

        public static readonly LocalisableString NOTE_VARIANT_LABEL = new EzLocalizationManager.EzLocalisableString("变体", "Variant");

        public static readonly LocalisableString NOTE_COMPARE_KIND_LABEL = new EzLocalizationManager.EzLocalisableString("对比类型", "Comparison type");

        public static readonly LocalisableString NOTE_COMPARE_KIND_TAP = new EzLocalizationManager.EzLocalisableString("Note", "Note");

        public static readonly LocalisableString NOTE_COMPARE_KIND_HOLD = new EzLocalizationManager.EzLocalisableString("LN", "LN");

        public static readonly LocalisableString NOTE_COLOUR_LABEL = new EzLocalizationManager.EzLocalisableString("颜色", "Colour");

        public static readonly LocalisableString NOTE_TRUE_COLOURING_LABEL = new EzLocalizationManager.EzLocalisableString("真着色", "True colouring");

        public static readonly LocalisableString NOTE_TRUE_COLOURING_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "TODO: EzNoteTrueColourProcessor",
            "TODO: EzNoteTrueColourProcessor");

        public static readonly LocalisableString NOTE_WIDTH_LABEL = new EzLocalizationManager.EzLocalisableString("宽度", "Width");

        public static readonly LocalisableString NOTE_HEIGHT_LABEL = new EzLocalizationManager.EzLocalisableString("高度", "Height");

        public static readonly LocalisableString NOTE_EXPORT_NAME_LABEL = new EzLocalizationManager.EzLocalisableString("导出名称", "Export name");

        public static readonly LocalisableString NOTE_CREATE_SNAPSHOT = new EzLocalizationManager.EzLocalisableString("创建 Note 对比快照", "Create note comparison snapshot");

        public static readonly LocalisableString NOTE_RESTORE_SNAPSHOT = new EzLocalizationManager.EzLocalisableString("恢复 Note 对比快照", "Restore note comparison snapshot");

        public static readonly LocalisableString NOTE_EXPORT_BUTTON = new EzLocalizationManager.EzLocalisableString("导出 PNG", "Export PNG");

        public static readonly LocalisableString NOTIFY_CREATED_NOTE_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "已创建 Note 对比快照",
            "Created note comparison snapshot");

        public static readonly LocalisableString NOTIFY_RESTORED_NOTE_SNAPSHOT = new EzLocalizationManager.EzLocalisableString(
            "已恢复 Note 对比快照",
            "Restored note comparison snapshot");

        public static readonly LocalisableString NOTIFY_NOTE_EXPORTED = new EzLocalizationManager.EzLocalisableString(
            "已导出 Note 预览到皮肤 _ez_note_edit 目录",
            "Exported note preview to skin _ez_note_edit folder");

        public static readonly LocalisableString NOTIFY_CANNOT_EXPORT_NOTE_PREVIEW = new EzLocalizationManager.EzLocalisableString(
            "当前皮肤无法导出 Note 预览",
            "Cannot export note preview for the current skin");

        #endregion

        #region Toolbar and preview

        public static readonly LocalisableString TOOLBAR_SWITCH_MODE = new EzLocalizationManager.EzLocalisableString("选择模式", "Select mode");

        public static readonly LocalisableString TOOLBAR_NOT_SUPPORTED = new EzLocalizationManager.EzLocalisableString(
            "尚未支持",
            "Not supported yet");

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

        public static readonly LocalisableString SIDEBAR_TAB_HUD = new EzLocalizationManager.EzLocalisableString("HUD", "HUD");

        public static readonly LocalisableString SIDEBAR_TAB_SIZE = new EzLocalizationManager.EzLocalisableString("尺寸", "Size");
        public static readonly LocalisableString SIDEBAR_HUD_PLACEHOLDER = new EzLocalizationManager.EzLocalisableString(
            "开启 HUD layer，并在预览中选中组件以编辑设置。",
            "Turn on HUD layer, then select a component in the preview to edit its settings.");

        public static readonly LocalisableString SIDEBAR_HUD_LAYER_OFF = new EzLocalizationManager.EzLocalisableString(
            "请先在场景栏选择编辑层（非「隐藏 HUD」）。",
            "Pick an editing layer in the scene bar first (anything but \"Hide HUD\").");

        public static readonly LocalisableString SIDEBAR_HUD_NO_SELECTION = new EzLocalizationManager.EzLocalisableString(
            "在预览中点击 HUD 组件以选中并显示设置。",
            "Click a HUD component in the preview to select it and show settings.");

        public static readonly LocalisableString LIBRARY_RAIL_COMPONENT = new EzLocalizationManager.EzLocalisableString(
            "组件", "Parts");

        public static readonly LocalisableString LIBRARY_RAIL_FAMILY_EZ = new EzLocalizationManager.EzLocalisableString("Ez", "Ez");
        public static readonly LocalisableString LIBRARY_RAIL_FAMILY_ARGON = new EzLocalizationManager.EzLocalisableString("Argon", "Argon");
        public static readonly LocalisableString LIBRARY_RAIL_FAMILY_LEGACY = new EzLocalizationManager.EzLocalisableString("Legacy", "Legacy");
        public static readonly LocalisableString LIBRARY_RAIL_FAMILY_OTHER = new EzLocalizationManager.EzLocalisableString("其他", "Other");

        public static LocalisableString LibraryRailFamilyName(EzHudComponentFamily family) => family switch
        {
            EzHudComponentFamily.Ez => LIBRARY_RAIL_FAMILY_EZ,
            EzHudComponentFamily.Argon => LIBRARY_RAIL_FAMILY_ARGON,
            EzHudComponentFamily.Legacy => LIBRARY_RAIL_FAMILY_LEGACY,
            _ => LIBRARY_RAIL_FAMILY_OTHER,
        };

        public static LocalisableString LibraryPanelTitle(EzHudComponentFamily family, bool rulesetScoped) => (family, rulesetScoped) switch
        {
            (EzHudComponentFamily.Ez, false) => new EzLocalizationManager.EzLocalisableString("Ez HUD 组件（通用）", "Ez HUD parts (global)"),
            (EzHudComponentFamily.Ez, true) => new EzLocalizationManager.EzLocalisableString("Ez HUD 组件（模式）", "Ez HUD parts (mode)"),
            (EzHudComponentFamily.Argon, false) => new EzLocalizationManager.EzLocalisableString("Argon HUD 组件（通用）", "Argon HUD parts (global)"),
            (EzHudComponentFamily.Argon, true) => new EzLocalizationManager.EzLocalisableString("Argon HUD 组件（模式）", "Argon HUD parts (mode)"),
            (EzHudComponentFamily.Legacy, false) => new EzLocalizationManager.EzLocalisableString("Legacy HUD 组件（通用）", "Legacy HUD parts (global)"),
            (EzHudComponentFamily.Legacy, true) => new EzLocalizationManager.EzLocalisableString("Legacy HUD 组件（模式）", "Legacy HUD parts (mode)"),
            (_, false) => new EzLocalizationManager.EzLocalisableString("其他 HUD 组件（通用）", "Other HUD parts (global)"),
            (_, true) => new EzLocalizationManager.EzLocalisableString("其他 HUD 组件（模式）", "Other HUD parts (mode)"),
        };

        public static readonly LocalisableString LIBRARY_PLACEHOLDER = new EzLocalizationManager.EzLocalisableString(
            "请先进入 HUD 场景并选择编辑层。",
            "Open a HUD scene and pick an editing layer first.");

        public static readonly LocalisableString LIBRARY_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "当前层没有可放置的组件。",
            "No components can be placed in the current layer.");

        public static readonly LocalisableString PICKER_WINDOW_TITLE = new EzLocalizationManager.EzLocalisableString(
            "可视化选择器", "Visual picker");

        public static readonly LocalisableString PICKER_WINDOW_DRAG_HINT = new EzLocalizationManager.EzLocalisableString(
            "拖动标题栏移动", "Drag the title bar");

        public static readonly LocalisableString TOOLBAR_VISUAL_PICKER = new EzLocalizationManager.EzLocalisableString(
            "可视化选择器", "Visual picker");

        public static readonly LocalisableString SIDEBAR_SKIN_SCOPED_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "当前皮肤没有专用设置。",
            "This skin has no scoped settings.");

        public static readonly LocalisableString SKIN_SPECIFIC_SCENE_PLACEHOLDER = new EzLocalizationManager.EzLocalisableString(
            "皮肤专用场景布局预留。请在主工作区侧栏「专用」中编辑设置。",
            "Skin-specific scene layout is reserved. Edit settings under the workspace 「Scoped」 sidebar tab.");

        // TODO: 谱面预览场景未实现编辑：仅做全谱预览，之后用来修改每种 note 的颜色与厚度。
        public static readonly LocalisableString BEATMAP_PREVIEW_SCENE_PLACEHOLDER = new EzLocalizationManager.EzLocalisableString(
            "谱面预览场景：插屏渲染 mania 全谱。之后用来修改每种 note 的颜色、厚度。",
            "Beatmap preview scene: renders the full mania chart in a plugged screen. Will edit each note colour and thickness later.");

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
