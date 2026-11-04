// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Per-ruleset PPP bundle revision (all chart/player skills for that mode share one version).
    /// Bump one constant to retire every <c>beatmap_ppplus</c> / <c>player_ppplus</c> row for that ruleset.
    /// <see cref="EzOsuSkillAlgorithm.VERSION"/> is not used as the PPP staleness stamp.
    /// </summary>
    public static class EzPppSkillVersions
    {
        public const int OSU = 1;
        public const int TAIKO = 1;
        public const int CATCH = 1;
        public const int MANIA = 1;

        /// <summary>Max bundle revision across modes (data-status facet stamp).</summary>
        public const int FACET_REVISION = 1;

        public static int ForRuleset(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID)
                return OSU;

            if (rulesetOnlineId == EzPppSkillCatalog.TAIKO_RULESET_ID)
                return TAIKO;

            if (rulesetOnlineId == EzPppSkillCatalog.CATCH_RULESET_ID)
                return CATCH;

            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return MANIA;

            return 0;
        }

        /// <summary>Expected row version for a PPP skill id (player/chart metric suffix).</summary>
        public static int ForSkillId(string skillId)
        {
            if (!EzPppSkillCatalog.TrySplit(skillId, out _, out string metric))
                return 0;

            return ForMetric(metric);
        }

        /// <summary>
        /// Bundle revision for a metric. Shared ids (<see cref="EzPppSkillCatalog.STAMINA"/>, <see cref="EzPppSkillCatalog.STAR_RATING"/>) invalidate when any owning mode bumps.
        /// </summary>
        public static int ForMetric(string metricId)
        {
            switch (metricId)
            {
                case EzPppSkillCatalog.RHYTHM:
                case EzPppSkillCatalog.COLOUR:
                    return TAIKO;

                case EzPppSkillCatalog.MOVEMENT:
                    return CATCH;

                case EzPppSkillCatalog.STRAIN:
                    return MANIA;

                case EzPppSkillCatalog.STAR_RATING:
                    return Math.Max(Math.Max(OSU, TAIKO), Math.Max(CATCH, MANIA));

                case EzPppSkillCatalog.STAMINA:
                    return Math.Max(OSU, TAIKO);

                case EzPppSkillCatalog.ACCURACY_DIFFICULTY:
                    return OSU;

                default:
                    if (EzOsuSkillAxisExtensions.TryParse(metricId, out _))
                        return OSU;

                    return 0;
            }
        }
    }
}
