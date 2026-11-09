// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Which analysis systems a ruleset has a vector for, and which one it reads by default.
    /// </summary>
    /// <remarks>
    /// Two rulesets actually have a choice (mania reads Mina, every PP+ ruleset reads PPP), so this is a table of
    /// two cases rather than a declaration per system.
    /// </remarks>
    public sealed class EzSkillProfile
    {
        private EzSkillProfile(int rulesetOnlineId, IReadOnlyList<EzAnalysisSystem> systems, EzAnalysisSystem defaultSystem)
        {
            RulesetOnlineId = rulesetOnlineId;
            Systems = systems;
            DefaultSystem = defaultSystem;
        }

        public int RulesetOnlineId { get; }

        public IReadOnlyList<EzAnalysisSystem> Systems { get; }

        public EzAnalysisSystem DefaultSystem { get; }

        public bool HasSkills => Systems.Count > 0;

        public bool Contains(EzAnalysisSystem system) => Systems.Contains(system);
        /// <summary>Chart-side systems, or null when the ruleset has none.</summary>
        public static EzSkillProfile? ForChart(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return new EzSkillProfile(rulesetOnlineId, [EzAnalysisSystem.Mina, EzAnalysisSystem.PPP], EzAnalysisSystem.Mina);

            if (EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId))
                return new EzSkillProfile(rulesetOnlineId, [EzAnalysisSystem.PPP], EzAnalysisSystem.PPP);

            return null;
        }

        /// <summary>Player-side systems, or null when the ruleset has none.</summary>
        public static EzSkillProfile? ForPlayer(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return new EzSkillProfile(rulesetOnlineId, [EzAnalysisSystem.SSR, EzAnalysisSystem.PPP], EzAnalysisSystem.SSR);

            if (EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId))
                return new EzSkillProfile(rulesetOnlineId, [EzAnalysisSystem.PPP], EzAnalysisSystem.PPP);

            return null;
        }
    }
}
