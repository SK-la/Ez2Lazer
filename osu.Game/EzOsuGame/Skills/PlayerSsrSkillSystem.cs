// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player-side SSR skill axes (aggregated from score-goal MinaCalc runs).
    /// Independent from beatmap MSD — same axis names, different system namespace.
    /// </summary>
    public sealed class PlayerSsrSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.PLAYER_SSR;

        public EzSkillScope Scope => EzSkillScope.Player;

        public bool IsDefaultRadar => true;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = createSkills();

        public bool AppliesToRuleset(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        private static IReadOnlyList<EzSkillDefinition> createSkills()
        {
            var list = new List<EzSkillDefinition>(EzMinaSkillAxisExtensions.All.Length);

            foreach (var axis in EzMinaSkillAxisExtensions.All)
            {
                var chip = axis.Chip();
                list.Add(new EzSkillDefinition(
                    EzSkillSystems.PLAYER_SSR,
                    axis.ToSsrSkillId(),
                    chip.Name,
                    EzSkillScope.Player,
                    chip.AccentHex));
            }

            return list;
        }
    }
}
