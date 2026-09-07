// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using MinaCalc;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One MinaCalc skillset vector (MSD or SSR). Each axis maps to an independent skill id.
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
        public static EzSkillsetVector FromMina(MinaCalcScores scores) => new EzSkillsetVector(scores.Overall, scores.Stream, scores.Jumpstream, scores.Handstream, scores.Stamina, scores.JackSpeed,
            scores.Chordjack, scores.Technical);

        public double Get(string skillId) => skillId switch
        {
            EzSkillIds.OVERALL => Overall,
            EzSkillIds.STREAM => Stream,
            EzSkillIds.JUMPSTREAM => Jumpstream,
            EzSkillIds.HANDSTREAM => Handstream,
            EzSkillIds.STAMINA => Stamina,
            EzSkillIds.JACK_SPEED => JackSpeed,
            EzSkillIds.CHORDJACK => Chordjack,
            EzSkillIds.TECHNICAL => Technical,
            _ => 0,
        };

        public IEnumerable<(string AxisId, double Value)> Enumerate()
        {
            foreach (string id in EzSkillIds.MINA_SKILLSETS)
                yield return (id, Get(id));
        }
    }
}
