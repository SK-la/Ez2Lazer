// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
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

        [Resolved(canBeNull: true)]
        private OsuConfigManager? config { get; set; }

        [Resolved(canBeNull: true)]
        private AudioManager? audioManager { get; set; }

        [Resolved(canBeNull: true)]
        private IBindable<WorkingBeatmap>? beatmap { get; set; }

        [Resolved(canBeNull: true)]
        private DrawableRuleset? drawableRuleset { get; set; }

        private Bindable<double>? boundVolume;
        private IAdjustableAudioComponent? hitsoundChain;

        /// <summary>
        /// Whether this sample plays under the hitsound volume the notes in this beatmap play under.
        /// </summary>
        /// <remarks>
        /// In rulesets whose notes are themselves audio (mania, BMS) a storyboard sample sits on the same timeline the
        /// notes do, which is the only signal available to tell it apart from a storyboard that merely carries
        /// background sound on these rulesets: both arrive as plain <see cref="StoryboardSampleInfo"/>.
        /// </remarks>
        protected virtual bool CountsAsNoteAudio => StoryboardSampleInfoExtensions.IsNoteAudioIn(drawableRuleset?.Ruleset.RulesetInfo);

        /// <summary>
        /// Whether this sample is the beatmap's background music, played from the storyboard because the beatmap has
        /// no audio track of its own.
        /// </summary>
        /// <remarks>
        /// Background music is not note audio, so it must not follow the hitsound volume even on the rulesets whose
        /// storyboard samples otherwise do.
        /// </remarks>
        protected virtual bool CarriesBackgroundMusic => beatmap != null && StoryboardSampleInfoExtensions.CarriesBackgroundMusicIn(beatmap.Value);

        /// <summary>
        /// The audio component whose volume chain a hitsound-classified sample plays under, or <see langword="null"/>
        /// to take the chain from the game-wide hitsound volume setting.
        /// </summary>
        /// <remarks>
        /// A sample classified as a hitsound resolves through the hitsound sample store, which deliberately omits the
        /// global sample adjustments the regular store applies, so its chain has to be applied from outside the store.
        /// A sample hosted by gameplay instead plays under the mixer gameplay audio already goes through, which
        /// carries the same setting along with adjustments a ruleset or mod applies to gameplay audio.
        /// </remarks>
        protected virtual IAdjustableAudioComponent? HitsoundVolumeChain => null;

        protected override void LoadAsyncComplete()
        {
            bool noteAudio = CountsAsNoteAudio && !CarriesBackgroundMusic;
            bool backgroundMusic = CarriesBackgroundMusic && !sampleInfo.IsHitsound;

            // Note audio and storyboard-carried background music both resolve through the hitsound store: that store
            // carries neither the game-wide effect volume nor the music volume, so each of them only needs its own
            // slider applied here. The regular store already carries the music volume, and binding it on top of that
            // would apply it twice.
            if ((noteAudio || backgroundMusic) && !sampleInfo.IsHitsound)
                Samples = new ISampleInfo[] { new HitsoundClassifiedSampleInfo(sampleInfo) };

            base.LoadAsyncComplete();

            if (noteAudio || sampleInfo.IsHitsound)
            {
                if (HitsoundVolumeChain is { } chain)
                    BindAdjustments(hitsoundChain = chain);
                else if (config != null)
                    Volume.BindTo(boundVolume = config.GetBindable<double>(OsuSetting.HitsoundVolume));
            }
            else if (backgroundMusic && audioManager != null)
            {
                Volume.BindTo(boundVolume = audioManager.VolumeTrack);
            }
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
            if (isDisposing)
            {
                if (boundVolume != null)
                    Volume.UnbindFrom(boundVolume);

                if (hitsoundChain != null)
                    UnbindAdjustments(hitsoundChain);
            }

            boundVolume = null;
            hitsoundChain = null;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// A copy of a storyboard sample marked as a hitsound, so playback resolves through the hitsound sample store.
        /// </summary>
        /// <remarks>
        /// Deriving from <see cref="StoryboardSampleInfo"/> keeps the lookup itself untouched: beatmap-skin sample
        /// gating and fallback rules key on that type, so a wrapper of any other type would silently disable them.
        /// </remarks>
        private sealed class HitsoundClassifiedSampleInfo : StoryboardSampleInfo
        {
            private readonly StoryboardSampleInfo inner;

            public HitsoundClassifiedSampleInfo(StoryboardSampleInfo inner)
                : base(inner.Source, inner.Path, inner.StartTime, inner.Volume)
            {
                this.inner = inner;
            }

            public override bool IsHitsound => true;

            public override int Volume => inner.Volume;

            public override IEnumerable<string> LookupNames => inner.LookupNames;
        }
    }
}
