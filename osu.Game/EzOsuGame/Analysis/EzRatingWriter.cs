// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// The one write path for the star rating and pp each analysis system gives a chart.
    /// </summary>
    /// <remarks>
    /// Callers must pass an object attached to an open write transaction: a rating written onto a detached
    /// <see cref="BeatmapInfo"/> is lost, because the embedded data was cloned with the beatmap.
    /// The official pair is mirrored onto <see cref="BeatmapInfo.StarRating"/> / <see cref="BeatmapInfo.PerformancePoints"/>
    /// so upstream code keeps reading the fields it has always read.
    /// </remarks>
    public static class EzRatingWriter
    {
        /// <summary>Writes a system's star rating. A non-finite value is ignored.</summary>
        public static void WriteSR(BeatmapInfo beatmap, EzAnalysisSystem system, double sr)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (!double.IsFinite(sr))
                return;

            beatmap.EzRatingData.SetStar(system, sr);

            if (system == EzAnalysisSystem.Official)
                beatmap.StarRating = sr;
        }

        /// <summary>Writes a system's perfect-play pp. A non-finite value is ignored.</summary>
        public static void WritePP(BeatmapInfo beatmap, EzAnalysisSystem system, double pp)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (!double.IsFinite(pp))
                return;

            beatmap.EzRatingData.SetPerformance(system, pp);

            if (system == EzAnalysisSystem.Official)
                beatmap.PerformancePoints = pp;
        }

        /// <summary>Writes whichever halves are supplied. Null leaves the other half untouched.</summary>
        public static void Write(BeatmapInfo beatmap, EzAnalysisSystem system, double? sr = null, double? pp = null)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (sr is double star)
                WriteSR(beatmap, system, star);

            if (pp is double points)
                WritePP(beatmap, system, points);
        }
    }
}
