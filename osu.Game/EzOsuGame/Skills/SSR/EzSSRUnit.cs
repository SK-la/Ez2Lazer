// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// The SSR system's role in <c>osu.Game</c>, currently carrying only its revision.
    /// </summary>
    /// <remarks>
    /// The rating math is still supplied by the ruleset assembly (<see cref="Analysis.IEzSunnyStarRatingSupport"/>),
    /// so this type deliberately implements nothing but the number. It exists so the system has a home here and the
    /// stored rows have one authority to be stamped against, instead of the version being reachable only through a
    /// late-bound provider.
    /// <para>
    /// The rest of the SSR role - chart input, the rating itself, player aggregation - moves here later; that split
    /// does not change who owns the number.
    /// </para>
    /// </remarks>
    public static class EzSSRUnit
    {
        /// <summary>
        /// Revision stamped on the SSR rows.
        /// </summary>
        /// <remarks>
        /// Bump when the rating algorithm changes, wherever that algorithm currently lives. A ruleset that reports a
        /// different number than this is a wiring bug, not a second revision.
        /// </remarks>
        public const int VERSION = 20250415;
    }
}
