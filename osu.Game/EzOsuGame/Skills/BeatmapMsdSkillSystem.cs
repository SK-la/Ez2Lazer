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
            var list = new List<EzSkillDefinition>(EzSkillIds.MINA_SKILLSETS.Length);

            foreach (string axis in EzSkillIds.MINA_SKILLSETS)
            {
                list.Add(new EzSkillDefinition(
                    EzSkillSystems.BEATMAP_MSD,
                    EzSkillIds.Msd(axis),
                    axisDisplay(axis),
                    EzSkillScope.Beatmap,
                    accent(axis)));
            }

            return list;
        }

        private static string axisDisplay(string axis) => axis switch
        {
            EzSkillIds.OVERALL => "Overall",
            EzSkillIds.STREAM => "Stream",
            EzSkillIds.JUMPSTREAM => "Jumpstream",
            EzSkillIds.HANDSTREAM => "Handstream",
            EzSkillIds.STAMINA => "Stamina",
            EzSkillIds.JACK_SPEED => "Jackspeed",
            EzSkillIds.CHORDJACK => "Chordjack",
            EzSkillIds.TECHNICAL => "Technical",
            _ => axis,
        };

        private static string accent(string axis) => axis switch
        {
            EzSkillIds.OVERALL => "#c9cfdd",
            EzSkillIds.STREAM => "#8f6bd8",
            EzSkillIds.JUMPSTREAM => "#6f87d8",
            EzSkillIds.HANDSTREAM => "#b06bc0",
            EzSkillIds.STAMINA => "#ad6b5d",
            EzSkillIds.JACK_SPEED => "#c66f84",
            EzSkillIds.CHORDJACK => "#c59a5c",
            EzSkillIds.TECHNICAL => "#83a86f",
            _ => "#8f6bd8",
        };
    }
}
