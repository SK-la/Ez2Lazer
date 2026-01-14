// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.EzLatency;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK.Input;

namespace osu.Game.LAsEzExtensions.Audio
{
    /// <summary>
    /// Unified latency measurement manager that coordinates between game input events and the EzLogModule system.
    /// Acts as the central bridge between osu! game events and framework-level latency tracking.
    /// </summary>
    public partial class InputAudioLatencyTracker : IDisposable
    {
        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private AudioManager? audioManager { get; set; }

        private Ez2ConfigManager ezConfig { get; set; }

        private ScoreProcessor? scoreProcessor;

        private Action<EzLatencyRecord>? measurementHandler;

        // 开关状态缓存
        private bool isEnabled;

        /// <summary>
        /// Global instance for unified access
        /// </summary>
        public static InputAudioLatencyTracker? Instance { get; private set; }

        public InputAudioLatencyTracker(Ez2ConfigManager ez2ConfigManager)
        {
            ezConfig = ez2ConfigManager;
            Instance = this;
        }

        public void Initialize(ScoreProcessor processor)
        {
            isEnabled = ezConfig.Get<bool>(Ez2Setting.InputAudioLatencyTracker);
            scoreProcessor = processor;

            // 绑定到配置变化，统一控制EzOsuLatency总开关
            ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker).BindValueChanged(enabled =>
            {
                isEnabled = enabled.NewValue;

                // 通过framework的总开关接口统一控制

                // 管理所有Ez模块的启用状态
                EzLogModule.Instance.Enabled = isEnabled;
                EzInputModule.Enabled = isEnabled;
                EzJudgeModule.Enabled = isEnabled;
            });

            // 设置初始状态

            EzLogModule.Instance.Enabled = isEnabled;
            EzInputModule.Enabled = isEnabled;
            EzJudgeModule.Enabled = isEnabled;

            // Do not attach event handlers here. Start/Stop controls lifecycle.
            measurementHandler = logFrameworkRecord;

