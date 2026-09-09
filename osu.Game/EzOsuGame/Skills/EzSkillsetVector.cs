// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using MinaCalc;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One MinaCalc skillset vector (MSD or SSR). Axes follow <see cref="EzMinaSkillAxis"/>.
    /// </summary>
    public readonly record struct EzSkillsetVector(
        double Overall,
        double Stream,
        double Jumpstream,
        double Handstream,
        double Stamina,
        double JackSpeed,
        double Chordjack,
        double Technical)
    {
        public static EzSkillsetVector FromMina(MinaCalcScores scores)
            => new EzSkillsetVector(scores.Overall, scores.Stream, scores.Jumpstream, scores.Handstream, scores.Stamina, scores.JackSpeed, scores.Chordjack, scores.Technical);

        public double Get(EzMinaSkillAxis axis) => axis switch
        {
            EzMinaSkillAxis.Overall => Overall,
            EzMinaSkillAxis.Stream => Stream,
            EzMinaSkillAxis.Jumpstream => Jumpstream,
            EzMinaSkillAxis.Handstream => Handstream,
            EzMinaSkillAxis.Stamina => Stamina,
            EzMinaSkillAxis.JackSpeed => JackSpeed,
            EzMinaSkillAxis.Chordjack => Chordjack,
            EzMinaSkillAxis.Technical => Technical,
            _ => 0,
        };

        public IEnumerable<(EzMinaSkillAxis Axis, double Value)> Enumerate()
        {
            foreach (var axis in EzMinaSkillAxisExtensions.All)
                yield return (axis, Get(axis));
        }
    }
}
