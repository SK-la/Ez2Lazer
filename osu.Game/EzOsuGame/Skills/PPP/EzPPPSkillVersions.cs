// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Per-ruleset PPP bundle revision (all chart/player skills for that mode share one version).
    /// Bump one constant to retire every <c>beatmap_ppp</c> / <c>player_ppp</c> row for that ruleset.
    /// Rows carry their own <c>RulesetOnlineId</c>, so the bundle is resolved from the row rather than from the skill id.
    /// </summary>
    public static class EzPPPSkillVersions
    {
        /// <summary>
        /// Revision 2 follows the corrected algorithm spec: raw-distance aim, the documented flow/pattern weights,
        /// rhythm complexity, and the four performance formulas.
        /// </summary>
        public const int OSU = 2;

        public const int TAIKO = 2;
        public const int CATCH = 2;
        public const int MANIA = 2;

        /// <summary>Max bundle revision across modes (data-status facet stamp).</summary>
        public const int FACET_REVISION = 2;

        public static int ForRuleset(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID)
                return OSU;

            if (rulesetOnlineId == EzLocalProfileConstants.TAIKO_RULESET_ID)
                return TAIKO;

            if (rulesetOnlineId == EzLocalProfileConstants.CATCH_RULESET_ID)
                return CATCH;

            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return MANIA;

            return 0;
        }
    }
}
