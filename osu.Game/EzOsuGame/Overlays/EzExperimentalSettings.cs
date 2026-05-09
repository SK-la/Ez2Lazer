// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzExperimentalSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EZ_EXPERIMENTAL_SECTION_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzSubFrameCorrectionEnabled),
                    Caption = EZ_SUB_FRAME_CORRECTION_ENABLED,
                    HintText = EZ_SUB_FRAME_CORRECTION_ENABLED_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzJudgmentDiagEnabled),
                    Caption = EZ_JUDGMENT_DIAG_ENABLED,
                    HintText = EZ_JUDGMENT_DIAG_ENABLED_TOOLTIP,
                }),

                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzTimingTraceEnabled),
                    Caption = EZ_TIMING_TRACE_ENABLED,
                    HintText = EZ_TIMING_TRACE_ENABLED_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                    Caption = INPUT_AUDIO_LATENCY_TRACKER,
                    HintText = INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP,
                })
                {
                    Keywords = new[] { "latency", "audio", "input" }
                },
            });
        }

        internal static readonly LocalisableString EZ_EXPERIMENTAL_SECTION_HEADER = new EzLocalizationManager.EzLocalisableString(
            "实验性功能", "Experimental Features");

        internal static readonly LocalisableString EZ_SUB_FRAME_CORRECTION_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 子帧时序校正", "Enable Ez Sub-frame Timing Correction");

        internal static readonly LocalisableString EZ_SUB_FRAME_CORRECTION_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "子帧时序校正：利用前一帧的时钟值来补偿判断。"
            + "\n按键在上一次 FSC 时钟刷新和现在之间被按下；插值到实际按键时间。"
            + "\n可以理解为改用相对于上一帧的时间进行判定，而不是主时轴绝对时间。",
            "Sub-frame timing correction: compensate for judgment using previous frame's clock value."
            + "\nThe key was pressed between the last FSC clock update and now; interpolate to the actual press time."
            + "\nThis can be understood as using time relative to the previous frame for judgment, rather than the absolute time of the main timeline.");

        internal static readonly LocalisableString EZ_JUDGMENT_DIAG_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 判定诊断", "Enable Ez Judgment Diagnostics");

        internal static readonly LocalisableString EZ_JUDGMENT_DIAG_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(研究功能)在游戏结束后，输出本局判定信息到.csv文件。"
            + "\n默认输出路径：桌面/EzDiag/",
            "(Testing feature) Output judgment information to a .csv file after the game ends."
            + "\nDefault output path: Desktop/EzDiag/");

        internal static readonly LocalisableString EZ_TIMING_TRACE_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 时序追踪", "Enable Ez Timing Trace");

        internal static readonly LocalisableString EZ_TIMING_TRACE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(研究功能)在游戏结束后，输出本局判定信息到.csv文件。"
            + "\n追踪按键输入与音频的时序关系, 用于检查打击音效的时序。"
            + "\n默认输出路径：桌面/EzDiag/",
            "(Testing feature) Output judgment information to a .csv file after the game ends."
            + "\nTrack the timing relationship between key input and audio, used to check the timing of hit sounds."
            + "\nDefault output path: Desktop/EzDiag/");

        internal static readonly LocalisableString INPUT_AUDIO_LATENCY_TRACKER = new EzLocalizationManager.EzLocalisableString(
            "输入音频延迟追踪器", "Input Audio Latency Tracker");

        internal static readonly LocalisableString INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(测试功能)启用后可追踪按键输入与音频的延迟, 用于调试和优化打击音效的同步性。在游戏结束后会弹出一个统计窗口。更详细的内容可以查看runtime.log文件。"
            + "\n延迟检测管线：按键 → 检查打击并应用 → 应用判定结果 → 播放note音频",
            "(Testing feature) When enabled, it can track the latency between key input and audio, used for debugging and optimizing the synchronization of hit sound effects. "
            + "A statistics window will pop up after the game ends. More detailed information can be found in the runtime.log file."
            + "\nLatency detection pipeline: Key Press → Check Hit and Apply → Apply Hit Result → Play Note Audio");
    }
}
