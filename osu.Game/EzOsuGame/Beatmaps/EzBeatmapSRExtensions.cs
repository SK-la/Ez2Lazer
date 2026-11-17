// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SSR;

namespace osu.Game.EzOsuGame.Beatmaps
{
    public static class EzBeatmapSRExtensions
    {
        public static bool SupportsSunnyStarRating(this BeatmapInfo beatmap)
            => EzSSRUnit.SupportsRuleset(beatmap.Ruleset.OnlineID);

        public static bool HasPersistedSunnyStarRating(this BeatmapInfo beatmap) => beatmap.EzRatingData.SunnySR >= 0;

        public static double? GetPersistedSunnyStarRating(this BeatmapInfo beatmap)
            => beatmap.HasPersistedSunnyStarRating() ? beatmap.EzRatingData.SunnySR : null;

        /// <summary>
        /// Stored skill star for the ruleset's selected readout. Live values are read from the analysis cache.
        /// </summary>
        public static double? GetEzStarRating(this BeatmapInfo beatmap)
        {
            if (EzRulesetReadout.Selected(beatmap.Ruleset.OnlineID) is not { } readout || readout.SkillRating == EzAnalysisSystem.None)
                return null;

            return finite(EzRulesetReadout.StoredStar(beatmap, readout.SkillRating));
        }

        /// <summary>
        /// A per-system rating is only a value above zero: the columns are schema-populated and a zero means the
        /// backfill has not reached this chart, not that it was rated zero.
        /// </summary>
        private static double? finite(double? value)
            => value is double actual && double.IsFinite(actual) && actual > 0 ? actual : null;
    }
}
