// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.LAsEzExtensions.Localization
{
    public static class EzSettingsStrings
    {
        public static readonly EzLocalizationManager.EzLocalisableString INPUT_AUDIO_LATENCY_TRACKER = new EzLocalizationManager.EzLocalisableString("输入音频延迟追踪器", "Input Audio Latency Tracker");

        public static readonly EzLocalizationManager.EzLocalisableString INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(测试功能)启用后可追踪按键输入与音频的延迟, 用于调试和优化打击音效的同步性。在游戏结束后会弹出一个统计窗口。更详细的内容可以查看runtime.log文件。"
            + "\n延迟检测管线：按键 → 检查打击并应用 → 应用判定结果 → 播放note音频",
            "(Testing feature) When enabled, it can track the latency between key input and audio, used for debugging and optimizing the synchronization of hit sound effects. "
            + "A statistics window will pop up after the game ends. More detailed information can be found in the runtime.log file."
            + "\nLatency detection pipeline: Key Press → Check Hit and Apply → Apply Hit Result → Play Note Audio");

        public static readonly EzLocalizationManager.EzLocalisableString ACCURACY_CUTOFF_S = new EzLocalizationManager.EzLocalisableString("Acc S评级线(Mania)", "Accuracy Cutoff S (Mania)");
        public static readonly EzLocalizationManager.EzLocalisableString ACCURACY_CUTOFF_A = new EzLocalizationManager.EzLocalisableString("Acc A评级线(Mania)", "Accuracy Cutoff A (Mania)");

        public static readonly EzLocalizationManager.EzLocalisableString DISABLE_CMD_SPACE =
            new EzLocalizationManager.EzLocalisableString("游戏时禁用 Cmd+Space (聚焦搜索) ", "Disable Cmd+Space (Spotlight) during gameplay");

        public static readonly EzLocalizationManager.EzLocalisableString STORAGE_FOLDER_CREATED =
            new EzLocalizationManager.EzLocalisableString("已创建目录：{0}\n请将文件放入该目录", "Created folder: {0}\nAdd files to the folder");

        public static readonly EzLocalizationManager.EzLocalisableString STORAGE_FOLDER_EMPTY = new EzLocalizationManager.EzLocalisableString("目录为空：{0}", "Folder is empty: {0}");

        public static readonly EzLocalizationManager.EzLocalisableString SETTINGS_TITLE = new EzLocalizationManager.EzLocalisableString("设置", "Settings");
        public static readonly EzLocalizationManager.EzLocalisableString SAVE_BUTTON = new EzLocalizationManager.EzLocalisableString("保存", "Save");
        public static readonly EzLocalizationManager.EzLocalisableString CANCEL_BUTTON = new EzLocalizationManager.EzLocalisableString("取消", "Cancel");

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

        public static readonly EzLocalizationManager.EzLocalisableString SAVE_TO_COLLECTION = new EzLocalizationManager.EzLocalisableString("将当前过滤结果保存到", "Save current filter result to");
    }
}
