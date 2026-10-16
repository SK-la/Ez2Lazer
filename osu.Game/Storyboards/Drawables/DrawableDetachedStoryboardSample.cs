// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Storyboards.Drawables
{
    /// <summary>
    /// Plays a <see cref="DetachedStoryboardSampleInfo"/>, keeping the balance of the note it came from.
    /// The volume chain is inherited from <see cref="DrawableStoryboardSample"/>.
    /// </summary>
    public partial class DrawableDetachedStoryboardSample : DrawableStoryboardSample
    {
        private readonly DetachedStoryboardSampleInfo info;

        public DrawableDetachedStoryboardSample(DetachedStoryboardSampleInfo sampleInfo)
            : base(sampleInfo)
        {
            info = sampleInfo;
            MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Balance is resolved once, from the column the removed object occupied.
            Balance.Value = info.Balance;
        }
    }
}
