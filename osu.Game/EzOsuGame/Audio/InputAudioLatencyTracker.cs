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
        private readonly Ez2ConfigManager ezConfig;
        private readonly INotificationOverlay? notificationOverlay;
        private readonly EzLatencyManager latencyManager;

        private ScoreProcessor? scoreProcessor;
        private Bindable<bool>? inputAudioLatencyConfigBindable;
        private Action<ValueChangedEvent<bool>>? inputAudioLatencyConfigHandler;
        private Action<ValueChangedEvent<bool>>? enabledChangedHandler;
        private bool initialized;
        private bool started;
        private bool disposed;

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
                if (latencyManager.Enabled.Value)
                    Start();
                return;
            }

            initialized = true;
            latencyManager.ClearStatistics();

            inputAudioLatencyConfigBindable = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker);
            inputAudioLatencyConfigHandler = v => latencyManager.Enabled.Value = v.NewValue;
            inputAudioLatencyConfigBindable.BindValueChanged(inputAudioLatencyConfigHandler, true);

            // Do not subscribe OnNewRecord for per-hit logging — session summary only (avoids audio-thread log spam).

            enabledChangedHandler = enabled =>
            {
                if (enabled.NewValue)
                    Start();
                else
                    Stop();
            };
            latencyManager.Enabled.BindValueChanged(enabledChangedHandler, true);

            Logger.Log("[EzOsuLatency] tracker armed for gameplay session", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }

        public void Start()
        {
            if (disposed || started || scoreProcessor == null)
                return;

            started = true;
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
            if (latencyManager.Enabled.Value)
                latencyManager.RecordInputEvent(key);
        }

        /// <summary>
        /// Mania column press (may arrive shortly after framework KeyDown; analyzer coalesces within 2ms).
        /// </summary>
        public void RecordColumnPress(int column)
        {
            if (latencyManager.Enabled.Value)
                latencyManager.RecordInputEvent(column);
        }

        public void GenerateLatencyReport()
        {
            if (disposed)
                return;

            Stop();

            var stats = latencyManager.GetStatistics();

            if (!stats.HasData)
            {
                Logger.Log("[EzOsuLatency] session ended with no complete records", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            Logger.Log(
                $"[EzOsuLatency] Input→Judge={stats.AvgInputToJudge:F2}ms Input→Audio={stats.AvgInputToPlayback:F2}ms Audio→Judge={stats.AvgPlaybackToJudge:F2}ms n={stats.RecordCount}",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Debug);

            notificationOverlay?.Post(new SimpleNotification
            {
                Text =
                    $"Latency analysis complete!\nInput→Judge: {stats.AvgInputToJudge:F1}ms\nInput→Audio: {stats.AvgInputToPlayback:F1}ms\nAudio→Judge: {stats.AvgPlaybackToJudge:F1}ms\nRecords: {stats.RecordCount}",
                Icon = FontAwesome.Solid.ChartLine,
            });

            latencyManager.ClearStatistics();
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

            disposed = true;
            Stop();

            if (enabledChangedHandler != null)
                latencyManager.Enabled.ValueChanged -= enabledChangedHandler;

            if (inputAudioLatencyConfigBindable != null && inputAudioLatencyConfigHandler != null)
                inputAudioLatencyConfigBindable.ValueChanged -= inputAudioLatencyConfigHandler;

            // Critical: do not leave GLOBAL.Enabled true after leaving gameplay.
            latencyManager.Enabled.Value = false;
            latencyManager.ClearStatistics();

            if (Instance == this)
                Instance = null;

            Logger.Log("[EzOsuLatency] tracker disposed; GLOBAL.Enabled=false", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }
    }
}
