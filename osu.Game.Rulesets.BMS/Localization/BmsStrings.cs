// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Rulesets.BMS.Localization
{
    public static class BmsStrings
    {
        #region Settings

        public static readonly LocalisableString SETTINGS_OPEN_CAROUSEL_SONG_SELECT = new BmsLocalizationManager.BmsLocalisableString("标准 BMS 选歌（Carousel）", "Standard BMS song select (Carousel)");

        public static readonly LocalisableString SETTINGS_OPEN_RAJA_SONG_SELECT = new BmsLocalizationManager.BmsLocalisableString("Raja 风格 BMS 选歌", "Raja-style BMS song select");

        public static readonly LocalisableString SETTINGS_BUILD_ANALYTICS_DATABASE = new BmsLocalizationManager.BmsLocalisableString("构建 BMS 分析库", "Build BMS analytics database");

        public static readonly LocalisableString SETTINGS_OPEN_PATH_WIZARD = new BmsLocalizationManager.BmsLocalisableString("打开 BMS 曲库设置向导", "Open BMS library path wizard");

        public static readonly LocalisableString SETTINGS_AUTO_PRELOAD_KEYSOUNDS = new BmsLocalizationManager.BmsLocalisableString("自动预加载 Key-sound", "Auto-preload key sounds");

        public static readonly LocalisableString SETTINGS_KEY_SOUND_VOLUME = new BmsLocalizationManager.BmsLocalisableString("Key-sound 音量", "Key sound volume");

        public static readonly LocalisableString SETTINGS_DP_STAGE_SPACING = new BmsLocalizationManager.BmsLocalisableString("DP-Stage 间距", "DP stage spacing");

        public static readonly LocalisableString SETTINGS_DP_STAGE_SPACING_HINT =
            new BmsLocalizationManager.BmsLocalisableString("DP模式(10k+)，控制左右面板之间的间距。", "DP mode (10k+): spacing between left and right stages.");

        public static readonly LocalisableString SETTINGS_GAMEPLAY_ROUTE = new BmsLocalizationManager.BmsLocalisableString("Gameplay 路由", "Gameplay route");

        public static readonly LocalisableString SETTINGS_GAMEPLAY_ROUTE_HINT = new BmsLocalizationManager.BmsLocalisableString(
            "ManiaCompatibility：复用 Mania 渲染与判定（推荐）。BmsNative：使用 BMS 原生流水线（实验性）。",
            "ManiaCompatibility: reuse Mania rendering and judgement (recommended). BmsNative: experimental native BMS pipeline.");

        public static readonly LocalisableString SETTINGS_MANIA_SCROLL_NOTE = new BmsLocalizationManager.BmsLocalisableString("BMS 复用 mania 设置及快捷键（含 LAlt 加速步进）。",
            "BMS reuses mania scroll settings and shortcuts (including LAlt fast step).");

        public static readonly LocalisableString SETTINGS_SCANNING_NOTE = new BmsLocalizationManager.BmsLocalisableString("正在扫描...", "Scanning...");

        public static readonly LocalisableString SETTINGS_CANNOT_OPEN_SONG_SELECT =
            new BmsLocalizationManager.BmsLocalisableString("无法打开 BMS 选歌界面（未找到屏幕导航器）。", "Cannot open BMS song select (screen runner not found).");

        public static readonly LocalisableString SETTINGS_CANNOT_OPEN_WIZARD = new BmsLocalizationManager.BmsLocalisableString("无法打开向导（未找到屏幕导航器）。", "Cannot open wizard (screen runner not found).");

        public static readonly LocalisableString SETTINGS_ADD_VALID_PATH_FIRST =
            new BmsLocalizationManager.BmsLocalisableString("请先在向导中添加至少一个有效的 BMS 文件夹路径", "Add at least one valid BMS folder path in the wizard first.");

        public static readonly LocalisableString SETTINGS_SCANNING_LIBRARY = new BmsLocalizationManager.BmsLocalisableString("正在扫描 BMS 歌曲库...", "Scanning BMS library...");

        public static readonly LocalisableString SETTINGS_SCAN_COMPLETE = new BmsLocalizationManager.BmsLocalisableString("BMS 歌曲库扫描完成!", "BMS library scan complete!");

        public static readonly LocalisableString SETTINGS_DEFAULT_COLLECTION_NAME = new BmsLocalizationManager.BmsLocalisableString("BMS Collection", "BMS Collection");

        public static string Settings_CachedStatus(int songs, int charts) => settings_cached_status_template.Format(songs, charts);

        public static string Settings_ScanCompleteStatus(int songs, int charts) => settings_scan_complete_status_template.Format(songs, charts);

        public static string Settings_ScanFailedStatus(string message) => settings_scan_failed_status_template.Format(message);

        public static string Settings_CollectionsCreated(int pathCount, int chartCount) => settings_collections_created_template.Format(pathCount, chartCount);

        private static readonly BmsLocalizationManager.BmsLocalisableString settings_cached_status_template =
            new BmsLocalizationManager.BmsLocalisableString("已缓存 {0} 首歌曲, {1} 张谱面", "Cached {0} songs, {1} charts");

        private static readonly BmsLocalizationManager.BmsLocalisableString settings_scan_complete_status_template =
            new BmsLocalizationManager.BmsLocalisableString("扫描完成! {0} 首歌曲, {1} 张谱面", "Scan complete: {0} songs, {1} charts");

        private static readonly BmsLocalizationManager.BmsLocalisableString settings_scan_failed_status_template = new BmsLocalizationManager.BmsLocalisableString("扫描失败: {0}", "Scan failed: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString settings_collections_created_template = new BmsLocalizationManager.BmsLocalisableString("已为 {0} 个路径创建收藏夹，共收藏 {1} 张谱面",
            "Created collections for {0} path(s), {1} chart(s) added.");

        #endregion

        #region Path wizard

        public static readonly LocalisableString PATH_WIZARD_TITLE = new BmsLocalizationManager.BmsLocalisableString("BMS 曲库设置向导", "BMS library path wizard");

        public static readonly LocalisableString PATH_WIZARD_TOOLTIP = new BmsLocalizationManager.BmsLocalisableString("BMS 曲库路径设置向导 - 用于配置 BMS 文件的扫描目录", "Configure folders scanned for BMS charts.");

        public static readonly LocalisableString PATH_WIZARD_INTRO = new BmsLocalizationManager.BmsLocalisableString(
            "先添加任意数量的文件夹路径。应用会立即保存并重建扫描，确定只关闭当前向导。",
            "Add one or more folder paths. Changes are saved and rescanned immediately; OK only closes this wizard.");

        public static readonly LocalisableString PATH_WIZARD_INTRO_TOOLTIP = new BmsLocalizationManager.BmsLocalisableString(
            "在此处选择包含 BMS 文件的文件夹，然后点击'添加当前路径'按钮将其添加到曲库列表中。您可以添加多个文件夹，每个文件夹都会被扫描以查找 BMS 文件。",
            "Pick folders containing BMS files, then click Add current path. Multiple folders are supported.");

        public static readonly LocalisableString PATH_WIZARD_ADD_CURRENT_PATH = new BmsLocalizationManager.BmsLocalisableString("添加当前路径", "Add current path");

        public static readonly LocalisableString PATH_WIZARD_CLEAR_LIST = new BmsLocalizationManager.BmsLocalisableString("清空列表", "Clear list");

        public static readonly LocalisableString PATH_WIZARD_ADDED_PATHS_HEADER = new BmsLocalizationManager.BmsLocalisableString("已添加的路径", "Added paths");

        public static readonly LocalisableString PATH_WIZARD_CONFIRM = new BmsLocalizationManager.BmsLocalisableString("确定", "OK");

        public static readonly LocalisableString PATH_WIZARD_APPLY = new BmsLocalizationManager.BmsLocalisableString("应用", "Apply");

        public static readonly LocalisableString PATH_WIZARD_NO_PATHS_YET = new BmsLocalizationManager.BmsLocalisableString("暂未添加路径。", "No paths added yet.");

        public static readonly LocalisableString PATH_WIZARD_REMOVE = new BmsLocalizationManager.BmsLocalisableString("移除", "Remove");

        public static readonly LocalisableString PATH_WIZARD_REMOVE_DIALOG_HEADER = new BmsLocalizationManager.BmsLocalisableString("移除曲库路径", "Remove library path");

        public static string PathWizard_RemoveDialogBody(string path) => path_wizard_remove_dialog_body_template.Format(path);

        private static readonly BmsLocalizationManager.BmsLocalisableString path_wizard_remove_dialog_body_template =
            new BmsLocalizationManager.BmsLocalisableString("该操作将从列表移除以下路径：\n{0}", "This will remove the following path from the list:\n{0}");

        #endregion

        #region Song select (shared)

        public static readonly LocalisableString SONG_SELECT_REFRESH_LIBRARY = new BmsLocalizationManager.BmsLocalisableString("刷新曲库", "Refresh library");

        public static readonly LocalisableString SONG_SELECT_BUILD_ANALYTICS_SHORT = new BmsLocalizationManager.BmsLocalisableString("构建分析库", "Build analytics");

        public static readonly LocalisableString SONG_SELECT_BACK = new BmsLocalizationManager.BmsLocalisableString("返回", "Back");

        public static readonly LocalisableString SONG_SELECT_ADD_LIBRARY_PATH_FIRST =
            new BmsLocalizationManager.BmsLocalisableString("请先在 BMS 设置中添加曲库路径", "Add BMS library paths in Settings -> BMS first.");

        public static readonly LocalisableString SONG_SELECT_SCANNING_LIBRARY = new BmsLocalizationManager.BmsLocalisableString("正在扫描 BMS 曲库...", "Scanning BMS library...");

        public static readonly LocalisableString SONG_SELECT_SELECT_CHART_FIRST = new BmsLocalizationManager.BmsLocalisableString("请先选择一个 BMS 谱面", "Select a BMS chart first.");

        public static readonly LocalisableString SONG_SELECT_SELECT_CHART_TO_PLAY = new BmsLocalizationManager.BmsLocalisableString("请选择一个 BMS 谱面", "Select a BMS chart.");

        public static readonly LocalisableString SONG_SELECT_EMPTY_INDEX = new BmsLocalizationManager.BmsLocalisableString(
            "BMS 曲库索引为空，请在设置中添加路径或点「刷新曲库」扫描",
            "BMS library index is empty. Add paths in settings or tap Refresh library.");

        public static readonly LocalisableString SONG_SELECT_SOURCE_FILE_NOT_FOUND =
            new BmsLocalizationManager.BmsLocalisableString("未能定位 BMS 源文件，请刷新曲库", "Could not locate BMS source file. Refresh library.");

        public static string SongSelect_RefreshFailed(string message) => song_select_refresh_failed_template.Format(message);

        public static string SongSelect_LoadBeatmapFailed(string message) => song_select_load_beatmap_failed_template.Format(message);

        private static readonly BmsLocalizationManager.BmsLocalisableString song_select_refresh_failed_template = new BmsLocalizationManager.BmsLocalisableString("刷新失败：{0}", "Refresh failed: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString song_select_load_beatmap_failed_template =
            new BmsLocalizationManager.BmsLocalisableString("加载谱面失败：{0}", "Failed to load beatmap: {0}");

        #endregion

        #region Raja song select UI

        public static readonly LocalisableString RAJA_SEARCH_PLACEHOLDER = new BmsLocalizationManager.BmsLocalisableString("搜索曲目 (Enter)", "Search charts (Enter)");

        public static readonly LocalisableString RAJA_ROOT_LABEL = new BmsLocalizationManager.BmsLocalisableString("ROOT", "ROOT");

        public static readonly LocalisableString RAJA_KEY_HINTS = new BmsLocalizationManager.BmsLocalisableString(
            "↑↓ 移动 | Enter 进入/开始 | Esc/← 返回 | 1=KEY 2=排序 8=同文件夹",
            "↑↓ Move | Enter open/play | Esc/← Back | 1=KEY 2=Sort 8=Same folder");

        public static readonly LocalisableString RAJA_SELECTION_DETAIL = new BmsLocalizationManager.BmsLocalisableString("选中项详情", "Selection details");

        public static readonly LocalisableString RAJA_PLACEHOLDER_DASH = new BmsLocalizationManager.BmsLocalisableString("—", "—");

        public static readonly LocalisableString RAJA_SELECT_CHART_FOR_ANALYTICS = new BmsLocalizationManager.BmsLocalisableString("选择曲目以预览分析数据", "Select a chart to preview analytics");

        public static readonly LocalisableString RAJA_EMPTY_LIST = new BmsLocalizationManager.BmsLocalisableString("(空)", "(empty)");

        public static readonly LocalisableString RAJA_DIRECTORY_ENTER_HINT = new BmsLocalizationManager.BmsLocalisableString("目录 — 按 Enter 进入", "Folder — press Enter to open");

        public static readonly LocalisableString RAJA_ANALYTICS_NONE = new BmsLocalizationManager.BmsLocalisableString("分析: —", "Analytics: —");

        public static readonly LocalisableString RAJA_SAME_FOLDER_FILTER_TITLE = new BmsLocalizationManager.BmsLocalisableString("SAME FOLDER", "SAME FOLDER");

        public static string Raja_DetailArtist(string artist) => raja_detail_artist_template.Format(artist);

        public static string Raja_DetailLevel(int level, int keyCount, string fileName) => raja_detail_level_template.Format(level, keyCount, fileName);

        public static string Raja_DetailBpm(double bpm, int notes) => raja_detail_bpm_template.Format(bpm, notes);

        public static string Raja_DetailPath(string path) => raja_detail_path_template.Format(path);

        public static string Raja_DetailAnalytics(double pp, double xxySr, double avgKps, double maxKps, double star) => raja_detail_analytics_template.Format(pp, xxySr, avgKps, maxKps, star);

        public static string Raja_RowAnalyticsSuffix(double pp, double xxySr, double avgKps) => raja_row_analytics_suffix_template.Format(pp, xxySr, avgKps);

        private static readonly BmsLocalizationManager.BmsLocalisableString raja_detail_artist_template = new BmsLocalizationManager.BmsLocalisableString("艺术家: {0}", "Artist: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString raja_detail_level_template =
            new BmsLocalizationManager.BmsLocalisableString("等级: Lv.{0} | {1}K | {2}", "Level: Lv.{0} | {1}K | {2}");

        private static readonly BmsLocalizationManager.BmsLocalisableString raja_detail_bpm_template = new BmsLocalizationManager.BmsLocalisableString("BPM: {0:0.#} | 音符: {1}",
            "BPM: {0:0.#} | Notes: {1}");

        private static readonly BmsLocalizationManager.BmsLocalisableString raja_detail_path_template = new BmsLocalizationManager.BmsLocalisableString("路径: {0}", "Path: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString raja_detail_analytics_template =
            new BmsLocalizationManager.BmsLocalisableString("PP {0:0.#} | xxySR {1:0.##} | KPS {2:0.#}/{3:0.#} | ★ {4:0.##}", "PP {0:0.#} | xxySR {1:0.##} | KPS {2:0.#}/{3:0.#} | ★ {4:0.##}");

        private static readonly BmsLocalizationManager.BmsLocalisableString raja_row_analytics_suffix_template =
            new BmsLocalizationManager.BmsLocalisableString("PP{0:0.#} SR{1:0.##} KPS{2:0.#}", "PP{0:0.#} SR{1:0.##} KPS{2:0.#}");

        #endregion

        #region Analytics

        public static readonly LocalisableString ANALYTICS_LIBRARY_EMPTY = new BmsLocalizationManager.BmsLocalisableString("BMS 曲库为空，请先扫描曲库", "BMS library is empty. Scan the library first.");

        public static readonly LocalisableString ANALYTICS_BUILDING = new BmsLocalizationManager.BmsLocalisableString("正在构建 BMS 分析库...", "Building BMS analytics database...");

        public static readonly LocalisableString ANALYTICS_BUILD_COMPLETE = new BmsLocalizationManager.BmsLocalisableString("BMS 分析库构建完成", "BMS analytics database build complete");

        public static readonly LocalisableString ANALYTICS_CANCELLED = new BmsLocalizationManager.BmsLocalisableString("BMS 分析已取消", "BMS analytics cancelled");

        public static readonly LocalisableString ANALYTICS_PREPARING = new BmsLocalizationManager.BmsLocalisableString("准备分析…", "Preparing analytics…");

        public static readonly LocalisableString ANALYTICS_COMPLETE = new BmsLocalizationManager.BmsLocalisableString("分析完成", "Analytics complete");

        public static readonly LocalisableString ANALYTICS_CANCELLED_SHORT = new BmsLocalizationManager.BmsLocalisableString("分析已取消", "Analytics cancelled");

        public static string Analytics_BuildFailed(string message) => analytics_build_failed_template.Format(message);

        public static string Analytics_ChartStarted(int index, int total, string title) => analytics_chart_started_template.Format(index, total, title);

        public static string Analytics_ChartFinished(int index, int total, string title) => analytics_chart_finished_template.Format(index, total, title);

        public static string Analytics_ChartParsing(int index, int total, string title) => analytics_chart_parsing_template.Format(index, total, title);

        private static readonly BmsLocalizationManager.BmsLocalisableString analytics_build_failed_template =
            new BmsLocalizationManager.BmsLocalisableString("分析库构建失败：{0}", "Analytics build failed: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString analytics_chart_started_template =
            new BmsLocalizationManager.BmsLocalisableString("[{0}/{1}] 开始: {2}", "[{0}/{1}] Started: {2}");

        private static readonly BmsLocalizationManager.BmsLocalisableString analytics_chart_finished_template =
            new BmsLocalizationManager.BmsLocalisableString("[{0}/{1}] 完成: {2}", "[{0}/{1}] Done: {2}");

        private static readonly BmsLocalizationManager.BmsLocalisableString analytics_chart_parsing_template =
            new BmsLocalizationManager.BmsLocalisableString("[{0}/{1}] 解析中: {2}", "[{0}/{1}] Parsing: {2}");

        #endregion

        #region Library scan / import

        public static readonly LocalisableString SCAN_SCANNING_FOLDERS = new BmsLocalizationManager.BmsLocalisableString("正在扫描文件夹...", "Scanning folders...");

        public static readonly LocalisableString SCAN_NO_VALID_PATHS = new BmsLocalizationManager.BmsLocalisableString("错误: 没有可用的路径", "Error: no valid paths");

        public static readonly LocalisableString SCAN_NO_BMS_FILES = new BmsLocalizationManager.BmsLocalisableString("未找到 BMS 文件", "No BMS files found");

        public static string Scan_FoundFiles(int count) => scan_found_files_template.Format(count);

        public static string Scan_ParsingFolders(int done, int total) => scan_parsing_folders_template.Format(done, total);

        public static string Scan_Complete(int songs, int charts) => scan_complete_template.Format(songs, charts);

        public static readonly LocalisableString SCAN_CANCELLED = new BmsLocalizationManager.BmsLocalisableString("扫描已取消", "Scan cancelled");

        public static string Scan_Error(string message) => scan_error_template.Format(message);

        public static string Scan_LoadedFromIndex(int songs, int charts) => scan_loaded_from_index_template.Format(songs, charts);

        public static readonly LocalisableString IMPORT_INDEXING = new BmsLocalizationManager.BmsLocalisableString("正在索引 BMS 曲库...", "Indexing BMS library...");

        public static readonly LocalisableString IMPORT_WRITING_CATALOG = new BmsLocalizationManager.BmsLocalisableString("正在写入 osu 曲库...", "Writing osu library catalog...");

        public static string Import_Complete(int songs, int charts) => import_complete_template.Format(songs, charts);

        private static readonly BmsLocalizationManager.BmsLocalisableString scan_found_files_template = new BmsLocalizationManager.BmsLocalisableString("找到 {0} 个 BMS 文件，正在解析...",
            "Found {0} BMS file(s), parsing...");

        private static readonly BmsLocalizationManager.BmsLocalisableString scan_parsing_folders_template =
            new BmsLocalizationManager.BmsLocalisableString("正在解析... {0}/{1} 文件夹", "Parsing... {0}/{1} folders");

        private static readonly BmsLocalizationManager.BmsLocalisableString scan_complete_template = new BmsLocalizationManager.BmsLocalisableString("扫描完成: {0} 首歌曲, {1} 张谱面",
            "Scan complete: {0} songs, {1} charts");

        private static readonly BmsLocalizationManager.BmsLocalisableString scan_error_template = new BmsLocalizationManager.BmsLocalisableString("扫描错误: {0}", "Scan error: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString scan_loaded_from_index_template =
            new BmsLocalizationManager.BmsLocalisableString("已加载 {0} 首歌曲, {1} 张谱面", "Loaded {0} songs, {1} charts");

        private static readonly BmsLocalizationManager.BmsLocalisableString import_complete_template = new BmsLocalizationManager.BmsLocalisableString("完成: {0} 首歌曲, {1} 张谱面",
            "Done: {0} songs, {1} charts");

        #endregion

        #region Player loader

        public static readonly LocalisableString LOADER_LOADING = new BmsLocalizationManager.BmsLocalisableString("加载中...", "Loading...");

        public static readonly LocalisableString LOADER_PARSING_BMS_FILE = new BmsLocalizationManager.BmsLocalisableString("正在解析 BMS 文件...", "Parsing BMS file...");

        public static readonly LocalisableString LOADER_PARSING_BEATMAP = new BmsLocalizationManager.BmsLocalisableString("正在解析谱面...", "Parsing beatmap...");

        public static readonly LocalisableString LOADER_LOAD_FAILED = new BmsLocalizationManager.BmsLocalisableString("错误: 谱面加载失败或没有音符", "Error: failed to load beatmap or chart has no notes");

        public static string Loader_LoadComplete(int noteCount) => loader_load_complete_template.Format(noteCount);

        public static string Loader_LoadError(string message) => loader_load_error_template.Format(message);

        public static string Loader_LaunchFailed(string message) => loader_launch_failed_template.Format(message);

        private static readonly BmsLocalizationManager.BmsLocalisableString loader_load_complete_template = new BmsLocalizationManager.BmsLocalisableString("加载完成! {0} 个音符", "Load complete! {0} notes");

        private static readonly BmsLocalizationManager.BmsLocalisableString loader_load_error_template = new BmsLocalizationManager.BmsLocalisableString("加载失败: {0}", "Load failed: {0}");

        private static readonly BmsLocalizationManager.BmsLocalisableString loader_launch_failed_template =
            new BmsLocalizationManager.BmsLocalisableString("启动游戏失败: {0}", "Failed to start gameplay: {0}");

        #endregion
    }
}
