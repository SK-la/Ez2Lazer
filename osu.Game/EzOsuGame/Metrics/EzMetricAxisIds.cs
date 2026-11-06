// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Axis ids shared by every producer of the same kind.
    /// </summary>
    /// <remarks>
    /// These are storage keys, so they are declared once here and referenced by the skill vocabularies that
    /// emit them. A second literal with the same text would let a row be written under one key and read under
    /// another without any compile error.
    /// </remarks>
    public static class EzMetricAxisIds
    {
        /// <summary>
        /// Star rating of whichever system produced it. Official, Sunny and PP+ all write this same id, so a
        /// reader can compare them without knowing the system; the producer identity says which one it was.
        /// </summary>
        public const string STAR_RATING = "star_rating";

        /// <summary>Performance points of whichever system produced them.</summary>
        public const string PERFORMANCE_POINTS = "performance_points";
    }
}
