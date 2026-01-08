// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Logging;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osuTK.Input;

namespace osu.Game.Audio
{
    /// <summary>
    /// Tracks latency between physical input, judgement, and audio playback for hit notes.
    /// Logs detailed timing information and provides analysis on game exit.
    /// </summary>
    public partial class InputAudioLatencyTracker : IDisposable
    {
        private readonly List<LatencyRecord> records = new List<LatencyRecord>();
        private readonly Stopwatch stopwatch = new Stopwatch();

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        private ScoreProcessor? scoreProcessor;

        // Store the last key press time for matching with judgement
        private double lastKeyPressTime;
        private Key lastKey;

        public static InputAudioLatencyTracker GlobalTracker { get; set; }

        public InputAudioLatencyTracker()
        {
            stopwatch.Start();
            GlobalTracker = this;
        }

        public void Initialize(ScoreProcessor processor)
        {
            scoreProcessor = processor;
            scoreProcessor.NewJudgement += OnNewJudgement;
        }

        public void RecordKeyPress(Key key)
        {
            lastKeyPressTime = stopwatch.Elapsed.TotalMilliseconds;
            lastKey = key;
            Logger.Log($"[InputAudioLatency] Key press: {key} at {lastKeyPressTime:F2}ms", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        public void RecordAudioPlay()
        {
            double timeMs = stopwatch.Elapsed.TotalMilliseconds;
            Logger.Log($"[InputAudioLatency] Audio play at {timeMs:F2}ms", LoggingTarget.Runtime, LogLevel.Verbose);

            var lastRecord = records.LastOrDefault(r => r.AudioPlayTimeMs == 0);
            if (lastRecord.AudioPlayTimeMs == 0 && records.Count > 0 && records.Last().AudioPlayTimeMs == 0)
            {
                records[records.Count - 1] = new LatencyRecord
                {
                    KeyPressTimeMs = lastRecord.KeyPressTimeMs,
                    JudgementTimeMs = lastRecord.JudgementTimeMs,
                    AudioPlayTimeMs = timeMs
                };
            }
        }

        private void OnNewJudgement(JudgementResult result)
        {
            if (result.Type.IsScorable())
            {
                double judgementTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                Logger.Log($"[InputAudioLatency] Judgement at {judgementTimeMs:F2}ms for {result.HitObject.GetType().Name}", LoggingTarget.Runtime, LogLevel.Verbose);

                var record = new LatencyRecord
                {
                    KeyPressTimeMs = lastKeyPressTime, // Use real key press time
                    JudgementTimeMs = judgementTimeMs,
                    AudioPlayTimeMs = 0 // Will be set when audio plays
                };
                records.Add(record);
            }
        }

        /// <summary>
        /// Called when the game exits to analyze and notify.
        /// </summary>
        public void OnGameExit()
        {
            if (scoreProcessor != null)
                scoreProcessor.NewJudgement -= OnNewJudgement;

            if (records.Count == 0)
                return;

            var validRecords = records.Where(r => r.AudioPlayTimeMs > 0).ToList();
            if (validRecords.Count == 0)
                return;

            double avgInputLatency = validRecords.Average(r => r.InputLatencyMs);
            double avgAudioLatency = validRecords.Average(r => r.AudioLatencyMs);
            double avgSuggestedOffset = validRecords.Average(r => r.SuggestedOffsetMs);

            string message = $"Average input latency: {avgInputLatency:F2}ms, Audio playback latency: {avgAudioLatency:F2}ms, Suggested offset adjustment: {avgSuggestedOffset:F2}ms (based on {validRecords.Count} hits)";

            Logger.Log($"[InputAudioLatency] Analysis: {message}", LoggingTarget.Runtime, LogLevel.Verbose);

            notificationOverlay?.Post(new SimpleNotification
            {
                Text = message,
                Icon = FontAwesome.Solid.InfoCircle,
            });
        }

        public void Dispose()
        {
            if (scoreProcessor != null)
                scoreProcessor.NewJudgement -= OnNewJudgement;
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Represents a single latency measurement.
    /// </summary>
    public struct LatencyRecord
    {
        public double KeyPressTimeMs;
        public double JudgementTimeMs;
        public double AudioPlayTimeMs;

        public double InputLatencyMs => JudgementTimeMs - KeyPressTimeMs;
        public double AudioLatencyMs => AudioPlayTimeMs - JudgementTimeMs;
        public double SuggestedOffsetMs => AudioLatencyMs - InputLatencyMs;
    }
}
