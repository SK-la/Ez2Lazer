// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Player-side PPP skills. Mania slices use key count; other rulesets use <see cref="EzSkillSystems.UNSLICED_KEY"/>.
    /// Mania keeps SSR as the default radar.
    /// </summary>
    public sealed class PlayerPPPSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.PLAYER_PPP;

        public EzSkillScope Scope => EzSkillScope.Player;

        public bool IsDefaultRadar => false;

        public bool IsDefaultRadarFor(int rulesetOnlineId)
            => AppliesToRuleset(rulesetOnlineId) && rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = EzPPPSkillCatalog.CreateDefinitions(EzSkillScope.Player);

        public bool AppliesToRuleset(int rulesetOnlineId)
            => EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId);
    }
}
