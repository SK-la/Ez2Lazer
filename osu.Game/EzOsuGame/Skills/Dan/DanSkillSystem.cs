// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Player-facing dan catalog: one entry per key count and side (RC / LN).
    /// Declares the directory only; dan values follow their own storage, not the skill value tables.
    /// </summary>
    public sealed class DanSkillSystem : IEzSkillSystem
    {
        /// <summary>Persisted wire id for the RC side.</summary>
        public const string SIDE_RC = "rc";

        /// <summary>Persisted wire id for the LN side.</summary>
        public const string SIDE_LN = "ln";

        private static readonly int[] tracked_key_counts = { 4, 5, 6, 7, 8, 9 };

        public string SystemId => EzSkillSystems.DAN;

        public EzSkillScope Scope => EzSkillScope.Player;

        public bool IsDefaultRadar => false;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = createSkills();

        public bool AppliesToRuleset(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

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
