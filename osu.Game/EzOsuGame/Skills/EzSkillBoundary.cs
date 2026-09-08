// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Boundary notes (aligned with mania-hub / mania-tracker):
    /// <list type="bullet">
    /// <item><see cref="EzSkillSystems.BEATMAP_MSD"/> — chart difficulty axes from MinaCalc MSD. Not player skill.</item>
    /// <item><see cref="EzSkillSystems.PLAYER_SSR"/> — player skill axes from score-goal SSR + AggregateSSRs. Profile Skills radar source.</item>
    /// <item><see cref="EzSkillSystems.DAN"/> — independent dan. Chart: <see cref="EzChartDanEstimator"/> / <see cref="EzSkillProvider.TryGetChartDan"/>; player: credit clears → <see cref="EzDanEstimate"/>. LeoBlack deferred.</item>
    /// <item>xxy radar skill-ification is explicitly deferred.</item>
    /// </list>
    /// Reference clone: sibling repo <c>Ez2Lazer/mania-hub</c>.
    /// Ez Realm v8 (single bump) holds skill rows, dan estimate columns, and history points.
    /// </summary>
    public static class EzSkillBoundary
    {
    }
}
