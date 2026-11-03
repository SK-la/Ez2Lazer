// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Audio;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A sample which was carried by a hit object that a mod has removed or rewritten, but which must still be
    /// played at its original time.
    /// </summary>
    /// <remarks>
    /// The originating <see cref="ISampleInfo"/> is kept intact rather than converted to a storyboard sample, so
    /// volume routing, hitsound classification, the beatmap hitsounds toggle and ruleset-specific sample lookups
    /// (e.g. file-backed keysounds) behave exactly as they did while the hit object was alive.
    /// </remarks>
    public class DetachedNoteSample
    {
        /// <summary>
        /// The position of an object that has no horizontal placement, i.e. centred.
        /// </summary>
        public const float CENTRE = 0.5f;

        /// <summary>
        /// The time at which the sample should play.
        /// </summary>
        public readonly double Time;

        /// <summary>
        /// The sample to play.
        /// </summary>
        public readonly ISampleInfo Sample;

        /// <summary>
        /// Playback position on the playfield, as a 0..1 range (0 for far-left, 0.5 for centre, 1 for far-right).
        /// </summary>
        public readonly float Position;

        public DetachedNoteSample(double time, ISampleInfo sample, float position)
        {
            Time = time;
            Sample = sample;
            Position = position;
        }
    }
}
