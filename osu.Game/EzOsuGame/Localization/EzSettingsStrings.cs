// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzSettingsStrings
    {
        #region 分散设置

        public static readonly EzLocalizationManager.EzLocalisableString SCALING_GAME_MODE = new EzLocalizationManager.EzLocalisableString("缩放游戏模式", "Scaling Game Mode");

        public static readonly EzLocalizationManager.EzLocalisableString SCALING_GAME_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "缩放游戏模式会根据当前游戏模式自动调整界面元素的大小。"
            + "\n开启后，界面元素会根据当前游戏模式进行缩放，以提供更适合的视觉体验。"
            + "\n例如，在Mania模式下，界面元素会缩小以适应更多的列数，而在其他模式下则保持默认大小。",
            "The Scaling Game Mode will automatically adjust the size of UI elements based on the current game mode."
            + "\nWhen enabled, UI elements will be scaled according to the current game mode to provide a more suitable visual experience."
            + "\nFor example, in Mania mode, UI elements will be scaled down to accommodate more columns, while in other modes they will remain at their default size.");

        public static readonly EzLocalizationManager.EzLocalisableString DISABLE_CMD_SPACE = new EzLocalizationManager.EzLocalisableString("游戏时禁用 Cmd+Space (聚焦搜索) ",
            "Disable Cmd+Space (Spotlight) during gameplay");

        public static readonly EzLocalizationManager.EzLocalisableString STORAGE_FOLDER_CREATED = new EzLocalizationManager.EzLocalisableString("已创建目录：{0}\n请将文件放入该目录",
            "Created folder: {0}\nAdd files to the folder");

        public static readonly EzLocalizationManager.EzLocalisableString STORAGE_FOLDER_EMPTY = new EzLocalizationManager.EzLocalisableString("目录为空：{0}", "Folder is empty: {0}");

        public static readonly EzLocalizationManager.EzLocalisableString SETTINGS_TITLE = new EzLocalizationManager.EzLocalisableString("设置", "Settings");
        public static readonly EzLocalizationManager.EzLocalisableString SAVE_BUTTON = new EzLocalizationManager.EzLocalisableString("保存", "Save");
        public static readonly EzLocalizationManager.EzLocalisableString CANCEL_BUTTON = new EzLocalizationManager.EzLocalisableString("取消", "Cancel");

        #endregion

        public static readonly EzLocalizationManager.EzLocalisableString EZ_GAME_SECTION_HEADER = new EzLocalizationManager.EzLocalisableString("Ez游玩设置", "Ez Gameplay");
        public static readonly EzLocalizationManager.EzLocalisableString EZ_UI_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("Ez 界面设置", "Ez UI Settings");

        public static readonly EzLocalizationManager.EzLocalisableString FRAME_LIMITER_BASE =
            new EzLocalizationManager.EzLocalisableString("帧率基数", "Frame limiter");

        public static readonly EzLocalizationManager.EzLocalisableString FRAME_LIMITER_BASE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Update / Draw 帧率限制中 Nx 倍率的基础值。首次使用时会自动填入当前显示器刷新率；之后可自定义（例如设为 200 且选择 2x 时，上限为 400 Hz）。",
            "Base value for Nx frame limiters on Update and Draw. On first use, the current display refresh rate is applied; afterwards you can customise it (e.g. 200 with 2x gives a 400 Hz cap).");

        public static readonly EzLocalizationManager.EzLocalisableString UPDATE_FRAME_LIMITER =
            new EzLocalizationManager.EzLocalisableString("Update 帧率限制", "Update frame limiter");

        public static readonly EzLocalizationManager.EzLocalisableString UPDATE_FRAME_LIMITER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "限制游戏逻辑更新（Update）线程的最高帧率。Draw 帧率仍由图形设置中的「帧率限制」控制。",
            "Limits the maximum update rate of the game logic thread. Draw frame rate is still controlled by the \"Frame limiter\" setting in Graphics.");

        public static readonly EzLocalizationManager.EzLocalisableString HIDE_MAIN_MENU_ONLINE_BANNER =
            new EzLocalizationManager.EzLocalisableString("屏蔽主界面底部新闻广告", "Hide main menu bottom news banner");

        public static readonly EzLocalizationManager.EzLocalisableString HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后将隐藏主界面底部的在线新闻/广告轮播图。",
            "When enabled, the online news/advertisement banner at the bottom of the main menu will be hidden.");

        public static readonly EzLocalizationManager.EzLocalisableString ACRYLIC_UI_ENABLED =
            new EzLocalizationManager.EzLocalisableString("毛玻璃 UI", "Acrylic UI");

        public static readonly EzLocalizationManager.EzLocalisableString ACRYLIC_UI_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，选歌界面的 Wedge、过滤器、谱面预览等面板背景将使用穿透虚化（采样当前屏幕内容）。"
            + "\n会启用全屏 capture 缓冲，仅建议在选歌界面使用。",
            "When enabled, song select wedge, filter, and beatmap preview panel backgrounds use true backdrop acrylic blur."
            + "\nEnables a full-screen capture buffer while any acrylic panel is visible on song select.");

        public static readonly EzLocalizationManager.EzLocalisableString ACRYLIC_UI_BLUR_STRENGTH =
            new EzLocalizationManager.EzLocalisableString("毛玻璃虚化强度", "Acrylic blur strength");

        public static readonly EzLocalizationManager.EzLocalisableString ACRYLIC_UI_BLUR_STRENGTH_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "选歌界面毛玻璃面板的模糊强度。总开关关闭时不生效。",
            "Blur strength for song select acrylic panels. Has no effect when acrylic UI is disabled.");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFICATION_BEHAVIOUR =
            new EzLocalizationManager.EzLocalisableString("通知行为", "Notification behaviour");

        public static readonly EzLocalizationManager.EzLocalisableString NOTIFICATION_BEHAVIOUR_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "控制游戏内通知弹窗与提示音。"
            + "\n正常：与 osu! lazer 默认行为一致。"
            + "\n游戏内聚焦：进入谱面游玩（含加载与暂停）后屏蔽一切通知弹窗与提示音。"
            + "\n永不通知：在任何时候均不显示通知弹窗或播放提示音。",
            "Controls in-game notification toasts and sounds."
            + "\nNormal: same as default osu! lazer behaviour."
            + "\nIn-game focus: suppress all notification toasts and sounds while playing a beatmap (including load and pause)."
            + "\nNever notify: suppress notification toasts and sounds at all times.");

        public static readonly LocalisableString SCREENSHOT_ACTION =
            new EzLocalizationManager.EzLocalisableString("截图行为", "Screenshot action");

        public static readonly LocalisableString SCREENSHOT_COPIED_TO_CLIPBOARD =
            new EzLocalizationManager.EzLocalisableString("截图已复制到剪贴板！", "Screenshot copied to clipboard!");

        public static LocalisableString ScreenshotSaved(string filename) =>
            new EzLocalizationManager.EzLocalisableString($"截图已保存！点此处查看：\n{filename}", $"Screenshot saved! Click to view.\n{filename}");

        public static readonly EzLocalizationManager.EzLocalisableString STORYBOARD_VIDEO_AUTO_SIZE =
            new EzLocalizationManager.EzLocalisableString("故事板视频自适应填满", "Storyboard video auto-size to fill");

        public static readonly EzLocalizationManager.EzLocalisableString STORYBOARD_VIDEO_AUTO_SIZE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后故事板视频将自动调整大小以填满整个故事板区域，可能会裁剪部分画面但能更好地适应不同分辨率和屏幕比例。",
            "When enabled, storyboard videos will automatically adjust their size to fill the entire storyboard area, "
            + "which may crop some of the video but will better adapt to different resolutions and screen ratios.");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析重算", "Enable Ez analysis recomputation");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，选歌界面会像官方星级一样按当前 Mod 按需重算 xxy/PP/KPS 并刷新面板。"
            + "\n关闭时，有 Mod 仍显示 Realm 无 Mod 基线。不控制 SQLite 与 Realm 回填。",
            "When enabled, song select recomputes xxy/PP/KPS for the current mods on demand (like official star rating) and refreshes panels."
            + "\nWhen disabled, mods still show the NoMod Realm baseline. Does not control SQLite or Realm backfill.");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析本地数据（SQLite）", "Enable Ez analysis local data (SQLite)");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "控制主 analysis SQLite（kps/KPC 缓存）与分支曲库（预生成 Mod 快照）的读写。"
            + "\n仅在缺少当前版本文件或需要 schema 升级时自动预热；已有匹配文件时请在下方的维护控件手动补算/重算。",
            "Controls main analysis SQLite (kps/KPC cache) and songs branch libraries (precomputed mod snapshots)."
            + "\nAuto-warmup runs only when the current database is missing or needs schema upgrade; use maintenance controls below when a matching file already exists.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_TARGET =
            new EzLocalizationManager.EzLocalisableString("数据维护目标", "Data maintenance target");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_TARGET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "选择要维护的数据范围。SQLite 在已有匹配的最新版文件时不会自动预热；Realm 缺失项仍会在启动时自动补算。",
            "Choose which data to maintain. SQLite is not auto-warmed when a matching current database exists; Realm missing values are still filled at startup.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_EXECUTE =
            new EzLocalizationManager.EzLocalisableString("执行", "Execute");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_EXECUTE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "确认后可选补算缺失或完全重算。大库可能耗时较长，请留意右上角进度通知。",
            "Choose backfill missing or force full rebuild. Large libraries may take a while; watch the progress notification in the top-right corner.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_DIALOG_HEADER =
            new EzLocalizationManager.EzLocalisableString("数据维护", "Data maintenance");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_DIALOG_BODY = new EzLocalizationManager.EzLocalisableString(
            "即将对所选目标执行后台维护。\n补算缺失仅处理未写入的数据；完全重算会先清除已有结果再全部重算。",
            "Background maintenance will run for the selected target.\nBackfill missing only fills gaps; force rebuild clears existing results first.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_DIALOG_BACKFILL =
            new EzLocalizationManager.EzLocalisableString("尝试补算", "Backfill missing");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_DIALOG_FORCE =
            new EzLocalizationManager.EzLocalisableString("完全重算", "Force rebuild");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_UNAVAILABLE = new EzLocalizationManager.EzLocalisableString(
            "无法执行数据维护：所需后台处理器不可用。",
            "Cannot run data maintenance: the required background processor is unavailable.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_ALREADY_RUNNING = new EzLocalizationManager.EzLocalisableString(
            "数据维护已在后台运行，请等待当前任务完成。",
            "Data maintenance is already running in the background. Wait for the current task to finish.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_SQLITE_DISABLED = new EzLocalizationManager.EzLocalisableString(
            "SQLite 分析缓存已关闭，请在上方启用后再执行。",
            "SQLite analysis cache is disabled. Enable it above before running this action.");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_DIALOG_UNAVAILABLE = new EzLocalizationManager.EzLocalisableString(
            "无法打开确认对话框，请稍后重试。",
            "Cannot open the confirmation dialog. Try again later.");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_REALM_METADATA_BACKFILL =
            new EzLocalizationManager.EzLocalisableString("补算 Realm 元数据（Tag / XxySR / PP）", "Backfill Realm metadata (Tag / XxySR / PP)");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_REALM_METADATA_BACKFILL_BUTTON =
            new EzLocalizationManager.EzLocalisableString("立即补算", "Backfill now");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_REALM_METADATA_BACKFILL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "在后台补算选歌面板所需的 Realm 字段：谱面 Tag（视频/Storyboard）、Xxy 星级、PP。"
            + "\n启动时也会自动执行；若看不到进度通知，可点此手动触发。",
            "Backfill Realm fields used by song select: beatmap tags (video/storyboard), Xxy star rating, and PP."
            + "\nAlso runs automatically at startup; use this if progress notifications did not appear.");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_REALM_METADATA_BACKFILL_FORCE =
            new EzLocalizationManager.EzLocalisableString("强制全部重算 Realm 元数据", "Force full Realm metadata recalculation");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_REALM_METADATA_BACKFILL_FORCE_BUTTON =
            new EzLocalizationManager.EzLocalisableString("强制重算", "Force recalculate");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_REALM_METADATA_BACKFILL_FORCE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "先将所有谱面的 Tag / XxySR / PP 标记为未计算，再执行完整补算。"
            + "\n谱面较多时耗时较长，请留意右上角进度通知。",
            "Marks all beatmaps' Tag / XxySR / PP as uncomputed, then runs a full backfill."
            + "\nMay take a long time for large libraries; watch the progress notification in the top-right corner.");

        #region 机制类

        public static readonly EzLocalizationManager.EzLocalisableString EZ_GAME_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("Ez Mania 设置", "Ez Mania Settings");

        public static readonly EzLocalizationManager.EzLocalisableString SCORE_SUBMIT_WARNING = new EzLocalizationManager.EzLocalisableString("当前已锁定成绩上传", "Currently locked score submission");
        public static readonly EzLocalizationManager.EzLocalisableString ACCURACY_CUTOFF_S = new EzLocalizationManager.EzLocalisableString("Acc S评级线(Mania)", "Accuracy Cutoff S (Mania)");
        public static readonly EzLocalizationManager.EzLocalisableString ACCURACY_CUTOFF_A = new EzLocalizationManager.EzLocalisableString("Acc A评级线(Mania)", "Accuracy Cutoff A (Mania)");

        public static readonly EzLocalizationManager.EzLocalisableString OFFSET_PLUS_MANIA = new EzLocalizationManager.EzLocalisableString("高阶Offset修正(Mania)", "Advanced Offset Plus (Mania)");

        public static readonly EzLocalizationManager.EzLocalisableString OFFSET_PLUS_MANIA_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "直接修正输入结果的偏移值, 不改变音频、谱面的时间轴。"
            + "\n可以根绝所有输入延迟。（测试性功能！锁定成绩上传）",
            "Directly correct the offset value of input results without changing the timeline of audio and beatmap."
            + "\nCan be adjusted for all input delays. (Testing feature! Lock score upload)");

        public static readonly EzLocalizationManager.EzLocalisableString
            OFFSET_PLUS_NON_MANIA = new EzLocalizationManager.EzLocalisableString("高阶Offset修正(非Mania)", "Advanced Offset Plus (Non-Mania)");

        public static readonly EzLocalizationManager.EzLocalisableString OFFSET_PLUS_NON_MANIA_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "直接修正输入结果的偏移值, 不改变音频、谱面的时间轴。"
            + "\n可以根绝所有输入延迟。（测试性功能！锁定成绩上传）",
            "Directly correct the offset value of input results without changing the timeline of audio and beatmap."
            + "\nCan be adjusted for all input delays. (Testing feature! Lock score upload)");

        public static readonly EzLocalizationManager.EzLocalisableString HIT_OBJECT_LIFETIME_USES_OWN_TIME = new EzLocalizationManager.EzLocalisableString("以物件自身时间结束生命周期", "Use hit object's own time for lifetime");

        public static readonly EzLocalizationManager.EzLocalisableString HIT_OBJECT_LIFETIME_USES_OWN_TIME_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，HitObject 的命中后状态变换与生命周期结束将以物件自身时间为基准，而不是以实际交互判定时间为基准。"
            + "\nnote 会固定在判定线上消失，对所有模式生效。",
            "When enabled, hit-state transforms and hit object lifetime end will use the hit object's own time instead of the actual judged interaction time."
            + "\nNotes will disappear consistently at the judgement line. Applies to all game modes.");

        public static readonly LocalisableString SKIP_EMPTY_EDGE_COLUMNS = new EzLocalizationManager.EzLocalisableString("使用Ez2Ac 10k2s1p", "Use Ez2Ac 10k2s1p");

        public static readonly LocalisableString SKIP_EMPTY_EDGE_COLUMNS_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，14k谱面按13k显示（跳过最后一列），用于游玩Ez2Ac街机谱面（最后一列为空）。"
            + "\n若最后一列有音符，请关闭此选项。",
            "When enabled, 14K beatmaps display as 13K (last column hidden) for Ez2Ac arcade maps with an empty last column."
            + "\nDisable this if the last column contains notes.");

        public static readonly LocalisableString SKIP_WITH_GAMEPLAY_KEYS = new EzLocalizationManager.EzLocalisableString(
            "跳过可由游戏按键触发",
            "Allow gameplay keys to trigger skip");

        public static readonly LocalisableString SKIP_WITH_GAMEPLAY_KEYS_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后，在可跳过阶段按下当前模式的游玩按键也会触发跳过。"
            + "\n会读取你当前规则集与变体（如 mania 几 K）的实际键位设置。",
            "When enabled, pressing gameplay keys for the current mode also triggers skip during skippable periods."
            + "\nUses your current ruleset/variant key bindings (for example, mania key count bindings).");

        // public static readonly LocalisableString SKIP_EMPTY_EDGE_COLUMNS = new EzLocalizationManager.EzLocalisableString("跳过空边缘列", "Skip Empty Edge Columns");
        //
        // public static readonly LocalisableString SKIP_EMPTY_EDGE_COLUMNS_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
        //     "开启后，如果谱面第一列或最后一列是空的（没有音符），则实际加载时跳过这些空列，不显示它们。\n"
        //     + "主要面向伪14k的13k谱面，隐藏空列按实际结果显示。",
        //     "When enabled, if the first or last column of the beatmap is empty (no notes), those empty columns will be skipped during loading and not displayed.\n"
        //     + "This fixes the issue where the last column of some 14K beatmaps was incorrectly hidden when it actually had content.");

        public static readonly LocalisableString HIT_MODE = new EzLocalizationManager.EzLocalisableString("Mania 判定系统", "Mania Hit Mode");

        public static readonly LocalisableString HIT_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            @"
