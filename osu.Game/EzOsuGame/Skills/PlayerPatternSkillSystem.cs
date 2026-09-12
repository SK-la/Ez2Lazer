// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Hub <c>PATTERN_RATING_META</c> catalog for Skills UI (6/7/8K pattern axes).
    /// </summary>
    public sealed class PlayerPatternSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.PLAYER_PATTERN;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } =
            EzPlayerPatternAxisExtensions.All
                                         .Select(static axis =>
                                         {
                                             var meta = axis.Meta();
                                             return new EzSkillDefinition(
                                                 EzSkillSystems.PLAYER_PATTERN,
                                                 axis.ToSkillId(),
                                                 meta.DisplayName,
                                                 EzSkillScope.Player,
                                                 meta.AccentHex);
                                         })
                                         .ToList();
    }
}
