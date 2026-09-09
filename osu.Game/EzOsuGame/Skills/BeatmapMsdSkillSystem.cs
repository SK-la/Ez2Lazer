// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Chart-side MinaCalc MSD axes. Each axis is an independent skill (not one blob).
    /// </summary>
    public sealed class BeatmapMsdSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.BEATMAP_MSD;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = createSkills();

        private static IReadOnlyList<EzSkillDefinition> createSkills()
        {
            var list = new List<EzSkillDefinition>(EzMinaSkillAxisExtensions.All.Length);

            foreach (var axis in EzMinaSkillAxisExtensions.All)
            {
                var chip = axis.Chip();
                list.Add(new EzSkillDefinition(
                    EzSkillSystems.BEATMAP_MSD,
                    axis.ToMsdSkillId(),
                    chip.Name,
                    EzSkillScope.Beatmap,
                    chip.AccentHex));
            }

            return list;
        }
    }
}
