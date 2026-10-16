// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Storyboards.Drawables
{
    /// <summary>
    /// Plays a <see cref="DetachedStoryboardSampleInfo"/>, keeping the balance of the note it came from.
    /// A hitsound it carries plays under the gameplay mixer that note played under.
    /// </summary>
    public partial class DrawableDetachedStoryboardSample : DrawableStoryboardSample
    {
        private readonly DetachedStoryboardSampleInfo info;

        public DrawableDetachedStoryboardSample(DetachedStoryboardSampleInfo sampleInfo)
            : base(sampleInfo)
        {
            info = sampleInfo;
            MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME;

            // Playback resolves the sample definition of the removed object rather than the storyboard wrapper around
            // it. A sample lookup reads the bank, suffix and beatmap-sample flags of the definition it is handed, so
            // resolving the wrapper silently resolves a different sound (typically the skin's default hit sound).
            Samples = new[] { sampleInfo.OriginalSample };
        }

        [Resolved(canBeNull: true)]
        private DrawableRuleset? drawableRuleset { get; set; }

        /// <summary>
        /// The mixer of the ruleset this was detached from, which carries the hitsound volume the note played under.
        /// </summary>
        /// <remarks>
        /// Falls back to the game-wide setting when there is no ruleset to attach to (e.g. a detached sample sitting
        /// on its own outside of gameplay).
        /// </remarks>
        protected override IAdjustableAudioComponent? HitsoundVolumeChain => drawableRuleset?.Audio;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Balance is resolved once, from the column the removed object occupied.
            Balance.Value = info.Balance;
        }
    }
}
