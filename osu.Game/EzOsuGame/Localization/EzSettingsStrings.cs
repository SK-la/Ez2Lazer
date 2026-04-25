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

        public static readonly EzLocalizationManager.EzLocalisableString EZ_GAME_SECTION_HEADER = new EzLocalizationManager.EzLocalisableString("Ez游玩设置", "Ez Gameplay Settings");
        public static readonly EzLocalizationManager.EzLocalisableString EZ_UI_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("Ez 界面设置", "Ez UI Settings");

        public static readonly EzLocalizationManager.EzLocalisableString HIDE_MAIN_MENU_ONLINE_BANNER =
            new EzLocalizationManager.EzLocalisableString("屏蔽主界面底部新闻广告", "Hide main menu bottom news banner");

        public static readonly EzLocalizationManager.EzLocalisableString HIDE_MAIN_MENU_ONLINE_BANNER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后将隐藏主界面底部的在线新闻/广告轮播图。",
            "When enabled, the online news/advertisement banner at the bottom of the main menu will be hidden.");

        public static readonly EzLocalizationManager.EzLocalisableString STORYBOARD_VIDEO_AUTO_SIZE =
            new EzLocalizationManager.EzLocalisableString("故事板视频自适应填满", "Storyboard video auto-size to fill");

        public static readonly EzLocalizationManager.EzLocalisableString STORYBOARD_VIDEO_AUTO_SIZE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后故事板视频将自动调整大小以填满整个故事板区域，可能会裁剪部分画面但能更好地适应不同分辨率和屏幕比例。",
            "When enabled, storyboard videos will automatically adjust their size to fill the entire storyboard area, "
            + "which may crop some of the video but will better adapt to different resolutions and screen ratios.");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析重算", "Enable Ez analysis recomputation");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启后允许选歌界面进行实时分析并刷新显示，纳入Mod转换结果的分析。"
            + "\n注意：该开关不控制 SQLite 本地读取与预热（由下方 SQLite 开关单独控制）。",
            "When enabled, allows real-time analysis and display refresh in the song selection screen, including analysis of Mod conversion results."
            + "\nNote: This switch does not control SQLite local reading and warmup (which is controlled separately by the SQLite switch below).");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析本地数据（SQLite）", "Enable Ez analysis local data (SQLite)");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "启用独立控制的 SQLite 本地储存，启动时预热器。（No-Mod）"
            + "\n开启：面板直读本地数据，在exe启动时执行预热列队分析加载（进入游戏中会暂停进度），但不会考虑Mod转换结果。"
            + "\n关闭：不读取 SQLite 本地数据，也不执行预热。",
            "Enable independently controlled SQLite local storage and warmup on startup (No-Mod)"
            + "\nWhen enabled: Panels read local data directly, and perform warmup queue analysis loading on exe startup (progress will pause when entering gameplay), but Mod conversion results will not be considered."
            + "\nWhen disabled: Do not read SQLite local data, and do not perform warmup.");

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

        public static readonly LocalisableString HIT_MODE = new EzLocalizationManager.EzLocalisableString("Mania 判定系统", "Mania Hit Mode");

        public static readonly LocalisableString HIT_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "最大限度的获得不同音游的打击体验, 如 O2,BMS系 具备对应的特殊机制。\n"
            + "305  | 300  | 200  | 100 | 50  | Miss | Poor   | (UI)\n"
            + "———  ———  ———  ———  ———  ———  ———  ———\n"
            + "Cool | ---- | Good  | --- | Bad | Miss | ----   | (映射)\n"
            + "0.3% | ---- | 0.2%  | --- | 1%  | 5%   | ----   | O2 Easy\n"
            + "0.2% | ---- | 0.1%  | --- | 7%  | 4%   | ----   | O2 Normal\n"
            + "0.1% | ---- | 0.0%  | --- | 5%  | 3%   | ----   | O2 Hard\n"
            + "———  ———  ———  ———  ———  ———  ———  ———\n"
            + "Kool | Cool | Good  | --- | Bad | Poor | []Poor | (映射)\n"
            + "16.7 | 33.3 | 116.7 | --- | 250 | 250  | 500    | IIDX\n"
            + "15.0 | 30.0 | 60.0   | --- | 200 | 1000 | 1000   | LR2 Hard\n"
            + "15.0 | 45.0 | 112.0 | --- | 165 | 500  | 500    | Raja Normal\n"
            + "20.0 | 60.0 | 150.0 | --- | 500 | 500  | 500    | Raja Easy",
            "Get different rhythm game hit experiences to the maximum extent, such as O2 and BMS systems with corresponding special mechanisms.\n"
            + "305  | 300  | 200   | 100 | 50  | Miss | Poor   | (UI)\n"
            + "———  | ———  | ———   | ——— | ——— | ———  | ———    —\n"
            + "Cool | ---- | Good  | --- | Bad | Miss | ----   | (映射)\n"
            + "0.3% | ---- | 0.2%  | --- | 1%  | 5%   | ----   | O2 Easy\n"
            + "0.2% | ---- | 0.1%  | --- | 7%  | 4%   | ----   | O2 Normal\n"
            + "0.1% | ---- | 0.0%  | --- | 5%  | 3%   | ----   | O2 Hard\n"
            + "———  | ———  | ———   | ——— | ——— | ———  | ———    —\n"
            + "Kool | Cool | Good  | --- | Bad | Poor | []Poor | (映射)\n"
            + "16.7 | 33.3 | 116.7 | --- | 250 | 250  | 500    | IIDX\n"
            + "15.0 | 30.0 | 60.0  | --- | 200 | 1000 | 1000   | LR2 Hard\n"
            + "15.0 | 45.0 | 112.0 | --- | 165 | 500  | 500    | Raja Normal\n"
            + "20.0 | 60.0 | 150.0 | --- | 500 | 500  | 500    | Raja Easy");

        public static readonly LocalisableString HEALTH_MODE = new EzLocalizationManager.EzLocalisableString("Mania 血量系统", "Mania Health Mode");

        public static readonly LocalisableString HEALTH_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            ""
            + "| 305  | 300  | 200  | 100 |   50  | Miss | Poor  | (UI)|\n"
            + "| ——— | ——— | ——— | ——— | —— | ——— | ——— | ———|\n"
            + "| 0.4% | 0.3% | 0.1% | ---  | -1% | -6%  | -0%    | Lazer|\n"
            + "| ——— | ——— | ——— | ——— | —— | ——— | ——— | ———|\n"
            + "| Cool | ---- |  Good | ---  | Bad | Miss |  ----   |\n"
            + "| 0.3% | 0.0% | 0.2% | ---  | -1% | -5%  | -0%    | O2 Easy|\n"
            + "| 0.2% | 0.0% | 0.1% | ---  | -7% | -4%  | -0%    | O2 Normal|\n"
            + "| 0.1% | 0.0% | 0.0% | ---  | -5% | -3%  | -0%    | O2 Hard|\n"
            + "| ——— | ——— | ——— | ——— | —— | ——— | ——— | ———|\n"
            + "| Kool | Cool |  Good | ---  | Bad | Poor | []Poor |\n"
            + "| 0.4% | 0.3% | 0.1% | ---  | -1% | -5%   | -5%    | Ez2Ac|\n"
            + "| 1.6% | 1.6% | ----  | ---  | -5% | -9%   | -5%    | IIDX Hard|\n"
            + "| 1.0% | 1.0% | 0.5% | ---  | -6% | -10% | -2%    | LR2 Hard|\n"
            + "| 1.2% | 1.2% | 0.6% | ---  | -3% | -6%   | -2%    | Raja Normal|");

        public static readonly LocalisableString POOR_HIT_RESULT = new EzLocalizationManager.EzLocalisableString("增加 Poor 判定类型", "Additional Poor HitResult");

        public static readonly LocalisableString POOR_HIT_RESULT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Pool判定类型只在BMS系血量系统下生效, 用于严格扣血, 不影响Combo、Score\n"
            + "一个note可触发多个Pool判定, 只有早于Miss时才会触发, 不存在晚Pool",
            "The Poor HitResult type only takes effect under the BMS Health Mode, used for strict health deduction, does not affect Combo or Score\n"
            + "One note can trigger multiple Poor hit results, and it will only trigger if it is earlier than Miss, there is no late Poor");

