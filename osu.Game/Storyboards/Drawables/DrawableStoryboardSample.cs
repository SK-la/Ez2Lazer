// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Storyboards.Drawables
{
    public partial class DrawableStoryboardSample : PausableSkinnableSound
    {
        /// <summary>
        /// The amount of time allowable beyond the start time of the sample, for the sample to start.
        /// </summary>
        private const double allowable_late_start = 100;

        private readonly StoryboardSampleInfo sampleInfo;

        public override bool RemoveWhenNotAlive => false;

        public DrawableStoryboardSample(StoryboardSampleInfo sampleInfo)
            : base(sampleInfo)
        {
            this.sampleInfo = sampleInfo;
            LifetimeStart = sampleInfo.StartTime;
        }

        [Resolved]
        private IReadOnlyList<Mod>? mods { get; set; }

        private Bindable<double>? hitsoundVolume;

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager? config)
        {
            // A sample classified as a hitsound resolves through the hitsound sample store, which deliberately omits
            // the global sample adjustments the regular store applies. The hitsound volume chain therefore has to be
            // applied here instead, or the audio would end up outside of volume control rather than under it.
            if (sampleInfo.IsHitsound && config != null)
                Volume.BindTo(hitsoundVolume = config.GetBindable<double>(OsuSetting.HitsoundVolume));
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);

            if (mods != null)
            {
                foreach (var mod in mods.OfType<IApplicableToSample>())
                {
                    foreach (var sample in DrawableSamples)
                        mod.ApplyToSample(sample);
                }
            }
        }

        protected override void SamplePlaybackDisabledChanged(ValueChangedEvent<bool> disabled)
        {
            if (!RequestedPlaying) return;

            if (!Looping && disabled.NewValue)
            {
                // the default behaviour for sample disabling is to allow one-shot samples to play out.
                // storyboards regularly have long running samples that can cause this behaviour to lead to unintended results.
                // for this reason, we immediately stop such samples.
                Stop();
            }

            base.SamplePlaybackDisabledChanged(disabled);
        }

        protected override void Update()
        {
            base.Update();

            // Check if we've yet to pass the sample start time.
            if (Time.Current < sampleInfo.StartTime)
            {
                Stop();

                // Playback has stopped, but if the user fast-forwards to a point after the start time of the sample then
                // we must not have a lifetime end in order to continue receiving updates and start the sample below.
                LifetimeStart = sampleInfo.StartTime;
                LifetimeEnd = double.MaxValue;

                return;
            }

            // Ensure that we've elapsed from a point before the sample's start time before playing.
            if (Time.Current - Time.Elapsed <= sampleInfo.StartTime)
            {
                // We've passed the start time of the sample. We only play the sample if we're within an allowable range
                // from the sample's start, to reduce layering if we've been fast-forwarded far into the future
                if (!RequestedPlaying && Time.Current - sampleInfo.StartTime < allowable_late_start)
                    Play();
            }

            // Playback has started, but if the user rewinds to a point before the start time of the sample then
            // we must not have a lifetime start in order to continue receiving updates and stop the sample above.
            LifetimeStart = double.MinValue;
            LifetimeEnd = sampleInfo.StartTime;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && hitsoundVolume != null)
                Volume.UnbindFrom(hitsoundVolume);

            hitsoundVolume = null;

            base.Dispose(isDisposing);
        }
    }
}
