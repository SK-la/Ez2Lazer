// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Pluggable skill family (chart axes, player aggregates, dan catalogs, …).
    /// Register via <see cref="EzSkillRegistry"/>; ruleset wiring goes through <see cref="EzSkillProfile"/>.
    /// </summary>
    public interface IEzSkillSystem
    {
        string SystemId { get; }

        /// <summary>Beatmap vs player catalog — drives <see cref="EzSkillProfile"/> grouping.</summary>
        EzSkillScope Scope { get; }

        /// <summary>
        /// When true, this system is the default radar polygon for its <see cref="Scope"/>
        /// on every ruleset it applies to (at most one default per scope per ruleset).
        /// </summary>
        bool IsDefaultRadar { get; }

        IReadOnlyList<EzSkillDefinition> Skills { get; }

        bool AppliesToRuleset(int rulesetOnlineId);
    }
}
