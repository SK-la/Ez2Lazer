// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// Plays the samples that hit objects removed by a mod would have played, at their original time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Belongs under the ruleset's gameplay mixer so playback follows the same volume chain as live notes
    /// (hitsound volume, pause, intro skip, Mute mod). Samples resolve through the skin source rather than being
    /// converted to storyboard samples, so hitsound classification, the beatmap hitsounds toggle and ruleset
    /// specific lookups (e.g. file-backed keysounds) are unchanged.
    /// </para>
    /// <para>
    /// As with live notes, one channel per sample name is kept: re-triggering a name interrupts the previous one.
    /// </para>
    /// </remarks>
    public partial class DetachedSamplePlayer : Drawable
    {
        /// <summary>
        /// Samples further behind the current time than this are considered skipped (seek, intro skip) and not played.
        /// </summary>
        private const double allowable_late_start = 100;

        private readonly DetachedNoteSample[] samples;
        private readonly IAdjustableAudioComponent mixer;

        [Resolved(CanBeNull = true)]
        private ISkinSource? skin { get; set; }

        [Resolved(canBeNull: true)]
        private ISamplePlaybackDisabler? samplePlaybackDisabler { get; set; }

        [Resolved]
        private IGameplaySettings gameplaySettings { get; set; } = null!;

        /// <summary>
        /// Sample description to resolution result, including unresolvable ones so lookups are not repeated.
        /// </summary>
        private readonly Dictionary<ISampleInfo, ISample?> resolvedSamples = new Dictionary<ISampleInfo, ISample?>();

        /// <summary>
        /// Sample name to currently playing channel; a re-trigger replaces (and stops) the previous one.
        /// </summary>
        private readonly Dictionary<string, SampleChannel> channels = new Dictionary<string, SampleChannel>();

        /// <summary>
        /// Fallback keys for samples without a lookup name, so they never interrupt each other.
        /// </summary>
        private readonly Dictionary<ISampleInfo, string> fallbackKeys = new Dictionary<ISampleInfo, string>();

        private int fallbackKeyCounter;
        private int nextIndex;
        private double lastTime = double.MinValue;
        private ScheduledDelegate? pendingSourceChange;

        public DetachedSamplePlayer(IEnumerable<DetachedNoteSample> samples, IAdjustableAudioComponent mixer)
        {
            this.mixer = mixer;
            this.samples = samples.OrderBy(s => s.Time).ToArray();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (skin != null)
                skin.SourceChanged += onSourceChanged;
        }

        protected override void LoadAsyncComplete()
        {
            // Resolve up front on the load thread: a keysound chart can reference thousands of samples, and doing
            // the skin lookups on the first update frame would stutter.
            base.LoadAsyncComplete();

            foreach (var sample in samples)
                resolve(sample.Sample);
        }

        protected override void Update()
        {
            base.Update();

            double currentTime = Time.Current;

            // Seeking backwards re-arms everything that is still ahead of the new position.
            if (currentTime < lastTime)
                nextIndex = 0;

            lastTime = currentTime;

            // Paused, skipping intro or catching up: no new sound.
            if (samplePlaybackDisabler?.SamplePlaybackDisabled.Value == true)
                return;

            while (nextIndex < samples.Length && samples[nextIndex].Time <= currentTime)
            {
                var entry = samples[nextIndex++];

                // Skipped over by a seek rather than reached by playback; layering all of them would be worse.
                if (currentTime - entry.Time > allowable_late_start)
                    continue;

                play(entry);
            }
        }

        private void play(DetachedNoteSample entry)
        {
            var sample = resolve(entry.Sample);

            if (sample == null)
                return;

            string key = keyFor(entry.Sample);

            if (channels.Remove(key, out var previous))
                disposeChannel(previous);

            var channel = sample.GetChannel();

            channel.Volume.Value = Math.Max(entry.Sample.Volume, DrawableHitObject.MINIMUM_SAMPLE_VOLUME) / 100.0;
            channel.Balance.Value = DrawableHitObject.CalculateSamplePlaybackBalance(entry.Position, gameplaySettings.PositionalHitsoundsLevel.Value);
            channel.BindAdjustments(mixer);

            // Mirrors SkinnableSound: skip the channel allocation when the whole volume chain is silent (e.g. Mute).
            if (channel.AggregateVolume.Value <= 0)
            {
                disposeChannel(channel);
                return;
            }

            channels[key] = channel;
            channel.Play();
        }

        private ISample? resolve(ISampleInfo sampleInfo)
        {
            if (resolvedSamples.TryGetValue(sampleInfo, out var cached))
                return cached;

            resolvedSamples.Add(sampleInfo, cached = skin?.GetSample(sampleInfo));
            return cached;
        }

        private string keyFor(ISampleInfo sampleInfo)
        {
            // The first lookup name is the audio name the skin actually resolves; identical names share a channel.
            string? name = sampleInfo.LookupNames.FirstOrDefault();

            if (!string.IsNullOrEmpty(name))
                return name;

            if (!fallbackKeys.TryGetValue(sampleInfo, out string? fallbackKey))
                fallbackKeys.Add(sampleInfo, fallbackKey = $"\0{fallbackKeyCounter++}");

            return fallbackKey;
        }

        private void onSourceChanged()
        {
            // Mirrors SkinReloadableDrawable: defer, as the change may arrive from a disposed hierarchy.
            pendingSourceChange?.Cancel();
            pendingSourceChange = Scheduler.Add(resetSamples);
        }

        private void resetSamples()
        {
            pendingSourceChange = null;

            foreach (var channel in channels.Values)
                disposeChannel(channel);

            channels.Clear();
            fallbackKeys.Clear();
            resolvedSamples.Clear();

            foreach (var sample in samples)
                resolve(sample.Sample);
        }

        private static void disposeChannel(SampleChannel? channel)
        {
            if (channel == null || channel.IsDisposed)
                return;

            try
            {
                // Actual release happens on the audio thread; calling this repeatedly is safe.
                channel.Dispose();
            }
            catch
            {
                // Racing with the audio thread's own release is harmless.
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // A cancelled load (e.g. leaving the player loader) never ran the dependency loader,
                // so the subscription may not have been made.
                if (skin != null)
                    skin.SourceChanged -= onSourceChanged;
                pendingSourceChange?.Cancel();
                pendingSourceChange = null;

                foreach (var channel in channels.Values)
                    disposeChannel(channel);

                channels.Clear();
                fallbackKeys.Clear();
                resolvedSamples.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
