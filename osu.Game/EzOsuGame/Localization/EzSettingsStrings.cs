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

        public static readonly EzLocalizationManager.EzLocalisableString EZ_GAME_SECTION_HEADER = new EzLocalizationManager.EzLocalisableString("Ez 游玩", "Ez Gameplay");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_UI_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("Ez UI", "Ez UI");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_GAMEPLAY_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("Ez Gameplay", "Ez Gameplay");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("桌宠", "Desktop pet");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_ENABLED = new EzLocalizationManager.EzLocalisableString("启用桌宠", "Enable desktop pet");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "总开关。打开后还要勾下面的场景；pet.json 里的 hide/show 规则也能临时显隐。"
            + "\n不用在皮肤编辑器或 Ez 布局编辑器里添加。位置用 Left Alt 拖动。",
            "Master toggle. Also enable the scene checkboxes below; pet.json hide/show rules can override visibility."
            + "\nDo not add it in the skin editor or Ez layout editor. Move it with Left Alt + drag.");

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
            "扫描 EzResources/Pets 下带 pet.json 的文件夹。复制整个目录即可换包（PNG 序列帧）。",
            "Scans folders with pet.json under EzResources/Pets. Copy a folder to switch packs (PNG frame clips).");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SCALE = new EzLocalizationManager.EzLocalisableString("桌宠缩放", "Pet scale");

        public static readonly EzLocalizationManager.EzLocalisableString DESKTOP_PET_SCALE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "桌宠立绘缩放。位置请用 Left Alt 拖动，不在此修改。",
            "Scale of the pet sprite. Move it with Left Alt + drag; position is not edited here.");

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

        public static readonly EzLocalizationManager.EzLocalisableString LOGO_CENTRE_TEXT =
            new EzLocalizationManager.EzLocalisableString("Logo 中心文字", "Logo centre text");

        public static readonly EzLocalizationManager.EzLocalisableString LOGO_CENTRE_TEXT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "叠在 logo 正中。格式：文本,字号，例如 123,12 显示「123」、字号 12。支持颜文字；可用中文逗号。省略字号时默认 80。留空则不显示。字号是 logo 贴图坐标系下的像素（中间约 80–160 较合适）。",
            "Drawn in the centre of the logo. Format: text,size — e.g. 123,12 shows \"123\" at size 12. Kaomoji are supported; a Chinese comma also works. Size defaults to 80 if omitted. Leave empty to hide. Size is in logo-texture pixels (about 80–160 looks right in the hole).");

        public static readonly EzLocalizationManager.EzLocalisableString LOGO_CENTRE_TEXT_PLACEHOLDER =
            new EzLocalizationManager.EzLocalisableString("123,12 或 (´・ω・｀),80", "123,12 or (´・ω・｀),80");

        public static readonly EzLocalizationManager.EzLocalisableString LOGO_VISUALISATION =
            new EzLocalizationManager.EzLocalisableString("Logo 可视化", "Logo Visualiser");

        public static readonly EzLocalizationManager.EzLocalisableString LOGO_VISUALISATION_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "主菜单与选歌界面 logo 外圈的音频抖动样式。",
            "Audio visualiser style around the logo on the main menu and song select.");

        public static readonly EzLocalizationManager.EzLocalisableString HIDE_MAIN_MENU_ONLINE_BANNER =
            new EzLocalizationManager.EzLocalisableString("屏蔽主界面底部新闻广告", "Hide main menu bottom news banner");

        public static readonly EzLocalizationManager.EzLocalisableString HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后将隐藏主界面底部的在线新闻/广告轮播图。",
            "When enabled, the online news/advertisement banner at the bottom of the main menu will be hidden.");

        public static readonly EzLocalizationManager.EzLocalisableString TURBO_MODE =
            new EzLocalizationManager.EzLocalisableString("极速模式", "Turbo mode");

        public static readonly EzLocalizationManager.EzLocalisableString TURBO_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "压制皮肤以外的每帧开销以提高帧数：关闭故事板与视频、背景不再绘制、关闭背景与列模糊、"
            + "毛玻璃 UI、打击闪光、星星喷泉、按键显示、游戏内排行榜、季节背景与选歌背景虚化，并让角逐服务空转。"
            + "\n不会降低任何线程频率，也不会锁帧——提高帧数的目的是降低延迟。"
            + "\n开启后全程生效，不随进出游玩切换。被压制的设置项会在设置里变灰，关闭本项后自动还原成你原来的值。"
            + "\n注意：列模糊属于舞台外观，是本模式唯一会改变皮肤观感的一项。",
            "Suppresses per-frame cost outside the skin to raise frame rate: disables storyboard and video, stops drawing the background, "
            + "turns off background and column blur, acrylic UI, hit lighting, star fountains, key overlay, the gameplay leaderboard, "
            + "seasonal backgrounds and song select background blur, and makes the score race service inert."
            + "\nDoes not lower any thread rate and does not cap frames — the point of more frames is lower latency."
            + "\nStays active everywhere rather than toggling as you enter and leave gameplay. Suppressed settings appear greyed out, "
            + "and your original values are restored when this is turned off."
            + "\nNote: column blur is part of the stage look, and is the only item here that changes skin appearance.");

        public static readonly EzLocalizationManager.EzLocalisableString TURBO_MODE_MANAGED_NOTE = new EzLocalizationManager.EzLocalisableString(
            "由极速模式接管。关闭极速模式后会还原成你原来的值。",
            "Managed by turbo mode. Your original value is restored when turbo mode is turned off.");

        public static readonly EzLocalizationManager.EzLocalisableString FLOW_MODE =
            new EzLocalizationManager.EzLocalisableString("心流", "Zen Flow");

        public static readonly EzLocalizationManager.EzLocalisableString FLOW_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "专注过程，淡化成绩。"
            + "\n开启后，游戏结束将直接返回选曲界面，成绩依然保留但不显示结算。选曲界面禁用切换到成绩排名页。",
            "Focus on play, not results."
            + "\nWhen enabled, the game skips the results screen after play. Scores are saved but not shown. Ranking view is disabled in song select.");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_SECTION_HEADER =
            new EzLocalizationManager.EzLocalisableString("快速轮换", "Quick Rotation");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_ENABLED =
            new EzLocalizationManager.EzLocalisableString("快速轮换", "Quick Rotation");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "从选歌进入的单人局结束后，在专用抽卡界面连续游玩相似难度的谱面，全程不回到选歌界面。",
            "After solo play from song select, continue in a dedicated card-pick screen with similar difficulty. Never returns to song select until ended.");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_DIFFICULTY_TOLERANCE =
            new EzLocalizationManager.EzLocalisableString("难度容差", "Difficulty tolerance");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_DIFFICULTY_TOLERANCE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "抽卡后通过 Nice BPM 调速，使有效难度落在首次进入时基准难度的 ± 此值范围内。",
            "After picking a card, Nice BPM adjusts speed so effective difficulty stays within ± this value of the baseline from your first chart.");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_CROSS_KEY_MODE =
            new EzLocalizationManager.EzLocalisableString("跨键数轮换", "Cross key-count rotation");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_CROSS_KEY_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "仅 Mania。开启后候选池可含 4–10K；关闭则整段会话锁定首轮谱面的键数。",
            "Mania only. When enabled, the pool may include 4–10K charts. When disabled, key count is locked to the first chart.");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_CANDIDATE_COUNT =
            new EzLocalizationManager.EzLocalisableString("候选卡片数", "Candidate card count");

        public static readonly EzLocalizationManager.EzLocalisableString QUICK_ROTATION_CANDIDATE_COUNT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "每轮抽卡界面展示的谱面数量（3–6）。",
            "Number of beatmap cards shown each pick round (3–6).");

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

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_MODIFY =
            new EzLocalizationManager.EzLocalisableString("修改字体", "Modify fonts");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_MODIFY_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "选择系统字体：英文主字体 + 本地化缺字回退 + 系统 emoji（空=平台自动）。英语界面时英/本地化合一。更改下次启动生效。",
            "Pick system fonts: English primary + localized fallback + system emoji (empty = platform auto). English UI merges EN/localized. Applies on next launch.");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_DIALOG_HEADER =
            new EzLocalizationManager.EzLocalisableString("字体设置", "Font settings");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_NEXT_LAUNCH =
            new EzLocalizationManager.EzLocalisableString("下次启动时生效", "Takes effect on next launch");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_DEFAULT =
            new EzLocalizationManager.EzLocalisableString("UI 默认", "UI default");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_DEFAULT_EN =
            new EzLocalizationManager.EzLocalisableString("UI 英文", "UI English");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_DEFAULT_LOC =
            new EzLocalizationManager.EzLocalisableString("UI 本地化", "UI localized");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_TITLE =
            new EzLocalizationManager.EzLocalisableString("标题花体", "Title / alternate");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_TITLE_EN =
            new EzLocalizationManager.EzLocalisableString("标题英文", "Title English");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_TITLE_LOC =
            new EzLocalizationManager.EzLocalisableString("标题本地化", "Title localized");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_NUMERIC =
            new EzLocalizationManager.EzLocalisableString("数字", "Numeric");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SLOT_EMOJI =
            new EzLocalizationManager.EzLocalisableString("系统 Emoji", "System emoji");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_NONE =
            new EzLocalizationManager.EzLocalisableString("（未覆盖，使用内置）", "(No override — use built-in)");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_EMOJI_AUTO =
            new EzLocalizationManager.EzLocalisableString("（自动：平台默认彩色 emoji）", "(Auto — platform colour emoji)");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_SEARCH =
            new EzLocalizationManager.EzLocalisableString("搜索字体…", "Search fonts…");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_PREVIEW_DEFAULT =
            new EzLocalizationManager.EzLocalisableString("选歌与设置 Aa 测试", "Song select & settings Aa");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_PREVIEW_TITLE =
            new EzLocalizationManager.EzLocalisableString("Heading 标题", "Heading Title");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_PREVIEW_NUMERIC =
            new EzLocalizationManager.EzLocalisableString("0123456789", "0123456789");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_PREVIEW_EMOJI =
            new EzLocalizationManager.EzLocalisableString("😀🎉✨👍🔥", "😀🎉✨👍🔥");

        public static readonly EzLocalizationManager.EzLocalisableString UI_FONT_CLOSE =
            new EzLocalizationManager.EzLocalisableString("关闭", "Close");

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
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析即时计算", "Enable Ez analysis on-demand computation");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "控制 Ez SQLite 体系内指标在选歌时是否按需即时计算："
            + "\n· 主库：NoMod 的 kps/KPC（及 mania 列统计）缺失时 debounce 后补算；"
            + "\n· 有 Mod：按当前 Mod 即时计算 kps/xxy 等（无匹配分支快照时）；"
            + "\n· 分支库：仍读取已激活分支的预生成快照，本开关不替代分支构建。"
            + "\n关闭时仅读已落盘 SQLite 数据，不触发选歌实时计算。"
            + "\n不影响 Realm 元数据回填；Panel PP 随官方星级缓存（BeatmapDifficultyCache），与开关无关。",
            "Controls whether Ez SQLite metrics are computed on demand during song select:"
            + "\n· Main DB: backfill missing NoMod kps/KPC (and mania column stats) after debounce;"
            + "\n· With mods: compute kps/xxy live for the current mod set when no matching branch snapshot exists;"
            + "\n· Branch DB: still reads precomputed snapshots from the active branch; this switch does not replace branch builds."
            + "\nWhen disabled, only stored SQLite values are read; no live song-select computation."
            + "\nDoes not affect Realm metadata backfill; panel PP follows the official star cache (BeatmapDifficultyCache) and is not gated by this switch.");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析 SQLite", "Enable Ez analysis SQLite");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 分析 SQLite 主库与分支曲库的读写（kps/KPC、分支 xxy/PP 快照等）。"
            + "\n关闭后不加载、不写入上述本地缓存。"
            + "\n自动预热仅在缺少当前版本库或需 schema 升级时触发；已有匹配库时请用下方维护控件手动补算。"
            + "\n即时计算行为由「Ez 分析即时计算」开关单独控制。",
            "Enables read/write for the Ez analysis main SQLite database and songs branch libraries (kps/KPC, branch xxy/PP snapshots, etc.)."
            + "\nWhen disabled, stored local caches are neither loaded nor written."
            + "\nAuto-warmup runs only when the current database is missing or needs a schema upgrade; use maintenance controls below when a matching file already exists."
            + "\nOn-demand computation is controlled separately by \"Enable Ez analysis on-demand computation\".");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_TARGET =
            new EzLocalizationManager.EzLocalisableString("数据维护目标", "Data maintenance target");

        public static readonly EzLocalizationManager.EzLocalisableString DATA_REBUILD_TARGET_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "选择要维护的数据范围。SQLite 在已有匹配的最新版文件时不会自动预热；Realm 缺失项仍会在启动时自动补算。"
            + "\n成绩全量重算：有 replay 的 mania 成绩走 Session 原始环境重算（与选歌右键一致）；stable 旧成绩无 replay 时重走官方转换。"
            + "\n「尝试补算」仅处理 mania 成绩；「完全重算」处理全部游戏模式。",
            "Choose which data to maintain. SQLite is not auto-warmed when a matching current database exists; Realm missing values are still filled at startup."
            + "\nFull score recalculation: mania scores with replays are recalculated via Session in their stored environment (same as the song select context menu); legacy scores without replays are re-converted officially."
            + "\n\"Backfill\" processes mania scores only; \"Force rebuild\" processes all rulesets.");

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

        public static readonly LocalisableString SCRATCH_AXIS_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用转盘轴（L/R Scratch）",
            "Enable turntable axis (L/R Scratch)");

        public static readonly LocalisableString SCRATCH_AXIS_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后 L/R 转盘轴可用于 Mania 与 Catch（共用下方绑定与死区/停转设置）："
            + "\n• Mania：转动 L/R 转盘 → 对应首尾列按下；停止 = 松开。"
            + "\n  12K：列 0/11；16K：列 0/15；14K：列 0/13（10k2s1p 为 0/12）。"
            + "\n• Catch：逆时针 → 左移，顺时针 → 右移；转动期间判定放宽（早 10ms / 晚 20ms）；Dash 键照常。"
            + "\n• 单盘：只绑定 L 或 R 其中一个即可，顺/逆控制两个方向。"
            + "\n• 双盘：L/R 可各自顺逆；同时转动时以最近转动为准。"
            + "\n请勿把同一轴再绑到其它键位。",
            "When enabled, L/R turntable axes work in Mania and Catch (shared bindings and deadzone/stop settings below):"
            + "\n• Mania: spin L/R → press edge columns; stop = release."
            + "\n  12K: cols 0/11; 16K: cols 0/15; 14K: cols 0/13 (10k2s1p: 0/12)."
            + "\n• Catch: CCW → move left, CW → move right; judgment assist while spinning (10ms early / 20ms late); Dash keys unchanged."
            + "\n• Single deck: bind only L or R; both directions via spin direction."
            + "\n• Dual deck: each spins independently; if both active, the most recent spin wins."
            + "\nDo not bind the same axis to other keys.");

        public static readonly LocalisableString CATCH_SCRATCH_EZ2_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez2Catch 转盘增强",
            "Enable Ez2Catch turntable enhancement");

        public static readonly LocalisableString CATCH_SCRATCH_EZ2_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Catch 专用增强（需先开启上方转盘轴）："
            + "\n• 死区减半、1 帧即开始移动（默认需连续 2 帧同向超过死区才动，故小转一圈才有反应）。"
            + "\n• 转速越快移动越快，最高约 1.5× Dash 速度；Dash 键照常。"
            + "\n不影响 Mania scratch。",
            "Catch-only enhancement (requires turntable axis above):"
            + "\n• Half deadzone, 1-frame activation (default needs 2 consecutive frames above deadzone — why small spins feel dead)."
            + "\n• Faster spin = faster movement, up to ~1.5× Dash; Dash keys unchanged."
            + "\nDoes not affect Mania scratch.");

        public static readonly LocalisableString CATCH_SCRATCH_DASH_ENTER_ACCELERATION = new EzLocalizationManager.EzLocalisableString(
            "进入加速阈值",
            "Enter dash acceleration threshold");

        public static readonly LocalisableString CATCH_SCRATCH_DASH_ENTER_ACCELERATION_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "角加速度达到此值时进入 Dash（模拟按住加速键）。滑条显示值为实际阈值×1000。",
            "Enter Dash when angular acceleration reaches this value (simulates holding Dash). Slider shows actual threshold ×1000.");

        public static readonly LocalisableString CATCH_SCRATCH_DASH_EXIT_VELOCITY = new EzLocalizationManager.EzLocalisableString(
            "重置速度阈值",
            "Reset dash velocity threshold");

        public static readonly LocalisableString CATCH_SCRATCH_DASH_EXIT_VELOCITY_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "平滑角速度低于此值时退出 Dash（模拟松开加速键）。滑条显示值为实际阈值×1000。",
            "Exit Dash when smoothed angular velocity drops below this value (simulates releasing Dash). Slider shows actual threshold ×1000.");

        public static readonly LocalisableString SCRATCH_AXIS_L = new EzLocalizationManager.EzLocalisableString("L-Scratch 轴", "L-Scratch Axis");

        public static readonly LocalisableString SCRATCH_AXIS_R = new EzLocalizationManager.EzLocalisableString("R-Scratch 轴", "R-Scratch Axis");

        public static readonly LocalisableString SCRATCH_AXIS_BIND_HINT = new EzLocalizationManager.EzLocalisableString(
            "转动转盘以绑定…（再点取消 / Esc）",
            "Spin turntable to bind… (click again / Esc to cancel)");

        public static readonly LocalisableString SCRATCH_AXIS_BIND_IDLE_HINT = new EzLocalizationManager.EzLocalisableString(
            "点击 L/R 后转动对应转盘；仅累计位移，停靠位置不会触发绑定。Catch 单盘只需绑一侧。",
            "Click L/R then spin the turntable; only movement is counted, resting position does not bind. For Catch, binding one side is enough.");

        public static readonly LocalisableString SCRATCH_AXIS_BIND_LISTENING_HINT = new EzLocalizationManager.EzLocalisableString(
            "正在听轴… 累计位移 {0} / {1}",
            "Listening… travel {0} / {1}");

        public static readonly LocalisableString SCRATCH_AXIS_DEADZONE = new EzLocalizationManager.EzLocalisableString(
            "最小位移阈值",
            "Min movement delta");

        public static readonly LocalisableString SCRATCH_AXIS_STOP_THRESHOLD = new EzLocalizationManager.EzLocalisableString(
            "停转判定（毫秒）",
            "Stop threshold (ms)");

        public static readonly LocalisableString SCRATCH_AXIS_STOP_THRESHOLD_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "距上次有效转动超过此时长后视为松开。停靠在任意轴位置都不是按下，只有转动位移才算。",
            "Release after this many milliseconds without significant spin. Resting at any axis position is idle; only movement counts.");

        public static readonly LocalisableString SCRATCH_AXIS_DEADZONE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "相邻两次采样之间的最小 |Δ|（轴约 [-1,1]，对应 0–100 归一化后）。"
            + "\n不是摇杆「离中心」死区。beatoraja tick≈0.009；推荐 0.001–0.01。"
            + "\n过大（如 0.02+）会导致转盘无响应；过小（0）易被噪声连打。",
            "Minimum |Δ| between consecutive samples (axis ≈ [-1,1], i.e. normalised 0–100)."
            + "\nNot stick-from-centre deadzone. beatoraja tick ≈ 0.009; try 0.001–0.01."
            + "\nToo large (e.g. 0.02+) ignores real spins; 0 lets noise spam presses.");

        public static readonly LocalisableString SCRATCH_AXIS_STATUS_IDLE = new EzLocalizationManager.EzLocalisableString("状态：空闲", "Status: Idle");

        public static readonly LocalisableString SCRATCH_AXIS_STATUS_CW = new EzLocalizationManager.EzLocalisableString("状态：顺时针（按下）", "Status: Clockwise (pressed)");

        public static readonly LocalisableString SCRATCH_AXIS_STATUS_CCW = new EzLocalizationManager.EzLocalisableString("状态：逆时针（按下）", "Status: Counter-clockwise (pressed)");

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

        #region 实验性功能

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_ACCOUNT = new EzLocalizationManager.EzLocalisableString(
            "本地账户（实验性）", "Local Account (Testing)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_ACCOUNT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "允许无密码登录本地账户。跳过一切成绩上传、网络账户检查。",
            "Allows local account login without password. Skip all score submissions and online account checks.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE =
            new EzLocalizationManager.EzLocalisableString("计算本地个人成绩", "Compute Local Profile Stats");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "扫描本地成绩，按玩家名勾选导入后写入共享个人统计存档（与当前登录名无关）。",
            "Scan local scores, pick player names to include, then write into the shared local profile archive (independent of the logged-in name).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_HEADER =
            new EzLocalizationManager.EzLocalisableString("选择要导入的玩家名", "Select player names to import");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_BODY = new EzLocalizationManager.EzLocalisableString(
            "勾选要【重新计算】的玩家名：只覆盖这些名称对应的统计切片，其它已计算名称不受影响。打开「替换模式」会删除未勾选名称的旧切片。",
            "Check names to recompute: only those players’ stat slices are overwritten; other computed names stay. Enable replace mode to drop unchecked names’ old slices.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_REPLACE =
            new EzLocalizationManager.EzLocalisableString(
                "替换模式：删除未勾选名称的旧统计（不保留）",
                "Replace mode: delete old stats for unchecked names");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_CONFIRM =
            new EzLocalizationManager.EzLocalisableString("开始计算", "Compute");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_CANCEL =
            new EzLocalizationManager.EzLocalisableString("取消", "Cancel");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_STARTED =
            new EzLocalizationManager.EzLocalisableString("正在计算本地个人成绩…", "Computing local profile stats…");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_PROGRESS =
            new EzLocalizationManager.EzLocalisableString("正在计算个人成绩 PP… {0}/{1}", "Computing profile PP… {0}/{1}");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_SAVING =
            new EzLocalizationManager.EzLocalisableString("正在写入本地个人成绩…", "Saving local profile stats…");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_DONE =
            new EzLocalizationManager.EzLocalisableString("本地个人成绩已更新。", "Local profile stats updated.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_FAILED =
            new EzLocalizationManager.EzLocalisableString("本地个人成绩计算失败。", "Failed to compute local profile stats.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NO_SCORES =
            new EzLocalizationManager.EzLocalisableString("未找到可导入的本地成绩。", "No local scores found to import.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NONE_SELECTED =
            new EzLocalizationManager.EzLocalisableString("请至少勾选一个玩家名。", "Select at least one player name.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SELECT_SCORE =
            new EzLocalizationManager.EzLocalisableString("在左侧选择一条成绩", "Select a score on the left");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TITLE =
            new EzLocalizationManager.EzLocalisableString("本地个人主页", "Local Profile");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString("共享本地游玩统计", "Shared local play statistics");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SHARED_HINT =
            new EzLocalizationManager.EzLocalisableString("本地统计（共享存档）", "Local stats (shared archive)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BADGE =
            new EzLocalizationManager.EzLocalisableString("本地", "Local");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ACCOUNT_BUTTON =
            new EzLocalizationManager.EzLocalisableString("账号", "Account");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_PANEL_BUTTON =
            new EzLocalizationManager.EzLocalisableString("本地档案", "Local Profile");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_EMPTY_HINT =
            new EzLocalizationManager.EzLocalisableString("暂无统计。请到 设置 → Ez → 实验性功能 中点击「计算本地个人成绩」。", "No stats yet. Open Settings → Ez → Experimental and run “Compute Local Profile Stats”.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_KEYS =
            new EzLocalizationManager.EzLocalisableString("按键与 KPS", "Keys & KPS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_PERFORMANCE =
            new EzLocalizationManager.EzLocalisableString("成绩表现", "Performance");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_MANIA =
            new EzLocalizationManager.EzLocalisableString("Mania 键数 / 列", "Mania Keys / Columns");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_STD =
            new EzLocalizationManager.EzLocalisableString("最擅长 AR / CS", "Best AR / CS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_GRADES =
            new EzLocalizationManager.EzLocalisableString("成绩评级", "Grades");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_STARS =
            new EzLocalizationManager.EzLocalisableString("星级分布", "Star rating");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_CAREER =
            new EzLocalizationManager.EzLocalisableString("生涯总览", "Career overview");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_MODE_DATA =
            new EzLocalizationManager.EzLocalisableString("各模式统计", "Stats by mode");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_XXY_PLAY_DISTRIBUTION =
            new EzLocalizationManager.EzLocalisableString("xxy 星级分布", "Plays by xxy SR");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_STAR_PLAY_LINE =
            new EzLocalizationManager.EzLocalisableString("星级游玩分布", "Plays by star rating");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_XXY_PLAY_LINE =
            new EzLocalizationManager.EzLocalisableString("xxy 游玩分布", "xxy SR distribution");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_SCORE_DRILL =
            new EzLocalizationManager.EzLocalisableString("成绩记录", "Score history");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_BEATMAP_PERF =
            new EzLocalizationManager.EzLocalisableString("同图对比", "On this beatmap");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_SCORE_KEYS =
            new EzLocalizationManager.EzLocalisableString("本局数据", "This score");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_TRENDS =
            new EzLocalizationManager.EzLocalisableString("命中趋势", "Hit trends");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DRILL_SEARCH_PLACEHOLDER =
            new EzLocalizationManager.EzLocalisableString("按 PP、曲名或艺术家搜索", "Search by PP, title, or artist");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DRILL_NO_MATCHES =
            new EzLocalizationManager.EzLocalisableString("没有符合条件的成绩", "No scores match your search");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEATMAP_PERF_PP =
            new EzLocalizationManager.EzLocalisableString("PP", "PP");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEATMAP_PERF_ACC =
            new EzLocalizationManager.EzLocalisableString("Acc", "Acc");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEATMAP_PERF_OFFSET =
            new EzLocalizationManager.EzLocalisableString("Offset", "Offset");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TREND_OFFSET =
            new EzLocalizationManager.EzLocalisableString("击打偏移", "Hit offset");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TREND_ACCURACY =
            new EzLocalizationManager.EzLocalisableString("准确率", "Accuracy");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TREND_EMPTY =
            new EzLocalizationManager.EzLocalisableString("暂无趋势数据（需保留本地回放）", "No trend data yet (local replay required).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TOTAL_KEYS =
            new EzLocalizationManager.EzLocalisableString("按键总数", "Total keys");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_AVG_KPS =
            new EzLocalizationManager.EzLocalisableString("平均 KPS", "Avg KPS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_MAX_KPS =
            new EzLocalizationManager.EzLocalisableString("最大 KPS", "Max KPS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SCORE_COUNT =
            new EzLocalizationManager.EzLocalisableString("成绩数", "Scores");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TOTAL_PP =
            new EzLocalizationManager.EzLocalisableString("PP 合计", "Total PP");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TOTAL_DURATION =
            new EzLocalizationManager.EzLocalisableString("游戏时长", "Play Time");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEST_AR =
            new EzLocalizationManager.EzLocalisableString("最擅长 AR", "Best AR");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEST_CS =
            new EzLocalizationManager.EzLocalisableString("最擅长 CS", "Best CS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NO_RULESET_DATA =
            new EzLocalizationManager.EzLocalisableString("此模式暂无数据。", "No data for this ruleset.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COL_HEADER =
            new EzLocalizationManager.EzLocalisableString("列", "Col");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_MANIA_PLAYS_BY_KEY =
            new EzLocalizationManager.EzLocalisableString("各键数游玩次数", "Plays by key count");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_MANIA_AVG_KPS_LINE =
            new EzLocalizationManager.EzLocalisableString("各键数平均 KPS", "Avg KPS by key count");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL =
            new EzLocalizationManager.EzLocalisableString("从线上下载成绩或谱面（实验）", "Download Online Scores or Beatmaps (Experimental)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "需 Online 登录。下拉模式过滤 BP / 玩过的图；每批 50（BP100=两次）。可选下载缺图并加入「BP」或「玩过的图」收藏夹。",
            "Requires Online login. Ruleset filters BP / most-played; batch size 50 (BP100 = two runs). Optionally download missing maps into the BP or most-played collection.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_HEADER =
            new EzLocalizationManager.EzLocalisableString("从线上下载成绩或谱面", "Download Online Scores or Beatmaps");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_BODY = new EzLocalizationManager.EzLocalisableString(
            "模式对两种来源都生效。每批 50：BP 用 offset 0 与 50 各下载一次即可凑满约 100。玩过的图同样按起始 offset 推进。勾选下载缺图时，边下边加入对应收藏夹（仅「BP」「玩过的图」两个，不重复创建）。",
            "Ruleset applies to both sources. Batch size 50: run BP at offset 0 then 50 for ~100. Most-played advances the same way. With download enabled, maps are added to the matching collection as they finish (only “BP” / “玩过的图”, no duplicates).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_RULESET =
            new EzLocalizationManager.EzLocalisableString("模式（过滤）", "Ruleset (filter)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_KIND =
            new EzLocalizationManager.EzLocalisableString("来源", "Source");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_OFFSET_HINT =
            new EzLocalizationManager.EzLocalisableString(
                "当前来源+模式已存进度 offset = {0}（每批 {1}）。下方可改本次起始值。",
                "Stored offset for current source+ruleset = {0} (batch {1}). Edit the start value below.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_OFFSET_INPUT =
            new EzLocalizationManager.EzLocalisableString("起始 offset（填 0 重置）", "Start offset (0 = reset)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_INCLUDE_STATS =
            new EzLocalizationManager.EzLocalisableString(
                "缺图也写入个人统计（用 API 元数据，无 KPS；默认开）",
                "Also write profile stats without local map (API metadata, no KPS; on by default)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_DOWNLOAD_MAPS =
            new EzLocalizationManager.EzLocalisableString(
                "下载缺失谱面并加入收藏夹（BP→「BP」/ 玩过的图→「玩过的图」，节流）",
                "Download missing maps into collection (BP→“BP” / most-played→“玩过的图”, throttled)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_CONFIRM =
            new EzLocalizationManager.EzLocalisableString("开始下载", "Start Download");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_NEED_ONLINE =
            new EzLocalizationManager.EzLocalisableString("请先 Online 登录自己的账号（不能用本地账号模式）。", "Online login as yourself is required (not local-only mode).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_BUSY =
            new EzLocalizationManager.EzLocalisableString("正在下载线上成绩…", "Downloading online scores…");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_DONE =
            new EzLocalizationManager.EzLocalisableString(
                "线上下载完成：候选 {0}，成绩导入 {1}，已有成绩 {2}，无回放 {3}，缺图成绩 {4}，失败 {5}，统计写入 {6}，下图 {7}，本地已有图 {8}，收藏夹新增 {9}。",
                "Online download done: candidates {0}, scores imported {1}, owned {2}, no replay {3}, missing map scores {4}, failed {5}, stats {6}, maps downloaded {7}, maps already local {8}, collection adds {9}.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_FAILED =
            new EzLocalizationManager.EzLocalisableString("线上成绩下载失败。", "Failed to download online scores.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER =
            new EzLocalizationManager.EzLocalisableString("外部规则集管理器", "External Ruleset Manager");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "管理 rulesets/ 目录下的第三方规则集：启用/禁用加载，以及显式 OnlineID（≥4）的排序与映射。修改后需重启游戏。",
            "Manage third-party rulesets in rulesets/: enable/disable loading and order explicit OnlineIDs (>=4). Restart required after saving.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_HEADER =
            new EzLocalizationManager.EzLocalisableString("外部规则集管理", "External Ruleset Management");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_BODY = new EzLocalizationManager.EzLocalisableString(
            "仅影响 rulesets/ 目录中的用户 DLL。未定义 OnlineID（-1）的规则集不会被分配新 ID。",
            "Only affects user DLLs in rulesets/. Rulesets without an explicit OnlineID (-1) are not assigned new IDs.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_SAVE =
            new EzLocalizationManager.EzLocalisableString("保存", "Save");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_SAVED =
            new EzLocalizationManager.EzLocalisableString("外部规则集配置已保存，请重启游戏后生效。", "External ruleset config saved. Restart the game to apply.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_RESTART_HINT = new EzLocalizationManager.EzLocalisableString(
            "保存后请重启游戏使配置生效。",
            "Restart the game after saving to apply changes.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "rulesets/ 目录下暂无外部规则集 DLL。\n请将 osu.Game.Rulesets.*.dll 放入该目录后重新打开本窗口。",
            "No external ruleset DLLs in rulesets/.\nPlace osu.Game.Rulesets.*.dll there, then reopen this window.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_OPEN_FOLDER =
            new EzLocalizationManager.EzLocalisableString("打开 rulesets 文件夹", "Open rulesets folder");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_MANAGER_OPEN_FOLDER_FAILED =
            new EzLocalizationManager.EzLocalisableString("无法打开 rulesets 文件夹。", "Could not open the rulesets folder.");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_NO_DEFINED_ID =
            new EzLocalizationManager.EzLocalisableString("未定义 ID", "No ID defined");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用", "Enabled");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_ID_LABEL =
            new EzLocalizationManager.EzLocalisableString("ID", "ID");

        public static readonly EzLocalizationManager.EzLocalisableString EXTERNAL_RULESET_INVALID_ONLINE_ID = new EzLocalizationManager.EzLocalisableString(
            "OnlineID 必须为 -1 或 ≥4（不能使用 0~3）。",
            "OnlineID must be -1 or ≥4 (0~3 are reserved).");

        #endregion
    }
}
