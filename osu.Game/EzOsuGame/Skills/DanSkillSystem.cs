// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Independent dan estimate system. Chart path: <see cref="EzChartDanEstimator"/>; player path: <see cref="EzPlayerDanAggregator"/>.
    /// Must not be mixed into MSD/SSR aggregation.
    /// </summary>
    public sealed class DanSkillSystem : IEzSkillSystem
    {
        public const string SIDE_RC = "rc";
        public const string SIDE_LN = "ln";

        private static readonly int[] tracked_key_counts = { 4, 5, 6, 7, 8, 9 };

        public string SystemId => EzSkillSystems.DAN;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = createSkills();

        private static IReadOnlyList<EzSkillDefinition> createSkills()
        {
            var list = new List<EzSkillDefinition>();

            foreach (int keys in tracked_key_counts)
            {
                list.Add(new EzSkillDefinition(
                    EzSkillSystems.DAN,
                    EzSkillIds.Dan(keys, SIDE_RC),
                    $"{keys}K Regular dan",
                    EzSkillScope.Player,
                    "#ec6a9c"));

                list.Add(new EzSkillDefinition(
                    EzSkillSystems.DAN,
                    EzSkillIds.Dan(keys, SIDE_LN),
                    $"{keys}K LN dan",
                    EzSkillScope.Player,
                    "#f07474"));
            }

            return list;
        }
    }
}
