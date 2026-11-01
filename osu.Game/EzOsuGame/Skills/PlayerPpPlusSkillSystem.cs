// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// osu!standard player-side PP+-shaped axes (aggregated across local scores).
    /// Slice key in Realm is <see cref="EzSkillSystems.OSU_SLICE_KEY"/> (no keymode).
    /// </summary>
    public sealed class PlayerPpPlusSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.PLAYER_PPPLUS;

        public EzSkillScope Scope => EzSkillScope.Player;

        public bool IsDefaultRadar => true;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = createSkills();

        public bool AppliesToRuleset(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID;

        private static IReadOnlyList<EzSkillDefinition> createSkills()
        {
            var list = new List<EzSkillDefinition>(EzOsuSkillAxisExtensions.PlayerAxes.Length);

            foreach (var axis in EzOsuSkillAxisExtensions.PlayerAxes)
            {
                var chip = axis.Chip();
                list.Add(new EzSkillDefinition(
                    EzSkillSystems.PLAYER_PPPLUS,
                    axis.ToPlayerSkillId(),
                    chip.Name,
                    EzSkillScope.Player,
                    chip.AccentHex));
            }

            return list;
        }
    }
}
