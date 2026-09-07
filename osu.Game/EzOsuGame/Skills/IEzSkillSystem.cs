// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Pluggable skill family (MSD chart axes, SSR player axes, Dan, later xxy, …).
    /// </summary>
    public interface IEzSkillSystem
    {
        string SystemId { get; }

        IReadOnlyList<EzSkillDefinition> Skills { get; }
    }
}
