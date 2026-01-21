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
    /// Unified latency measurement manager that coordinates between game input events and the EzLatency system.
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

        private EzLatencyManager latencyManager;

        /// <summary>
        /// Global instance for unified access
        /// </summary>
        public static InputAudioLatencyTracker? Instance { get; private set; }

        public InputAudioLatencyTracker(Ez2ConfigManager ez2ConfigManager)
        {
            ezConfig = ez2ConfigManager;
            Instance = this;

            // 使用全局的 EzLatencyManager 实例以与框架层的全局插桩一致
            latencyManager = EzLatencyManager.GLOBAL;
        }

        public void Initialize(ScoreProcessor processor)
        {
            Logger.Log($"InputAudioLatencyTracker.Initialize called", LoggingTarget.Runtime, LogLevel.Debug);
            scoreProcessor = processor;

            // 将 Ez2Setting 的启用状态绑定到 EzLatencyManager
            var configBindable = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker);
            latencyManager.Enabled.BindTo(configBindable);

            // 订阅延迟记录事件，用于日志输出
            latencyManager.OnNewRecord += OnLatencyRecordGenerated;

            // 绑定启用状态变化，控制生命周期
            latencyManager.Enabled.BindValueChanged(enabled =>
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

            Logger.Log($"InputAudioLatencyTracker.Start called", LoggingTarget.Runtime, LogLevel.Debug);

            if (scoreProcessor != null)
                scoreProcessor.NewJudgement += OnNewJudgement;
        }

        public void Stop()
        {
            if (!started) return;

            started = false;

            if (scoreProcessor != null)
                scoreProcessor.NewJudgement -= OnNewJudgement;
        }

        /// <summary>
        /// Records a key press event for latency measurement.
        /// Call this when the player presses a key.
        /// </summary>
        /// <param name="key">The key that was pressed</param>
        public void RecordKeyPress(Key key)
        {
            if (latencyManager.Enabled.Value)
            {
                // 记录输入事件
                latencyManager.RecordInputEvent(key);
            }
        }

        /// <summary>
        /// Records a column press event for mania ruleset.
        /// </summary>
        /// <param name="column">The column that was pressed</param>
        public void RecordColumnPress(int column)
        {
            if (latencyManager.Enabled.Value)
            {
                // 记录输入事件 (使用 column 作为标识)
                latencyManager.RecordInputEvent(column);
            }
        }

        /// <summary>
        /// Call this when the game exits to generate latency statistics.
        /// </summary>
        public void GenerateLatencyReport()
        {
            if (!latencyManager.Enabled.Value)
                return;

            // 停止收集新数据
            Stop();

            // 从 EzLatencyManager 获取统计数据
            var stats = latencyManager.GetStatistics();

            if (!stats.HasData)
            {
                Logger.Log($"[EzOsuLatency] No latency data available for analysis", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            // 输出统计日志
            string message1 = $"Input→Judgement: {stats.AvgInputToJudge:F2}ms, Input→Audio: {stats.AvgInputToPlayback:F2}ms, Audio→Judgement: {stats.AvgPlaybackToJudge:F2}ms (based on {stats.RecordCount} complete records)";
            string message2 = $"Input→Judgement: {stats.AvgInputToJudge:F2}ms, \nInput→Audio: {stats.AvgInputToPlayback:F2}ms, \nAudio→Judgement: {stats.AvgPlaybackToJudge:F2}ms \n(based on {stats.RecordCount} complete records)";

            Logger.Log($"[EzOsuLatency] Latency Analysis: {message1}");
            Logger.Log($"[EzOsuLatency] Latency Analysis: \n{message2}", LoggingTarget.Runtime, LogLevel.Important);

            // 显示通知
            if (notificationOverlay != null)
            {
                notificationOverlay.Post(new SimpleNotification
                {
                    Text = $"Latency analysis complete!\nInput→Judge: {stats.AvgInputToJudge:F1}ms\nInput→Audio: {stats.AvgInputToPlayback:F1}ms\nAudio→Judge: {stats.AvgPlaybackToJudge:F1}ms\nRecords: {stats.RecordCount}",
                    Icon = FontAwesome.Solid.ChartLine,
                });
            }
        }

        private void OnNewJudgement(JudgementResult result)
        {
            if (!latencyManager.Enabled.Value)
                return;

            if (result.Type.IsScorable())
            {
                // 检查是否为普通note且判定为Perfect
                bool isNote = result.HitObject.GetType().Name.EndsWith("Note", StringComparison.Ordinal) ||
                              result.HitObject.GetType().Name == "Fruit" ||
                              result.HitObject.GetType().Name == "HitCircle" ||
                              result.HitObject.GetType().Name == "Hit";

                // 记录所有可计分的 note 判定，以便收集判定时间戳（不局限于 Perfect）
                if (isNote)
                {
                    latencyManager.RecordJudgeEvent();
                }
            }
        }

        public void Dispose()
        {
            Stop();

            // 解绑事件
            if (latencyManager != null)
            {
                latencyManager.OnNewRecord -= OnLatencyRecordGenerated;
                latencyManager.Dispose();
            }

            Instance = null;
        }

        /// <summary>
        /// 处理从 framework 层传来的延迟记录，输出详细日志
        /// </summary>
        private void OnLatencyRecordGenerated(EzLatencyRecord r)
        {
            try
            {
                var inputData = r.InputData;
                var hw = r.HardwareData;

                string keyVal = inputData.KeyValue?.ToString() ?? "-";

                string line = $"[EzOsuLatency] {r.Timestamp:O} | {r.MeasuredMs:F2} ms | note={r.Note} | in={r.InputTime:F2} | key={keyVal} | play={r.PlaybackTime:F2} | judge={r.JudgeTime:F2} | driver={r.DriverTime:F2} | out_hw={r.OutputHardwareTime:F2} | in_hw={r.InputHardwareTime:F2} | diff={r.LatencyDifference:F2}";

                // extra low-level structs
                string extra = $" | input_struct=(in={inputData.InputTime:F2}, key={inputData.KeyValue ?? "-"}, judge={inputData.JudgeTime:F2}, play={inputData.PlaybackTime:F2})" +
                               $" | hw_struct=(driver={hw.DriverTime:F2}, out_hw={hw.OutputHardwareTime:F2}, in_hw={hw.InputHardwareTime:F2}, diff={hw.LatencyDifference:F2})";

                Logger.Log(line + extra, LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"InputAudioLatencyTracker: failed to handle new record: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }
    }
}
