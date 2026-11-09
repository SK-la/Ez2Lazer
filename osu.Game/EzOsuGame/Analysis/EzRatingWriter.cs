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

        /// <summary>
        /// Retires both halves of one system's rating.
        /// </summary>
        /// <remarks>
        /// One writer so the halves cannot expire apart: a stale star with a fresh pp (or the reverse) is what a
        /// reader cannot tell from a valid pair, and the official pair is derived from one calculation, so it is only
        /// ever both or neither.
        /// </remarks>
        public static void Clear(BeatmapInfo beatmap, EzAnalysisSystem system)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            WriteSR(beatmap, system, -1);
            WritePP(beatmap, system, -1);
        }

        /// <summary>
        /// Whether automatic official backfill still owes this chart a difficulty calculation.
        /// </summary>
        /// <remarks>
        /// <c>-1</c> is missing; <c>0</c> is the settled-failure stamp (same role as
        /// <c>ScoreInfo.BackgroundReprocessingFailed</c>). A manual force rebuild clears back to <c>-1</c>.
        /// </remarks>
        public static bool NeedsOfficialBackfill(BeatmapInfo beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            return beatmap.BeatmapSet != null && beatmap.Ruleset.Available && beatmap.StarRating < 0;
        }

        /// <summary>
        /// Marks a chart as tried-and-failed so automatic backfill skips it until a manual rebuild clears the stamp.
        /// </summary>
        /// <remarks>
        /// Writes <c>0</c> (not <c>-1</c>): the pass selects on <c>&lt; 0</c>, so zero is settled. Official covers both
        /// halves; Sunny only needs the star column to leave the missing set.
        /// </remarks>
        public static void MarkProcessingFailed(BeatmapInfo beatmap, EzAnalysisSystem system)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            switch (system)
            {
                case EzAnalysisSystem.Official:
                    Write(beatmap, system, 0, 0);
                    break;

                case EzAnalysisSystem.SSR:
                    WriteSR(beatmap, system, 0);
                    break;
            }
        }

        /// <summary>
        /// Which multi-system baseline columns are still owed, given the stored values and an optional Mina overall axis.
        /// </summary>
        /// <remarks>
        /// Sunny pp is only owed when a Sunny star is already stored; Mina star is only owed when an overall axis is.
        /// A chart with neither source is not owed - that is what stops the pass from scanning the whole library every
        /// launch for columns it can never fill.
        /// </remarks>
        public static (bool NeedSunnyPp, bool NeedMinaSr) MultiSystemBaselineDebt(
            double sunnySr, double sunnyPp, double minaSr, double? minaOverall)
        {
            bool needSunnyPp = sunnyPp < 0 && sunnySr > 0;
            bool needMinaSr = minaSr < 0 && minaOverall is double overall && overall > 0;
            return (needSunnyPp, needMinaSr);
        }
    }
}
