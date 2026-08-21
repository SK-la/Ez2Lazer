// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Stage-space motion script keyed from <see cref="EzPetPackDefinition.Motions"/>.
    /// Moves <c>petBox</c>; does not bake walk cycles into frame pixels.
    /// </summary>
    public class EzPetMotionDefinition
    {
        /// <summary>
        /// <c>wander</c>, <c>moveTo</c>, or <c>teleportTo</c>.
        /// </summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// Normalised units per second for <c>wander</c> (distance across the layer).
        /// </summary>
        public float Speed { get; set; } = 0.04f;

        /// <summary>
        /// Normalised wander bounds <c>[x0, y0, x1, y1]</c>. Defaults to a padded screen rect when null/short.
        /// </summary>
        public float[]? Bounds { get; set; }

        /// <summary>
        /// Named anchor for <c>moveTo</c> / <c>teleportTo</c>, e.g. <c>results.rank</c>.
        /// </summary>
        public string? Anchor { get; set; }

        /// <summary>
        /// Explicit normalised target <c>[x, y]</c> when <see cref="Anchor"/> is unused.
        /// </summary>
        public float[]? Target { get; set; }

        public double DurationMs { get; set; } = 400;

        /// <summary>
        /// Optional easing name understood by the motion driver (e.g. <c>OutQuad</c>).
        /// </summary>
        public string? Easing { get; set; }
    }
}
