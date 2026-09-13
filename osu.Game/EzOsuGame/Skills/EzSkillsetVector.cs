// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        /// <summary>
        /// Number of <c>f32</c> values the MinaCalc n-key engine writes, in
        /// <see cref="RawOutputOrder"/> order.
        /// </summary>
        public const int RawLength = 8;

        /// <summary>
        /// Fixed engine output order. Do <b>not</b> reorder — it mirrors MinaCalc's
        /// <c>minacalc_compute</c> out-pointer layout.
        /// </summary>
        public static readonly EzMinaSkillAxis[] RawOutputOrder =
        [
            EzMinaSkillAxis.Overall,
            EzMinaSkillAxis.Stream,
            EzMinaSkillAxis.Jumpstream,
            EzMinaSkillAxis.Handstream,
            EzMinaSkillAxis.Stamina,
            EzMinaSkillAxis.JackSpeed,
            EzMinaSkillAxis.Chordjack,
            EzMinaSkillAxis.Technical,
        ];

        /// <summary>
        /// Builds a vector from the engine's raw eight-value output
        /// (<see cref="RawOutputOrder"/> order). Shorter spans yield the zero vector.
        /// </summary>
        public static EzSkillsetVector FromRaw(ReadOnlySpan<float> raw)
        {
            if (raw.Length < RawLength)
                return default;

            return new EzSkillsetVector(raw[0], raw[1], raw[2], raw[3], raw[4], raw[5], raw[6], raw[7]);
        }

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
