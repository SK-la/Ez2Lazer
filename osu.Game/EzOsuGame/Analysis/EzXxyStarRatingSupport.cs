// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Analysis
{
    public static class EzXxyStarRatingSupport
    {
        /// <summary>
        /// Whether the ruleset exposes xxy SR calculation (via <see cref="IEzXxyStarRatingSupport"/>).
        /// </summary>
        public static bool SupportsRuleset(IRulesetInfo? rulesetInfo)
            => rulesetInfo != null && tryGetSupport(rulesetInfo, out _);

        /// <summary>
        /// Whether baseline xxy SR can be calculated for this beatmap under the given ruleset.
        /// </summary>
        public static bool SupportsBeatmap(IBeatmap beatmap, IRulesetInfo rulesetInfo)
            => tryGetSupport(rulesetInfo, out var support) && support.SupportsXxyStarRating(beatmap);

        public static bool SupportsBeatmapInfo(BeatmapInfo beatmapInfo)
            => SupportsRuleset(beatmapInfo.Ruleset);

        private static bool tryGetSupport(IRulesetInfo rulesetInfo, out IEzXxyStarRatingSupport support)
        {
            support = null!;

            if (!EzAnalysisProviderBridge.TryCreateProvider(rulesetInfo, out IEzAnalysisProvider provider))
                return false;

            if (provider is not IEzXxyStarRatingSupport xxySupport)
                return false;

            support = xxySupport;
            return true;
        }
    }
}
