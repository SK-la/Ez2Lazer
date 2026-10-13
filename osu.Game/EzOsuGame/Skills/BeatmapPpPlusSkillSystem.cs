// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// osu!standard chart-side PP+-shaped axes (Jump / Flow / Precision / Speed / Stamina / Accuracy + Aim Total).
    /// </summary>
    public sealed class BeatmapPpPlusSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.BEATMAP_PPPLUS;

        public EzSkillScope Scope => EzSkillScope.Beatmap;

        public bool IsDefaultRadar => true;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = createSkills();

        public bool AppliesToRuleset(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID;

        private static IReadOnlyList<EzSkillDefinition> createSkills()
        {
            var list = new List<EzSkillDefinition>(EzOsuSkillAxisExtensions.ChartAxes.Length);

            foreach (var axis in EzOsuSkillAxisExtensions.ChartAxes)
            {
                var chip = axis.Chip();
                list.Add(new EzSkillDefinition(
                    EzSkillSystems.BEATMAP_PPPLUS,
                    axis.ToChartSkillId(),
                    chip.Name,
                    EzSkillScope.Beatmap,
                    chip.AccentHex));
            }

            return list;
        }
    }
}