#endregion

#region 音频设备设置

        public static readonly EzLocalizationManager.EzLocalisableString AUDIO_DEVICE_OUTPUT_HINT = new EzLocalizationManager.EzLocalisableString(
            "ASIO 处于测试阶段！"
            + "\n对于虚拟声卡，如VoiceMeeter，可能需要先切换到物理输出设备，之后再切换回VM。"
            + "\n请不要以为ASIO4All这类虚拟ASIO比WASAPI更好，软件桥接并不提供真正的ASIO低延迟。"
            + "\n硬件ASIO在 wasapi/VM 下，低延迟也依然是更好的。",
            "ASIO is testing! "
            + "\nFor virtual sound cards like VoiceMeeter, you may need to switch to a physical output device first, and then switch back to VM."
            + "\nPlease don't assume that virtual ASIO like ASIO4All is better than WASAPI, software bridging does not provide true ASIO low latency."
            + "\nHardware ASIO under wasapi/VM, low latency is still better.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_SAMPLE_RATE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 采样率（测试）",
            "ASIO Sample Rate (Testing)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_SAMPLE_RATE_HINT = new EzLocalizationManager.EzLocalisableString(
            "48k 更佳，过高的值会导致延迟和时钟同步错误。",
            "48k is better, too high a value will cause delays and clock synchronization errors.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_BUFFER_SIZE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 缓冲区大小（测试）",
            "ASIO Buffer Size (Testing)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_BUFFER_SIZE_HINT = new EzLocalizationManager.EzLocalisableString(
            "数值越低延迟越低，但过低可能会导致爆音或无法启动。默认为 128。",
            "Lower is lower latency, but too low may crackle or fail to start. Default is 128.");

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
