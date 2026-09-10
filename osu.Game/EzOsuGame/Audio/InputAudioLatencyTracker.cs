// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.EzLatency;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Audio
{
    /// <summary>
    /// Bridge between gameplay (input / judgement) and framework <see cref="EzLatencyManager"/>.
    /// </summary>
    public partial class InputAudioLatencyTracker : IDisposable
    {
        /// <summary>
        /// Loopback RMS threshold (linear 0–1). Expression-bodied so IDE hot reload can tune without a settings entry.
        /// <para></para>
        /// 信号电平换算公式:
        /// <code>20 * log10(0.02) ≈ -34 dBFS</code>
        /// </summary>
        public static float AcousticRmsThreshold => 0.02f;

        private readonly Ez2ConfigManager ezConfig;
        private readonly INotificationOverlay? notificationOverlay;
        private readonly EzLatencyManager latencyManager;

        private ScoreProcessor? scoreProcessor;
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

            scoreProcessor = processor;

            if (initialized)
            {
                // Re-entering a session: keep bindings, refresh stats and judgement subscription.
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
                $"[EzOsuLatency] tracker armed (threshold={AcousticRmsThreshold:F4}, acousticProbe={(latencyManager.AcousticProbeRunning ? "on" : "off")})",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Debug);
        }

        public void Start()
        {
            if (disposed || started || scoreProcessor == null)
                return;

            started = true;
            pushAcousticThreshold();
            scoreProcessor.NewJudgement += OnNewJudgement;
        }

        public void Stop()
        {
            if (!started)
                return;

            started = false;

            if (scoreProcessor != null)
                scoreProcessor.NewJudgement -= OnNewJudgement;
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
        /// Emit session summary (log + notification). Safe to call from results or exit; only runs once per session.
        /// </summary>
        public void GenerateLatencyReport()
        {
            if (disposed || reportGenerated)
                return;

            reportGenerated = true;
            Stop();

            var stats = latencyManager.GetStatistics();

            if (!stats.HasData)
            {
                Logger.Log("[EzOsuLatency] session ended with no complete records", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            string acousticPart = stats.AcousticRecordCount > 0
                ? $" | Acoustic(loopback) avg/min/max={stats.AvgAcousticRoundtrip:F2}/{stats.MinAcousticRoundtrip:F2}/{stats.MaxAcousticRoundtrip:F2}ms (n={stats.AcousticRecordCount})"
                : string.Empty;

            string summary =
                $"[EzOsuLatency] n={stats.RecordCount}"
                + $" | Input→Audio avg/min/max={stats.AvgInputToPlayback:F2}/{stats.MinInputToPlayback:F2}/{stats.MaxInputToPlayback:F2}ms"
                + $" | Input→Judge avg/min/max={stats.AvgInputToJudge:F2}/{stats.MinInputToJudge:F2}/{stats.MaxInputToJudge:F2}ms"
                + $" | Audio→Judge avg/min/max={stats.AvgPlaybackToJudge:F2}/{stats.MinPlaybackToJudge:F2}/{stats.MaxPlaybackToJudge:F2}ms"
                + acousticPart;

            Logger.Log(summary, Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

            if (notificationOverlay == null)
            {
                Logger.Log("[EzOsuLatency] summary ready but INotificationOverlay is null", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            else
            {
                string notificationText =
                    $"Latency summary (n={stats.RecordCount})\n"
                    + $"Input→Audio  avg {stats.AvgInputToPlayback:F1}  min {stats.MinInputToPlayback:F1}  max {stats.MaxInputToPlayback:F1} ms\n"
                    + $"Input→Judge  avg {stats.AvgInputToJudge:F1}  min {stats.MinInputToJudge:F1}  max {stats.MaxInputToJudge:F1} ms\n"
                    + $"Audio→Judge  avg {stats.AvgPlaybackToJudge:F1}  min {stats.MinPlaybackToJudge:F1}  max {stats.MaxPlaybackToJudge:F1} ms";

                if (stats.AcousticRecordCount > 0)
                {
                    notificationText +=
                        $"\nAcoustic(loopback)  avg {stats.AvgAcousticRoundtrip:F1}  min {stats.MinAcousticRoundtrip:F1}  max {stats.MaxAcousticRoundtrip:F1} ms"
                        + $" (n={stats.AcousticRecordCount})";
                }

                notificationOverlay.Post(new SimpleNotification
                {
                    Text = notificationText,
                    Icon = FontAwesome.Solid.ChartLine,
                });
            }

            latencyManager.ClearStatistics();
        }

        private void pushAcousticThreshold() => latencyManager.SetAcousticThreshold(AcousticRmsThreshold);

        private void onMeasurement(EzLatencyRecord record)
        {
            double softMs = record.PlaybackTime > 0 && record.InputTime > 0
                ? record.PlaybackTime - record.InputTime
                : record.MeasuredMs;

            string acoustic = record.Note == EzLatencyAnalyzer.NOTE_ACOUSTIC_LOOPBACK ||
                              (record.LatencyDifference > 0 && record.HardwareData.IsValid)
                ? $" acoustic={record.LatencyDifference:F2}ms"
                : " acoustic=n/a";

            Logger.Log(
                $"[EzOsuLatency] soft={softMs:F2}ms{acoustic} note={record.Note ?? "?"} key={record.InputData.KeyValue}",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Debug);
        }

        private void OnNewJudgement(JudgementResult result)
        {
            if (!latencyManager.Enabled.Value || !result.Type.IsScorable())
                return;

            bool isNote = result.HitObject.GetType().Name.EndsWith("Note", StringComparison.Ordinal) ||
                          result.HitObject.GetType().Name == "Fruit" ||
                          result.HitObject.GetType().Name == "HitCircle" ||
                          result.HitObject.GetType().Name == "Hit";

            if (isNote)
                latencyManager.RecordJudgeEvent();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            // Last chance if results/exit hooks were skipped (e.g. abrupt teardown).
            if (!reportGenerated)
                GenerateLatencyReport();

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