            // bind enable/disable to Start/Stop
            ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker).BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Start();
                else
                    Stop();
            }, true);
        }

        private bool started;

        public void Start()
        {
            if (started) return;

            started = true;

            if (scoreProcessor != null)
                scoreProcessor.NewJudgement += OnNewJudgement;

            try
            {
                if (measurementHandler != null)
                    EzLatencyService.Instance.OnMeasurement += measurementHandler;
            }
            catch { }
        }

        public void Stop()
        {
            if (!started) return;

            started = false;

            if (scoreProcessor != null)
                scoreProcessor.NewJudgement -= OnNewJudgement;

            try
            {
                if (measurementHandler != null)
                    EzLatencyService.Instance.OnMeasurement -= measurementHandler;
            }
            catch { }
        }

        /// <summary>
        /// Records a key press event for latency measurement.
        /// Call this when the player presses a key.
        /// </summary>
        /// <param name="key">The key that was pressed</param>
        public void RecordKeyPress(Key key)
        {
            if (isEnabled)
            {
                // EzOsuLatency: 记录输入时间戳 (T_input)
                EzInputModule.RecordTimestamp(DateTime.Now, key);
            }
        }

        /// <summary>
        /// Records a column press event for mania ruleset.
        /// </summary>
        /// <param name="column">The column that was pressed</param>
        public void RecordColumnPress(int column)
        {
            if (isEnabled)
            {
                // EzOsuLatency: 记录输入时间戳 (T_input) for mania
                EzInputModule.RecordTimestamp(DateTime.Now, (Key)(Key.Z + column));
            }
        }

        /// <summary>
        /// Call this when the game exits to generate latency statistics.
        /// </summary>
        public void GenerateLatencyReport()
        {
            if (!isEnabled)
                return;

            if (scoreProcessor != null)
                scoreProcessor.NewJudgement -= OnNewJudgement;

            // 从 EzLogModule 获取统计数据
            var stats = EzLogModule.Instance.GetLatencyStatistics();

            if (!stats.HasData)
            {
                Logger.Log($"[EzOsuLatency] No latency data available for analysis", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            // 由 InputAudioLatencyTracker 负责输出日志
            string message1 = $"Input->Judgement: {stats.AvgInputToJudge:F2}ms, Input->Audio: {stats.AvgInputToPlayback:F2}ms, Audio->Judgement: {stats.AvgPlaybackToJudge:F2}ms (based on {stats.RecordCount} complete records)";
            string message2 = $"Input->Judgement: {stats.AvgInputToJudge:F2}ms, \nInput->Audio: {stats.AvgInputToPlayback:F2}ms, \nAudio->Judgement: {stats.AvgPlaybackToJudge:F2}ms \n(based on {stats.RecordCount} complete records)";

            Logger.Log($"[EzOsuLatency] Latency Analysis: {message1}");
            Logger.Log($"[EzOsuLatency] Latency Analysis: \n{message2}", LoggingTarget.Runtime, LogLevel.Important);

            // 显示通知
            if (notificationOverlay != null)
            {
                notificationOverlay.Post(new SimpleNotification
                {
                    Text = $"Latency analysis complete!\nInput→Judge: {stats.AvgInputToJudge:F1}ms\nInput→Audio: {stats.AvgInputToPlayback:F1}ms\nAudio→Judge: {stats.AvgPlaybackToJudge:F1}ms",
                    Icon = FontAwesome.Solid.ChartLine,
                });
            }
        }

        private void OnNewJudgement(JudgementResult result)
        {
            if (!isEnabled)
                return;

            if (result.Type.IsScorable())
            {
                // 检查是否为普通note且判定为Perfect
                bool isNote = result.HitObject.GetType().Name.EndsWith("Note", StringComparison.Ordinal) ||
                              result.HitObject.GetType().Name == "Fruit" ||
                              result.HitObject.GetType().Name == "HitCircle" ||
                              result.HitObject.GetType().Name == "Hit";

                bool isPerfect = result.Type == HitResult.Perfect;

                if (isNote && isPerfect && EzInputModule.InputTime > 0)
                {
                    // EzOsuLatency: 记录判定时间戳 (T_judge) - 只记录普通note的Perfect判定且有对应输入
                    EzJudgeModule.RecordTimestamp(DateTime.Now);
                }
                else if (!isNote || !isPerfect)
                {
                    // 如果不是普通note或不是Perfect判定，清空之前记录的输入时间戳，避免记录不完整的延迟数据
                    EzInputModule.ClearInputData();
                }
            }
        }

        public void Dispose()
        {
            Stop();

            // 清理所有Ez模块的状态
            EzLogModule.Instance.Enabled = false;
            EzInputModule.Enabled = false;
            EzJudgeModule.Enabled = false;

            Instance = null;
        }

        private void logFrameworkRecord(EzLatencyRecord r)
        {
            try
            {
                var inputData = r.InputData;
                var hw = r.HardwareData;

                string keyVal = inputData.KeyValue?.ToString() ?? (r.InputTime > 0 ? r.InputTime.ToString("F2") : "-");

                string line = $"[EzOsuLatency] {r.Timestamp:O} | {r.MeasuredMs:F2} ms | note={r.Note} | in={r.InputTime:F2} | key={keyVal} | play={r.PlaybackTime:F2} | judge={r.JudgeTime:F2} | driver={r.DriverTime:F2} | out_hw={r.OutputHardwareTime:F2} | in_hw={r.InputHardwareTime:F2} | diff={r.LatencyDifference:F2}";

                // extra low-level structs
                string extra = $" | input_struct=(in={inputData.InputTime:F2}, key={inputData.KeyValue ?? "-"}, judge={inputData.JudgeTime:F2}, play={inputData.PlaybackTime:F2})" +
                               $" | hw_struct=(driver={hw.DriverTime:F2}, out_hw={hw.OutputHardwareTime:F2}, in_hw={hw.InputHardwareTime:F2}, diff={hw.LatencyDifference:F2})";

                Logger.Log(line + extra, LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch { }
        }
    }
}
