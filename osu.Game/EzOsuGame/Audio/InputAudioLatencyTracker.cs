// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Audio.EzLatency;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Scoring;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Audio
{
    /// <summary>
    /// 音频闭环延迟桥：输入戳 → <c>Sample.Play</c> → 输出路径 PCM 过阈值
    /// （Default / Shared / Exclusive / ASIO；非麦克风 loopback）。
    /// 判定耗时（In→Judge）归 <see cref="Diagnostics.EzJudgmentDiagnostics"/>，不在此追踪。
    /// </summary>
    public partial class InputAudioLatencyTracker : IDisposable
    {
        /// <summary>
        /// 输出路径 RMS 阈值（线性 0–1）。表达式属性便于 IDE 热重载调试，无设置项。
        /// 电平换算：<c>20 * log10(0.08) ≈ -22 dBFS</c>。
        /// </summary>
        public static float AcousticRmsThreshold => 0.08f;

        private readonly Ez2ConfigManager ezConfig;
        private readonly INotificationOverlay? notificationOverlay;
        private readonly EzLatencyManager latencyManager;

        private Bindable<bool>? inputAudioLatencyConfigBindable;
        private Action<ValueChangedEvent<bool>>? inputAudioLatencyConfigHandler;
        private Action<ValueChangedEvent<bool>>? enabledChangedHandler;
        private Action<EzLatencyRecord>? measurementHandler;
        private bool initialized;
        private bool started;
        private bool disposed;
        private bool reportGenerated;

        public static InputAudioLatencyTracker? Instance { get; private set; }

        public InputAudioLatencyTracker(Ez2ConfigManager ez2ConfigManager, INotificationOverlay? notificationOverlay = null)
        {
            ezConfig = ez2ConfigManager;
            this.notificationOverlay = notificationOverlay;
            Instance = this;
            latencyManager = EzLatencyManager.GLOBAL;
        }

        public void Initialize(ScoreProcessor processor)
        {
            if (disposed)
                return;

            // processor kept for call-site compatibility (Player still passes ScoreProcessor).
            _ = processor;

            if (initialized)
            {
                latencyManager.ClearStatistics();
                reportGenerated = false;
                pushAcousticThreshold();
                if (latencyManager.Enabled.Value)
                    Start();
                return;
            }

            initialized = true;
            reportGenerated = false;
            latencyManager.ClearStatistics();

            inputAudioLatencyConfigBindable = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker);
            inputAudioLatencyConfigHandler = v => latencyManager.Enabled.Value = v.NewValue;
            inputAudioLatencyConfigBindable.BindValueChanged(inputAudioLatencyConfigHandler, true);

            enabledChangedHandler = enabled =>
            {
                if (enabled.NewValue)
                    Start();
                else
                    Stop();
            };
            latencyManager.Enabled.BindValueChanged(enabledChangedHandler, true);

            measurementHandler = onMeasurement;
            latencyManager.OnNewRecord += measurementHandler;

            Logger.Log(
                $"[EzOsuLatency] audio tracker armed (In→Play→Acou; threshold={AcousticRmsThreshold:F4}, acousticProbe={(latencyManager.AcousticProbeRunning ? "on" : "off")})",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Debug);
        }

        public void Start()
        {
            if (disposed || started)
                return;

            started = true;
            pushAcousticThreshold();
        }

        public void Stop()
        {
            started = false;
        }

        public void RecordKeyPress(Key key)
        {
            if (!latencyManager.Enabled.Value)
                return;

            pushAcousticThreshold();
            latencyManager.RecordInputEvent(key);
        }

        /// <summary>
        /// Mania column press (may follow framework KeyDown for the same key; analyzer upgrades Key→column only).
        /// </summary>
        public void RecordColumnPress(int column)
        {
            if (!latencyManager.Enabled.Value)
                return;

            pushAcousticThreshold();
            latencyManager.RecordInputEvent(column);
        }

        /// <summary>
        /// Emit session summary. Always writes ez_runtime log on exit.
        /// Toast is only for a completed play (<paramref name="postNotification"/>), deferred so
        /// <c>InGameFocus</c> can show a toast after leaving <c>Player</c>.
        /// </summary>
        public void GenerateLatencyReport(bool postNotification = false)
        {
            if (disposed || reportGenerated)
                return;

            reportGenerated = true;
            Stop();

            var stats = latencyManager.GetStatistics();

            if (!stats.HasData)
            {
                Logger.Log("[EzOsuLatency] session ended with no complete records", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                if (postNotification)
                    postSummaryNotification("Audio latency: no complete samples this play.");

                latencyManager.ClearStatistics();
                return;
            }

            string playToAcouPart = string.Empty;
            string acousticPart = string.Empty;

            if (stats.AcousticRecordCount > 0)
            {
                double playToAcouAvg = stats.AvgAcousticRoundtrip - stats.AvgInputToPlayback;
                acousticPart =
                    $" | In→Acou avg/min/max={stats.AvgAcousticRoundtrip:F2}/{stats.MinAcousticRoundtrip:F2}/{stats.MaxAcousticRoundtrip:F2}ms (n={stats.AcousticRecordCount})";
                playToAcouPart = $" | Play→Acou≈{playToAcouAvg:F2}ms (avg)";
            }

            string summary =
                $"[EzOsuLatency] n={stats.RecordCount}"
                + $" | In→Play avg/min/max={stats.AvgInputToPlayback:F2}/{stats.MinInputToPlayback:F2}/{stats.MaxInputToPlayback:F2}ms"
                + playToAcouPart
                + acousticPart;

            Logger.Log(summary, Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

            if (postNotification)
            {
                string notificationText =
                    $"Audio latency (n={stats.RecordCount})\n"
                    + $"In→Play  avg {stats.AvgInputToPlayback:F1}  min {stats.MinInputToPlayback:F1}  max {stats.MaxInputToPlayback:F1} ms";

                if (stats.AcousticRecordCount > 0)
                {
                    double playToAcouAvg = stats.AvgAcousticRoundtrip - stats.AvgInputToPlayback;
                    notificationText +=
                        $"\nPlay→Acou  ≈ {playToAcouAvg:F1} ms (avg)"
                        + $"\nIn→Acou  avg {stats.AvgAcousticRoundtrip:F1}  min {stats.MinAcousticRoundtrip:F1}  max {stats.MaxAcousticRoundtrip:F1} ms"
                        + $" (n={stats.AcousticRecordCount})";
                }

                postSummaryNotification(notificationText);
            }

            latencyManager.ClearStatistics();
        }

        private void postSummaryNotification(string text)
        {
            if (notificationOverlay == null)
            {
                Logger.Log("[EzOsuLatency] summary ready but INotificationOverlay is null", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            var overlay = notificationOverlay;
            var notification = new SimpleNotification
            {
                Text = text,
                Icon = FontAwesome.Solid.ChartLine,
            };

            // Defer past Player/PlayerLoader so InGameFocus allows toast + sound.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300).ConfigureAwait(false);
                    overlay.Post(notification);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[EzOsuLatency] deferred notification failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                }
            });
        }

        private void pushAcousticThreshold() => latencyManager.SetAcousticThreshold(AcousticRmsThreshold);

        private static string formatMs(double? ms) => ms.HasValue ? $"{ms.Value:F2}" : "n/a";

        private void onMeasurement(EzLatencyRecord record)
        {
            double? inToPlay = record.PlaybackTime > 0 && record.InputTime > 0
                ? record.PlaybackTime - record.InputTime
                : null;

            bool hasAcou = record.Note == EzLatencyAnalyzer.NOTE_ACOUSTIC_LOOPBACK
                           || (record.LatencyDifference > 0 && record.HardwareData.IsValid);

            double? inToAcou = hasAcou ? record.LatencyDifference : null;
            double? playToAcou = inToPlay.HasValue && inToAcou.HasValue
                ? inToAcou.Value - inToPlay.Value
                : null;

            Logger.Log(
                $"[EzOsuLatency] key={record.InputData.KeyValue} | In→Play={formatMs(inToPlay)} | Play→Acou={formatMs(playToAcou)} | In→Acou={formatMs(inToAcou)}",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Debug);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            // Exit/teardown: log only, never toast.
            if (!reportGenerated)
                GenerateLatencyReport(postNotification: false);

            disposed = true;
            Stop();

            if (enabledChangedHandler != null)
                latencyManager.Enabled.ValueChanged -= enabledChangedHandler;

            if (inputAudioLatencyConfigBindable != null && inputAudioLatencyConfigHandler != null)
                inputAudioLatencyConfigBindable.ValueChanged -= inputAudioLatencyConfigHandler;

            if (measurementHandler != null)
                latencyManager.OnNewRecord -= measurementHandler;

            // Critical: do not leave GLOBAL.Enabled true after leaving gameplay.
            latencyManager.Enabled.Value = false;
            latencyManager.ClearStatistics();

            if (Instance == this)
                Instance = null;

            Logger.Log("[EzOsuLatency] tracker disposed; GLOBAL.Enabled=false", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }
    }
}