|  305 |  300 |  Good |   OK |  Meh |  Miss |   Poor |  MapTo |
| ---- | ---- |  ---- | ---- | ---- |  ---- |   ---- |   ---- |
| Cool |      |  Good |      |  Bad |  Miss |        | O2Jam |
|  1/8 | -    |   3/8 | -   | 25/48 | NoHit | -      | BeatLength |
| Kool | Cool |  Good |      |  Miss | Fail |        | Ez2Ac |
| 1/60 | 2/60 |  5/60 | -    |  6/60 | 7/60 | -      | Frame |
| Kool | Cool |  Good |      | Bad       |      Poor | KPoor  |  BMS |
| 16.7 | 33.3 | 116.7 | -    | ±250      | -500/+150 | NoNote | IIDX |
| 15.0 | 30.0 |  60.0 | -    | ±200      |     -1000 | NoNote | LR2 Hard |
| 15.0 | 45.0 | 112.0 | -    | -165/+210 | -500/+150 | NoNote | Raja Normal |
| 20.0 | 60.0 | 150.0 | -    | -220/+280 | -500/+150 | NoNote | Raja Easy |
| Best | Cool |  Good |      |      |  Miss |        |  Malody |
| 20.0 | 60.0 |  94.0 | -    | -    |   150 | -      | E(Hard) |
| 44.0 | 84.0 | 118.0 | -    | -    |   150 | -      | B(Easy+) |");

        public static readonly LocalisableString HEALTH_MODE = new EzLocalizationManager.EzLocalisableString("Mania 血量系统", "Mania Health Mode");

        public static readonly LocalisableString HEALTH_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            @"| 305 | 300 | 200 | 100 | 50 | Miss | Poor | MapTo |
| ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- |
| 0.4% | 0.3% | 0.1% |   -  |  -1% |  -6% |  -0% | Lazer|
| Cool |      | Good |      |  Bad | Miss |      | O2Jam |
| 0.3% | -    | 0.2% |    - |  -1% |  -5% |    - | O2 Easy
| 0.2% | -    | 0.1% |    - |  -7% |  -4% |    - | O2 Normal |
| 0.1% | -    | 0.0% |    - |  -5% |  -3% |    - | O2 Hard |
|  Kool |  Cool |  Good |      |  Bad | Poor | []Poor | BMS |
| 0.16% | 0.16% |     - |   -  |  -5% |  -9% |    -5% | IIDX Hard |
| 0.10% | 0.10% | 0.05% |   -  |  -6% | -10% |    -2% | LR2 Hard |
| 0.15% | 0.12% | 0.03% |   -  |  -5% | -10% |    -5% | Raja Hard |");

        public static readonly LocalisableString POOR_HIT_RESULT = new EzLocalizationManager.EzLocalisableString("增加 Poor 判定类型", "Additional Poor HitResult");

        public static readonly LocalisableString POOR_HIT_RESULT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Pool判定类型只在BMS系血量系统下生效, 用于严格扣血, 不影响Combo、Score\n"
            + "一个note可触发多个Pool判定, 只有早于Miss时才会触发, 不存在晚Pool",
            "The Poor HitResult type only takes effect under the BMS Health Mode, used for strict health deduction, does not affect Combo or Score\n"
            + "One note can trigger multiple Poor hit results, and it will only trigger if it is earlier than Miss, there is no late Poor");

        public static readonly LocalisableString JUDGE_PRECEDENCE = new EzLocalizationManager.EzLocalisableString(
            "判定优先级",
            "Judge Precedence");

        public static readonly LocalisableString JUDGE_PRECEDENCE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "设置优先级算法。当按键点在多个note的判定重叠区时，选择如何计算判定。"
            + "\n Combo优先(LR2风格): 选择对score有利的note进行判定；"
            + "\n 时差优先(raja风格): 根据按键时间点，选择距离最近的note进行判定；"
            + "\n 最早note优先(osu风格): 选择重叠区时间最靠前的note进行判定。(注意可能因连续Late,导致极易出大量poor判而暴毙)",
            "Set the judge precedence algorithm. When the key press point overlaps with multiple notes, how to calculate the judge."
            + "\n Combo Priority (LR2 Style): Select the note that is most advantageous for score; "
            + "\n Duration Priority (Raja Style): Select the note closest to the key press time; "
            + "\n Earliest Note Priority (osu Style): Select the note with the earliest overlap time. (Note: This may result in a large number of poor judgments due to consecutive late notes, leading to a quick loss.)");

        #endregion

        #region 音频设备设置

        public static readonly EzLocalizationManager.EzLocalisableString AUDIO_DEVICE_OUTPUT_HINT = new EzLocalizationManager.EzLocalisableString(
            "ASIO 处于测试阶段！"
            + "\n对于虚拟音频驱动，如VoiceMeeter，可能需要先切换到物理输出设备，激活驱动后，之后再切换回VM。"
            + "\n请不要认为虚拟ASIO比WASAPI更好，如果没有声音请尝试重启。",
            "ASIO is testing! "
            + "\nFor virtual audio drivers like VoiceMeeter, you may need to switch to a physical output device first, activate the driver, and then switch back to VM."
            + "\nPlease do not assume virtual ASIO is better than WASAPI, and try restarting if there is no sound.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_SAMPLE_RATE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 输出格式（内部 PCM）",
            "ASIO Output Format (Internal PCM)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_SAMPLE_RATE_HINT = new EzLocalizationManager.EzLocalisableString(
            "仅在关闭「外部 PCM」时生效。所有音频仍会混音到统一的输出采样率。"
            + "\n推荐 48000 Hz，次选 44100 Hz。",
            "Only applies when External PCM is off. All audio is still mixed to a single output sample rate."
            + "\n48000 Hz is recommended; 44100 Hz is the alternative.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_BUFFER_SIZE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 缓冲区大小（内部 PCM）",
            "ASIO Buffer Size (Internal PCM)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_BUFFER_SIZE_HINT = new EzLocalizationManager.EzLocalisableString(
            "仅在关闭「外部 PCM」时生效。数值越低延迟越低，过低可能爆音或无法启动。",
            "Only applies when External PCM is off. Lower values reduce latency but may crackle or fail to start.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_PASSTHROUGH_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 外部 PCM（推荐）",
            "ASIO External PCM (Recommended)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_PASSTHROUGH_HINT = new EzLocalizationManager.EzLocalisableString(
            "开启：输出采样率、位深与缓冲区由 ASIO 驱动控制面板决定，游戏不覆盖（外部 PCM）。"
            + "\n关闭：使用下方游戏内设置指定输出格式（内部 PCM，适用于无驱动面板的设备）。"
            + "\n无论哪种模式，多路音效都会在混音后以统一格式输出。",
            "On: sample rate, bit depth, and buffer follow the ASIO driver control panel; the game does not override (external PCM)."
            + "\nOff: use the in-game settings below (internal PCM; for devices without a driver panel)."
            + "\nIn both modes, multiple sounds are mixed to one output format.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_RELOAD_DRIVER_LABEL = new EzLocalizationManager.EzLocalisableString(
            "重新加载 ASIO 驱动",
            "Reload ASIO Driver");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_RELOAD_DRIVER_HINT = new EzLocalizationManager.EzLocalisableString(
            "释放本程序占用的音频设备后，按当前输出设备选择重新初始化，并重新读取驱动当前生效的格式与缓冲区。",
            "Releases audio resources held by the game, re-initialises the current output device, and re-reads the driver's active format and buffer settings.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_RELOAD_DRIVER_FAILED_NOTIFICATION = new EzLocalizationManager.EzLocalisableString(
            "ASIO 驱动重新加载失败。请确认驱动控制面板中的设置，或尝试重启游戏。",
            "ASIO driver reload failed. Check the driver control panel settings, or try restarting the game.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_OUTPUT_UNAVAILABLE_NOTIFICATION = new EzLocalizationManager.EzLocalisableString(
            "ASIO 输出未能启动，当前没有声音。请尝试重启游戏；若仍失败，请关闭占用该 ASIO 驱动的其他程序，或切换到其他音频设备。",
            "ASIO output failed to start; there is no audio. Try restarting the game. If it still fails, close other apps using this ASIO driver or switch to another audio device.");

        #endregion

        #region Pixiv 背景

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTH_TOOL_HINT_PREFIX = new EzLocalizationManager.EzLocalisableString(
            "请从 ",
            "Download EzPixivAuth from ");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTH_TOOL_HINT_LINK = new EzLocalizationManager.EzLocalisableString(
            "EzPixivAuth Releases",
            "EzPixivAuth Releases");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTH_TOOL_HINT_SUFFIX = new EzLocalizationManager.EzLocalisableString(
            " 下载 EzPixivAuth，双击运行后会自动写入本机 pixiv_auth.json（与 client.realm 同目录）。",
            " and run it to write pixiv_auth.json next to client.realm.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CUSTOM_TOOL_HINT = new EzLocalizationManager.EzLocalisableString(
            "自定义",
            "Custom");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CUSTOM_TOOL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "展开高级选项：手动 refresh_token、反代地址、过滤、黑白名单。",
            "Show advanced options: manual refresh_token, proxy URL, filters, and artist/tag lists.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_MANUAL_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "仅当你已从其他途径获得 refresh_token 时使用。输入为隐藏显示，勿泄露给他人。",
            "Only if you already have a refresh_token from elsewhere. Input is masked; do not share it.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_REFRESH_TOKEN = new EzLocalizationManager.EzLocalisableString(
            "refresh_token", "refresh_token");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_REFRESH_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "粘贴后点「保存」。仅保存在本机。",
            "Paste and click Save. Stored locally only.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_API_PROXY_BASE_URL = new EzLocalizationManager.EzLocalisableString(
            "反代地址", "Proxy base URL");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_API_PROXY_BASE_URL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "替换 app-api.pixiv.net 与 i.pximg.net 请求（OAuth 登录仍直连）。"
            + "\n留空则全部直连官方。"
            + "\nCloudflare Workers 示例：https://pixiv.yourdomain.com"
            + "\nVercel 示例：https://your-project.vercel.app/api"
            + "\n参考：https://github.com/vmoranv/pixiv-proxy",
            "Rewrites app-api.pixiv.net API calls and i.pximg.net image downloads (OAuth login stays direct)."
            + "\nLeave empty to use official endpoints."
            + "\nCloudflare Workers example: https://pixiv.yourdomain.com"
            + "\nVercel example: https://your-project.vercel.app/api"
            + "\nSee: https://github.com/vmoranv/pixiv-proxy");

        public const string PIXIV_AUTH_TOOL_RELEASES_URL = "https://github.com/SK-la/EzPixivAuth/releases";

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_SAVE_TOKEN = new EzLocalizationManager.EzLocalisableString("保存", "Save");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_SAVE_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "保存到本机。", "Saves locally.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CHECK_LOGIN = new EzLocalizationManager.EzLocalisableString("检查登录", "Check login");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CHECK_LOGIN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "验证凭证是否可用。", "Verifies the saved credential.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_STATUS_NOT_CONFIGURED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv：尚未保存登录凭证。", "Pixiv: no credential saved yet.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_STATUS_LOGGED_IN = new EzLocalizationManager.EzLocalisableString(
            "Pixiv：已登录 @{0}", "Pixiv: logged in as @{0}");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_STATUS_INVALID = new EzLocalizationManager.EzLocalisableString(
            "Pixiv：凭证无效，请重新运行 EzPixivAuth 或手动保存。", "Pixiv: invalid credential; run EzPixivAuth again or save manually.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CLEAR_TOKEN = new EzLocalizationManager.EzLocalisableString("清除", "Clear");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CLEAR_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "删除本机保存的凭证。", "Deletes the locally saved credential.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TOKEN_SAVED = new EzLocalizationManager.EzLocalisableString("已保存。", "Saved.");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TOKEN_CLEARED = new EzLocalizationManager.EzLocalisableString("已清除。", "Cleared.");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TOKEN_EMPTY = new EzLocalizationManager.EzLocalisableString("请先粘贴 refresh_token。", "Paste a refresh_token first.");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_VERIFY_SUCCESS = new EzLocalizationManager.EzLocalisableString("Pixiv 登录成功：@{0}", "Pixiv login OK: @{0}");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_VERIFY_FAILED = new EzLocalizationManager.EzLocalisableString("Pixiv 登录验证失败。", "Pixiv login verification failed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_TOKEN_REFRESH_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 凭证刷新失败。", "Pixiv token refresh failed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_TOKEN_REFRESH_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 凭证刷新未返回 access token。", "Pixiv token refresh returned an empty access token.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_ACCESS_TOKEN_NOT_SET = new EzLocalizationManager.EzLocalisableString(
            "Pixiv access token 未就绪。", "Pixiv access token is not set.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_FOLLOW_FEED_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 关注流加载失败。", "Failed to load Pixiv follow feed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_FOLLOW_FEED_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 关注流过滤后无可用插图。", "Pixiv follow feed returned no illustrations after filtering.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_IMAGE_DOWNLOAD_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 插图下载失败。", "Failed to download Pixiv image.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_IMAGE_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 插图下载结果为空。", "Downloaded Pixiv image was empty.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_REQUEST_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 网络请求失败。", "Pixiv network request failed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LOG_SONG_CHANGE_DOWNLOAD = new EzLocalizationManager.EzLocalisableString(
            "切歌下载", "Song change download");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LOG_AUTO_PREFETCH = new EzLocalizationManager.EzLocalisableString(
            "自动预缓存", "Auto prefetch");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTO_DOWNLOAD_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 自动下载", "Pixiv background auto-download");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTO_DOWNLOAD_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "切歌时优先显示 BG_PIXIV 已有图片（不阻塞界面），并在后台每次追加下载 1 张。"
            + "\n开启后额外持续预缓存；未缓存不足 10 张时自动再拉一页关注流。",
            "Song changes show a random cached BG_PIXIV image immediately without blocking, while one new illustration downloads in the background."
            + "\nWhen enabled, also keeps prefetching and fetches another feed page when fewer than 10 are cached.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ALLOW_R18 = new EzLocalizationManager.EzLocalisableString(
            "允许 R-18 作品", "Allow R-18 works");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ALLOW_R18_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "关闭时过滤 sanity_level ≥ 4 或含 R-18 标签的作品。",
            "When disabled, filters works with sanity_level ≥ 4 or R-18 tags.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LANDSCAPE_ONLY = new EzLocalizationManager.EzLocalisableString(
            "仅横图", "Landscape only");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LANDSCAPE_ONLY_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启时只接受宽大于高的第一页（p0；多页作品不会选用 p1 及之后）。",
            "When enabled, only accepts page 0 (p0) when wider than tall; later pages of multi-page works are never used.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_WHITELIST = new EzLocalizationManager.EzLocalisableString(
            "画师 account 白名单", "Artist account whitelist");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_WHITELIST_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "非空时仅允许列表中的画师 account 句柄；多项用空格分隔（与 osu 标签类似，也可用逗号/分号）。",
            "When non-empty, only listed artist account handles are allowed. Separate with spaces (like osu tags), or commas/semicolons.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_BLACKLIST = new EzLocalizationManager.EzLocalisableString(
            "画师 account 黑名单", "Artist account blacklist");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_BLACKLIST_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "命中则跳过；多项用空格分隔（也可用逗号/分号）。",
            "Matching accounts are skipped. Separate with spaces, or commas/semicolons.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_INCLUDE = new EzLocalizationManager.EzLocalisableString(
            "标签包含（任一）", "Tag include (any)");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_INCLUDE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "非空时作品须至少包含其中一个标签（不区分大小写）；多项用空格分隔。含空格的标签请用逗号括起，如：女の子,AI Generated",
            "When non-empty, works must include at least one listed tag (case-insensitive). Separate with spaces; use commas for tags that contain spaces.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_EXCLUDE = new EzLocalizationManager.EzLocalisableString(
            "标签排除", "Tag exclude");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_EXCLUDE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "命中任一标签则跳过；多项用空格分隔。含空格的标签请用逗号括起。",
            "Works with any listed tag are skipped. Separate with spaces; use commas for tags that contain spaces.");

        #endregion

        #region 实验性功能

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_ACCOUNT = new EzLocalizationManager.EzLocalisableString(
            "本地账户（实验性）", "Local Account (Testing)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_ACCOUNT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "允许无密码登录本地账户。跳过一切成绩上传、网络账户检查。",
            "Allows local account login without password. Skip all score submissions and online account checks.");

        #endregion
    }
}
