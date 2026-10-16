// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Storyboards.Drawables
{
    /// <summary>
    /// Plays a <see cref="DetachedStoryboardSampleInfo"/>, keeping the balance and volume chain of the note it came
    /// from.
    /// </summary>
    public partial class DrawableDetachedStoryboardSample : DrawableStoryboardSample
    {
        private readonly DetachedStoryboardSampleInfo info;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private Bindable<double>? hitsoundVolume;

        public DrawableDetachedStoryboardSample(DetachedStoryboardSampleInfo sampleInfo)
            : base(sampleInfo)
        {
            info = sampleInfo;
            MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Balance.Value = info.Balance;

            // Hitsound-classified audio follows the hitsound slider on top of the master and effect volumes, exactly
            // as a live note's own sample does. Background audio is left to those two.
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
