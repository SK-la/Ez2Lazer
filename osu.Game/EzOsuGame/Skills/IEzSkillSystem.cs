// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Pluggable skill family (MSD chart axes, SSR player axes, Dan, …).
    /// Reserved: additional systems / keymode-specific axis catalogs register here via <see cref="EzSkillRegistry"/>.
    /// </summary>
    public interface IEzSkillSystem
    {
        string SystemId { get; }

        IReadOnlyList<EzSkillDefinition> Skills { get; }
    }
}
