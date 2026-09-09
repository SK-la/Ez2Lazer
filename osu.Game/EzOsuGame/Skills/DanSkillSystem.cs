// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player-facing dan skill catalog (Profile Track). Registers RC/LN tracks for keys 4–9 as directory entries.
    /// Chart path: <see cref="EzChartDanEstimator"/>; player clears: <see cref="EzPlayerDanAggregator"/>;
    /// side GetDan: <see cref="EzSkillProvider"/> headline fold after skillset refresh.
    /// Must not mix into MSD/SSR aggregation; storage remains <see cref="EzDanEstimate"/>.
    /// Reserved: keep multi-key entries even when some keys still fall back on Reform labels —
    /// custom ladders attach via <see cref="Dan.EzDanLadders"/>, not by shrinking this catalog.
    /// </summary>
    public sealed class DanSkillSystem : IEzSkillSystem
    {
        /// <summary>Persisted wire id; prefer <see cref="EzDanSide"/> in new code.</summary>
        public const string SIDE_RC = "rc";

        /// <summary>Persisted wire id; prefer <see cref="EzDanSide"/> in new code.</summary>
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
                    EzSkillSystems.DanSkillId(keys, EzDanSide.Rc),
                    EzDanSide.Rc.TrackDisplayName(keys),
                    EzSkillScope.Player,
                    "#ec6a9c"));

                list.Add(new EzSkillDefinition(
                    EzSkillSystems.DAN,
                    EzSkillSystems.DanSkillId(keys, EzDanSide.Ln),
                    EzDanSide.Ln.TrackDisplayName(keys),
                    EzSkillScope.Player,
                    "#f07474"));
            }

            return list;
        }
    }
}
