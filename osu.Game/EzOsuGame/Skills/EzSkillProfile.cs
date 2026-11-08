// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Which skill namespaces a ruleset has, and which one it reads by default.
    /// </summary>
    /// <remarks>
    /// Two rulesets actually have a choice (mania reads Mina, every PP+ ruleset reads PP+), so this is a table of
    /// two cases rather than a declaration per value.
    /// </remarks>
    public sealed class EzSkillProfile
    {
        private EzSkillProfile(int rulesetOnlineId, IReadOnlyList<string> systemIds, string? defaultSystemId)
        {
            RulesetOnlineId = rulesetOnlineId;
            SystemIds = systemIds;
            DefaultSystemId = defaultSystemId;
        }

        public int RulesetOnlineId { get; }

        public IReadOnlyList<string> SystemIds { get; }

        public string? DefaultSystemId { get; }

        public bool HasSkills => SystemIds.Count > 0;

        public bool ContainsSystem(string systemId) => SystemIds.Contains(systemId, StringComparer.Ordinal);

        /// <summary>Chart-side skill namespaces, or null when the ruleset has none.</summary>
        public static EzSkillProfile? ForChart(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return new EzSkillProfile(rulesetOnlineId, [EzSkillSystems.BEATMAP_MINA, EzSkillSystems.BEATMAP_PPP], EzSkillSystems.BEATMAP_MINA);

            if (EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId))
                return new EzSkillProfile(rulesetOnlineId, [EzSkillSystems.BEATMAP_PPP], EzSkillSystems.BEATMAP_PPP);

            return null;
        }

        /// <summary>Player-side skill namespaces, or null when the ruleset has none.</summary>
        public static EzSkillProfile? ForPlayer(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return new EzSkillProfile(rulesetOnlineId, [EzSkillSystems.PLAYER_MINA, EzSkillSystems.PLAYER_PPP], EzSkillSystems.PLAYER_MINA);

            if (EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId))
                return new EzSkillProfile(rulesetOnlineId, [EzSkillSystems.PLAYER_PPP], EzSkillSystems.PLAYER_PPP);

            return null;
        }
    }
}
