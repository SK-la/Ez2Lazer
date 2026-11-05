// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Analysis
{
    public static class EzSunnyStarRatingSupport
    {
        /// <summary>
        /// Whether the ruleset assembly is currently loadable (built-in or third-party extension).
        /// When a ruleset is removed or unavailable, persisted Realm metadata is left intact until it becomes available again.
        /// </summary>
        public static bool IsRulesetAvailable(IRulesetInfo? rulesetInfo)
            => rulesetInfo is RulesetInfo { Available: true };

        /// <summary>
        /// Whether the ruleset exposes xxy SR calculation (via <see cref="IEzSunnyStarRatingSupport"/>).
        /// </summary>
        public static bool SupportsRuleset(IRulesetInfo? rulesetInfo)
            => IsRulesetAvailable(rulesetInfo) && tryGetSupport(rulesetInfo!, out _);

        /// <summary>
        /// Whether baseline xxy SR can be calculated for this beatmap under the given ruleset.
        /// </summary>
        public static bool SupportsBeatmap(IBeatmap beatmap, IRulesetInfo rulesetInfo)
            => IsRulesetAvailable(rulesetInfo) && tryGetSupport(rulesetInfo, out var support) && support.SupportsSunnyStarRating(beatmap);

        /// <summary>
        /// yyyyMMdd xxy SR algorithm version for the ruleset, if it implements <see cref="IEzSunnyStarRatingSupport"/>.
        /// </summary>
        public static bool TryGetSunnyStarRatingVersion(IRulesetInfo rulesetInfo, out int version)
        {
            version = 0;

            if (!tryGetSupport(rulesetInfo, out IEzSunnyStarRatingSupport support))
                return false;

            version = support.SunnyStarRatingVersion;
            return version > 0;
        }

        private static bool tryGetSupport(IRulesetInfo rulesetInfo, out IEzSunnyStarRatingSupport support)
        {
            support = null!;

            if (!EzAnalysisProviderBridge.TryCreateProvider(rulesetInfo, out IEzAnalysisProvider provider))
                return false;

            if (provider is not IEzSunnyStarRatingSupport xxySupport)
                return false;

            support = xxySupport;
            return true;
        }
    }
}
