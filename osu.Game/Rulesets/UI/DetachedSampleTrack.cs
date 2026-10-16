// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// The background track that plays samples of hit objects a mod removed, at their original time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The elements are storyboard samples, so playback follows the storyboard sample rules (arming, rewinding, not
    /// sounding when the time was skipped over) without depending on the storyboard being displayed, which gameplay
    /// skips entirely when the storyboard is turned off.
    /// </para>
    /// <para>
    /// Whether an element counts as a hitsound is decided by <see cref="IDetachedSampleHost.DetachedSampleIsHitsound"/>,
    /// which is what selects the lookup and the volume chain it plays under.
    /// </para>
    /// </remarks>
    public partial class DetachedSampleTrack : CompositeDrawable
    {
        private readonly DetachedNoteSample[] samples;
        private readonly Func<ISampleInfo, bool> isHitsound;

        [Resolved]
        private IGameplaySettings gameplaySettings { get; set; } = null!;

        public DetachedSampleTrack(IEnumerable<DetachedNoteSample> samples, Func<ISampleInfo, bool> isHitsound)
        {
            this.samples = samples.ToArray();
            this.isHitsound = isHitsound;

            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Balance is resolved once, exactly as a live note resolves it, because the object that was removed is the
            // only thing that knew where the audio sat in the playfield.
            double positionalHitsoundsLevel = gameplaySettings.PositionalHitsoundsLevel.Value;

            foreach (var sample in samples)
            {
                var info = new DetachedStoryboardSampleInfo(
                    sample.Sample,
                    sample.Time,
                    DrawableHitObject.CalculateSamplePlaybackBalance(sample.Position, positionalHitsoundsLevel),
                    isHitsound(sample.Sample));

                AddInternal(new DrawableDetachedStoryboardSample(info));
            }
        }

        private partial class DrawableDetachedStoryboardSample : DrawableStoryboardSample
        {
            private readonly DetachedStoryboardSampleInfo info;

            [Resolved]
            private OsuConfigManager config { get; set; } = null!;

            public DrawableDetachedStoryboardSample(DetachedStoryboardSampleInfo sampleInfo)
                : base(sampleInfo)
            {
                info = sampleInfo;
                MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME;
            }

            private Bindable<double>? hitsoundVolume;

            [BackgroundDependencyLoader]
            private void load()
            {
                Balance.Value = info.Balance;

                // Hitsound-classified audio follows the hitsound volume slider, the same as a note's own sample does.
                if (info.IsHitsound)
                    Volume.BindTo(hitsoundVolume = config.GetBindable<double>(OsuSetting.HitsoundVolume));
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
}
