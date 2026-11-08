// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;

namespace osu.Game.EzOsuGame.Beatmaps
{
    public static class EzBeatmapSRExtensions
    {
        public static bool SupportsSunnyStarRating(this BeatmapInfo beatmap) => EzSunnyStarRatingSupport.SupportsRuleset(beatmap.Ruleset);

        public static bool HasPersistedSunnyStarRating(this BeatmapInfo beatmap) => beatmap.SunnyStarRating >= 0;

        public static double? GetPersistedSunnyStarRating(this BeatmapInfo beatmap) => beatmap.HasPersistedSunnyStarRating() ? beatmap.SunnyStarRating : null;

        /// <summary>
        /// 面板在官方星级旁展示的 Ez 星级。
        /// </summary>
        /// <remarks>
        /// Which rating this is comes from the ruleset, not from the analysis system song select happens to be
        /// sorting by: changing the sort order must not change the number a chart shows for itself. osu has a
        /// single Ez rating (PP+), so it is always the one shown; mania has two, so its selected system decides
        /// between the Sunny rating and the Mina rating, and only the Sunny source can be changed by a mod.
        /// </remarks>
        public static double? GetEzStarRating(this BeatmapInfo beatmap) => resolveEzStarRating(beatmap, null, preferAnalysisValues: false);

        public static EzManiaSummary ToEzManiaSummaryForDisplay(this BeatmapInfo beatmap, EzManiaSummary? analysisSummary = null, bool preferAnalysisValues = false)
        {
            double? rating = resolveEzStarRating(beatmap, analysisSummary, preferAnalysisValues);

            if (!analysisSummary.HasValue)
                return new EzManiaSummary(null, null, rating);

            var summary = analysisSummary.Value;

            return new EzManiaSummary(
                summary.ColumnCounts,
                summary.HasHoldNoteCounts ? summary.HoldNoteCounts : null,
                rating);
        }

        private static double? resolveEzStarRating(BeatmapInfo beatmap, EzManiaSummary? analysisSummary, bool preferAnalysisValues)
        {
            int rulesetId = beatmap.Ruleset.OnlineID;

            if (rulesetId != EzLocalProfileConstants.MANIA_RULESET_ID)
                return finite(beatmap.PPPStarRating);

            if (EzMetricVariantReadout.Selected(rulesetId)?.Id != EzMetricVariantCatalog.SUNNY)
                return finite(beatmap.MinaRating);

            if (preferAnalysisValues && analysisSummary?.XxySr is double liveXxy)
                return finite(liveXxy);

            return finite(beatmap.GetPersistedSunnyStarRating() ?? analysisSummary?.XxySr);
        }

        private static double? finite(double? value) => value is double actual && double.IsFinite(actual) && actual >= 0 ? actual : null;
    }
}
