// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Converts the Mina engine's rating into the star scale the Ez pill prints.
    /// </summary>
    /// <remarks>
    /// Only the star-facing value converts. The engine's axes, the dan chain and the stored
    /// <c>beatmap_msd</c> rows keep the engine's own scale.
    /// </remarks>
    public static class EzMinaRatingScale
    {
        /// <summary>Fixed divisor from the Mina rating to a star rating.</summary>
        public const double STAR_DIVISOR = 4.5;

        public static double ToStarRating(double rating) => rating / STAR_DIVISOR;
    }
}
