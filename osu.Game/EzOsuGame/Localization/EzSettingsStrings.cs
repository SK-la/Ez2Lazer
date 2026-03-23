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

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析重算", "Enable Ez analysis recomputation");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_REC_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "重算开关。开启后允许活动面板进行实时分析计算并刷新显示；关闭后不进行重算。"
            + "\n注意：该开关不控制 SQLite 本地读取与预热（由下方 SQLite 开关单独控制）。",
            "Recomputation switch. When enabled, active panels can run real-time analysis and refresh displayed values; when disabled, no recomputation is performed."
            + "\nNote: This switch does not control SQLite local reads or warmup (handled independently by the SQLite switch below).");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED =
            new EzLocalizationManager.EzLocalisableString("启用 Ez 分析本地数据（SQLite）", "Enable Ez analysis local data (SQLite)");

        public static readonly EzLocalizationManager.EzLocalisableString EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "本地数据开关。独立控制 SQLite 本地读取/写入与启动预热。"
            + "\n开启：面板可读取本地持久化分析数据，并在启动时执行预热。"
            + "\n关闭：不读取 SQLite 本地数据，也不执行预热。",
            "Local-data switch. Independently controls SQLite local read/write and startup warmup."
            + "\nEnabled: Panels can read persisted local analysis data and startup warmup runs."
            + "\nDisabled: No SQLite local reads and no warmup.");

#region 机制类

        public static readonly EzLocalizationManager.EzLocalisableString EZ_GAME_SETTINGS_HEADER = new EzLocalizationManager.EzLocalisableString("Ez Mania 设置", "Ez Mania Settings");

        public static readonly EzLocalizationManager.EzLocalisableString SCORE_SUBMIT_WARNING = new EzLocalizationManager.EzLocalisableString("当前锁定成绩上传", "Currently locking score submission");
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

        public static readonly LocalisableString HIT_MODE = new EzLocalizationManager.EzLocalisableString("Mania 判定系统", "(Mania) Hit Mode");

        public static readonly LocalisableString HIT_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Mania 判定系统, 获得不同音游的打击体验, 但是不保证所有模式都完全一比一复刻"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\n 305     300      200      100     50     Miss    Poor"
            + "\n16.67    33.33    116.67      -      250    250    500    IIDX"
            + "\n15.00   30.00   60.00      -     200    1000  1000   LR2 Hard"
            + "\n15.00   45.00    112.00     -      165     500    500   Raja Normal"
            + "\n20.00  60.00   150.00    -      500    500    500   Raja Easy",
            "(Mania) Hit Mode, get different rhythm game hit experiences, but not guaranteed to perfectly replicate all modes"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\n 305     300      200      100     50     Miss    Poor"
            + "\n16.67    33.33    116.67      -      250    250    500    IIDX"
            + "\n15.00   30.00   60.00      -     200    1000  1000   LR2 Hard"
            + "\n15.00   45.00    112.00     -      165     500    500   Raja Normal"
            + "\n20.00  60.00   150.00    -      500    500    500   Raja Easy");

        public static readonly LocalisableString HEALTH_MODE = new EzLocalizationManager.EzLocalisableString("Mania 血量系统", "(Mania) Health Mode");

        public static readonly LocalisableString HEALTH_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "\n——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\n 305    300    200   100    50   Miss       -"
            + "\n0.4%   0.3%   0.1%    0%   -1%   - 6%     -0%  Lazer"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\nKool        -   Good       -   Bad   Miss         -"
            + "\n0.3%   0.0%   0.2%    0%   -1%   - 5%     -0%  O2 Easy"
            + "\n0.2%   0.0%   0.1%    0%   -7%   - 4%     -0%  O2 Normal"
            + "\n0.1%   0.0%   0.0%    0%   -5%   - 3%     -0%  O2 Hard"
            + "\n——— ——— ——— ——— ——— ——— ——— ——— ——— ———"
            + "\nKool   Cool   Good      -   Bad   Poor  []Poor"
            + "\n0.4%   0.3%    0.1%    0%   -1%   - 5%      -5%  Ez2Ac"
            + "\n1.6%   1.6%    0.0%    0%   -5%   - 9%      -5%  IIDX Hard"
            + "\n1.0%   1.0%    0.5%    0%   -6%   -10%      -2%  LR2 Hard"
            + "\n1.2%   1.2%    0.6%    0%   -3%   - 6%      -2%  raja normal");

        public static readonly LocalisableString POOR_HIT_RESULT = new EzLocalizationManager.EzLocalisableString("增加 Poor 判定类型", "Additional Poor HitResult");

        public static readonly LocalisableString POOR_HIT_RESULT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "Pool判定类型只在BMS系血量系统下生效, 用于严格扣血, 不影响Combo、Score\n"
            + "一个note可触发多个Pool判定, 只有早于Miss时才会触发, 不存在晚Pool",
            "The Poor HitResult type only takes effect under the BMS Health Mode, used for strict health deduction, does not affect Combo or Score\n"
            + "One note can trigger multiple Poor hit results, and it will only trigger if it is earlier than Miss, there is no late Poor");

#endregion

        public static readonly EzLocalizationManager.EzLocalisableString INPUT_AUDIO_LATENCY_TRACKER = new EzLocalizationManager.EzLocalisableString("输入音频延迟追踪器", "Input Audio Latency Tracker");

        public static readonly EzLocalizationManager.EzLocalisableString INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(测试功能)启用后可追踪按键输入与音频的延迟, 用于调试和优化打击音效的同步性。在游戏结束后会弹出一个统计窗口。更详细的内容可以查看runtime.log文件。"
            + "\n延迟检测管线：按键 → 检查打击并应用 → 应用判定结果 → 播放note音频",
            "(Testing feature) When enabled, it can track the latency between key input and audio, used for debugging and optimizing the synchronization of hit sound effects. "
            + "A statistics window will pop up after the game ends. More detailed information can be found in the runtime.log file."
            + "\nLatency detection pipeline: Key Press → Check Hit and Apply → Apply Hit Result → Play Note Audio");
    }
}
