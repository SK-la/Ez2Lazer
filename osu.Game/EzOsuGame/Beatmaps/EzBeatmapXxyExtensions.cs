// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Beatmaps
{
    public static class EzBeatmapXxyExtensions
    {
        public static bool SupportsSunnyStarRating(this BeatmapInfo beatmap)
            => EzSunnyStarRatingSupport.SupportsBeatmapInfo(beatmap);

        public static bool HasPersistedSunnyStarRating(this BeatmapInfo beatmap) => beatmap.SunnyStarRating >= 0;

        public static double? GetPersistedSunnyStarRating(this BeatmapInfo beatmap)
            => beatmap.HasPersistedSunnyStarRating() ? beatmap.SunnyStarRating : null;

        public static EzManiaSummary ToEzManiaSummaryForDisplay(this BeatmapInfo beatmap, EzManiaSummary? analysisSummary = null, bool preferAnalysisValues = false)
        {
            double? xxy = preferAnalysisValues && analysisSummary?.XxySr is double analysisXxy
                ? analysisXxy
                : beatmap.GetPersistedSunnyStarRating() ?? analysisSummary?.XxySr;

            if (!analysisSummary.HasValue)
                return new EzManiaSummary(null, null, xxy);

            var summary = analysisSummary.Value;

            return new EzManiaSummary(
                summary.ColumnCounts,
                summary.HasHoldNoteCounts ? summary.HoldNoteCounts : null,
                xxy);
        }
    }
}
