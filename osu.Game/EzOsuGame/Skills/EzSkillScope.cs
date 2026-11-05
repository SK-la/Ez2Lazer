// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Which side of the analysis a metric describes: the chart itself, or a player's play of it.
    /// </summary>
    public enum EzSkillScope
    {
        Beatmap = 0,
        Player = 1,
    }
}
