// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Storyboards.Drawables;

namespace osu.Game.Storyboards
{
    /// <summary>
    /// A storyboard sample bound to a hit object sample that a mod removed, so its audio keeps playing on the
    /// background timeline instead of being lost with the object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike a parsed storyboard sample, this resolves through the sample definition it was built from, keeping the
    /// bank, suffix and beatmap-sample flags intact so a keysound is looked up the same way the note looked it up.
    /// </para>
    /// <para>
    /// <see cref="IsHitsound"/> is decided by the beatmap hosting the samples rather than being fixed, so a ruleset
    /// can have the audio treated as a hitsound (resolved and played through the hitsound chain) or as plain
    /// background audio.
    /// </para>
    /// </remarks>
    public class DetachedStoryboardSampleInfo : StoryboardSampleInfo
    {
        /// <summary>
        /// The 0..1 balance this sample should be played at, keeping the placement of the object it came from.
        /// </summary>
        public readonly double Balance;

        private readonly ISampleInfo sample;
        private readonly bool isHitsound;

        public DetachedStoryboardSampleInfo(ISampleInfo sample, double time, double balance, bool isHitsound)
            : base(StoryboardElementSource.Beatmap, sample.LookupNames.FirstOrDefault() ?? string.Empty, time, sample.Volume)
        {
            this.sample = sample;
            this.isHitsound = isHitsound;
            Balance = balance;
        }

        /// <summary>
        /// The sample definition the removed object played, which is what playback resolves.
        /// </summary>
        internal ISampleInfo OriginalSample => sample;

        public override int Volume => sample.Volume;

        public override bool IsHitsound => isHitsound;

        public override IEnumerable<string> LookupNames => sample.LookupNames;

        public override Drawable CreateDrawable() => new DrawableDetachedStoryboardSample(this);
    }
}
