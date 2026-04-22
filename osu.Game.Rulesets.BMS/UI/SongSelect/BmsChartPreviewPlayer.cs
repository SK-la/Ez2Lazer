// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// BMS-native chart preview player, written specifically for the BMS song-select screen.
    ///
    /// Design rationale: BMS has no separate "BGM track" — everything (notes and background) is just
    /// a sample placed on a channel at a specific time. This player therefore treats hit-object
    /// samples and <see cref="BmsBackgroundSoundEvent"/>s as a single ordered timeline of samples,
    /// exactly the way the BMS standard intends, and schedules each sample independently via
    /// <see cref="Scheduler.AddDelayed(Action, double, bool)"/>. That avoids the silent-miss problem of
    /// <c>EzPreviewTrackManager</c>'s coarse 16ms / 5ms-tolerance update loop on dense BMS keysound
    /// charts, where most note samples used to fall outside the trigger window.
    ///
    /// Samples are resolved via the supplied <see cref="BMSWorkingBeatmap"/>'s skin (which already
    /// owns a folder-mounted <see cref="ISampleStore"/>), so external BMS folders work without
    /// touching osu.Game's sandboxed storage.
    /// </summary>
    public partial class BmsChartPreviewPlayer : CompositeDrawable
    {
        /// <summary>
        /// Globally enable or disable this preview player. Mirrors <c>EzPreviewTrackManager.EnabledBindable</c>.
        /// </summary>
        public BindableBool EnabledBindable { get; } = new BindableBool();

        /// <summary>
        /// Override the preview start time (ms). When null, the chart's metadata <c>PreviewTime</c> is used,
        /// or 0 if that's missing.
        /// </summary>
        public double? OverridePreviewStartTime { get; set; }

        /// <summary>
        /// When true, the preview restarts at <see cref="OverridePreviewStartTime"/> after every event has
        /// fired (or after <see cref="LoopAfter"/> has elapsed since start). True by default to mirror
        /// osu.Game's looping song-select preview UX.
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// Maximum runtime per loop (ms). When the preview reaches this time it loops back to start.
        /// Defaults to 30 seconds, like osu.Game's preview.
        /// </summary>
        public double LoopAfter { get; set; } = 30_000;

        /// <summary>
        /// Cap one-shot scheduler tasks per loop so dense keysound charts do not flood the framework scheduler.
        /// </summary>
        private const int max_scheduled_samples_per_loop = 200;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        // currentBeatmap is the beatmap whose skin we use to resolve samples.
        private BMSWorkingBeatmap? currentBeatmap;
        private double previewStartTime;
        private readonly List<TimedSample> timeline = new List<TimedSample>();
        private readonly List<ScheduledDelegate> scheduledTriggers = new List<ScheduledDelegate>();
        private readonly List<SampleChannel> activeChannels = new List<SampleChannel>();
        private ScheduledDelegate? loopRestartDelegate;

        private bool playing;

        /// <summary>
        /// Start a fresh preview for <paramref name="beatmap"/>. Stops any in-flight preview first.
        /// Returns true if the preview successfully started, false if disabled or no events were collected.
        /// </summary>
        public bool StartPreview(BMSWorkingBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            StopPreview();

            if (!EnabledBindable.Value)
                return false;

            if (BmsAnalyticsScanService.IsRunning)
                return false;

            currentBeatmap = beatmap;

            // Build timeline from hit-object samples (keysounds) + BMS background channels.
            collectTimeline(beatmap);

            if (timeline.Count == 0)
            {
                Logger.Log("[BMS] BmsChartPreviewPlayer: no preview events for chart, falling back to silent.", LoggingTarget.Runtime, LogLevel.Debug);
                return false;
            }

            previewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;
            if (previewStartTime < 0)
                previewStartTime = 0;

            scheduleAllSamples();
            playing = true;
            return true;
        }

        public void StopPreview()
        {
            playing = false;

            foreach (var d in scheduledTriggers)
                d.Cancel();

            scheduledTriggers.Clear();

            loopRestartDelegate?.Cancel();
            loopRestartDelegate = null;

            stopActiveChannels();

            timeline.Clear();
            currentBeatmap = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            StopPreview();
            base.Dispose(isDisposing);
        }

        private void collectTimeline(BMSWorkingBeatmap beatmap)
        {
            timeline.Clear();

            // 1) Background channels — exposed directly on BMSBeatmap.
            if (beatmap.Beatmap is BMSBeatmap bmsBeatmap)
            {
                foreach (BmsBackgroundSoundEvent evt in bmsBeatmap.BackgroundSoundEvents)
                {
                    if (string.IsNullOrEmpty(evt.Filename))
                        continue;

                    timeline.Add(new TimedSample(evt.Time, evt.Filename, isBackground: true));
                }
            }

            // 2) Note key-sounds — pull file lookups from each hit object's FileHitSampleInfo.
            //    We use the playable beatmap (post-conversion) to align with what gameplay would actually play.
            try
            {
                IBeatmap playable = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);
                addHitObjectSamples(playable.HitObjects);
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] BmsChartPreviewPlayer: failed to read playable hit objects for keysound preview: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            timeline.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private void addHitObjectSamples(IEnumerable<HitObject> hitObjects)
        {
            foreach (HitObject ho in hitObjects)
            {
                foreach (var sample in ho.Samples)
                {
                    if (sample is ConvertHitObjectParser.FileHitSampleInfo fileSample && !string.IsNullOrEmpty(fileSample.Filename))
                        timeline.Add(new TimedSample(ho.StartTime, fileSample.Filename, isBackground: false));
                }

                if (ho.NestedHitObjects.Count > 0)
                    addHitObjectSamples(ho.NestedHitObjects);
            }
        }

        private void scheduleAllSamples()
        {
            if (currentBeatmap == null)
                return;

            loopRestartDelegate?.Cancel();
            loopRestartDelegate = null;

            double horizonTime = previewStartTime + LoopAfter;
            int scheduled = 0;

            for (int i = 0; i < timeline.Count; i++)
            {
                TimedSample evt = timeline[i];

                if (evt.Time < previewStartTime - 1)
                    continue;

                if (evt.Time > horizonTime)
                    break;

                if (scheduled >= max_scheduled_samples_per_loop)
                    break;

                double delay = Math.Max(0, evt.Time - previewStartTime);
                var captured = evt;

                scheduledTriggers.Add(Scheduler.AddDelayed(() => triggerSample(captured), delay));
                scheduled++;
            }

            if (Loop)
                loopRestartDelegate = Scheduler.AddDelayed(restartLoop, LoopAfter);
        }

        private void triggerSample(TimedSample evt)
        {
            if (!playing || currentBeatmap == null)
                return;

            try
            {
                ISample? sample = currentBeatmap.Skin.GetSample(new FilenameSampleInfo(evt.Filename));

                if (sample == null)
                    return;

                SampleChannel channel = sample.GetChannel();
                channel.Play();
                activeChannels.Add(channel);

                pruneStoppedChannels();
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] BmsChartPreviewPlayer: failed to play sample '{evt.Filename}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        private void pruneStoppedChannels()
        {
            for (int i = activeChannels.Count - 1; i >= 0; i--)
            {
                SampleChannel ch = activeChannels[i];

                if (ch.Playing)
                    continue;

                if (!ch.IsDisposed && !ch.ManualFree)
                {
                    try
                    {
                        ch.Dispose();
                    }
                    catch
                    {
                        // ignored — disposal race with audio thread is harmless here.
                    }
                }

                activeChannels.RemoveAt(i);
            }
        }

        private void stopActiveChannels()
        {
            foreach (SampleChannel ch in activeChannels)
            {
                try
                {
                    ch.Stop();

                    if (!ch.IsDisposed && !ch.ManualFree)
                        ch.Dispose();
                }
                catch
                {
                    // ignored — see pruneStoppedChannels.
                }
            }

            activeChannels.Clear();
        }

        private void restartLoop()
        {
            if (!playing || currentBeatmap == null)
                return;

            BMSWorkingBeatmap b = currentBeatmap;

            // Cancel the current set of scheduled triggers and active channels, then restart.
            foreach (var d in scheduledTriggers)
                d.Cancel();

            scheduledTriggers.Clear();
            stopActiveChannels();

            scheduleAllSamples();
            // scheduleAllSamples re-arms loopRestartDelegate.
            _ = b;
        }

        private readonly struct TimedSample
        {
            public TimedSample(double time, string filename, bool isBackground)
            {
                Time = time;
                Filename = filename;
                IsBackground = isBackground;
            }

            public double Time { get; }
            public string Filename { get; }

            // Currently informational only — consumers may want to weight or mute one of the streams in future.
            public bool IsBackground { get; }
        }

        /// <summary>
        /// Minimal <see cref="ISampleInfo"/> implementation that just exposes a single filename so that
        /// <see cref="osu.Game.Skinning.ISkin.GetSample(ISampleInfo)"/> can look up a BMS folder asset
        /// by its declared <c>#WAVxx</c> name.
        /// </summary>
        private sealed class FilenameSampleInfo : ISampleInfo
        {
            private readonly string filename;

            public FilenameSampleInfo(string filename)
            {
                this.filename = filename;
            }

            public IEnumerable<string> LookupNames => new[] { filename };
            public int Volume => 100;
        }
    }
}
