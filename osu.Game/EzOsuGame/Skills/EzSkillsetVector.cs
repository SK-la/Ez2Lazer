// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

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
        /// <summary>
        /// Number of <c>f32</c> values the MinaCalc n-key engine writes, in
        /// <see cref="RAW_OUTPUT_ORDER"/> order.
        /// </summary>
        public const int RAW_LENGTH = 8;

        /// <summary>
        /// Fixed engine output order. Do <b>not</b> reorder — it mirrors MinaCalc's
        /// <c>minacalc_compute</c> out-pointer layout.
        /// </summary>
        public static readonly EzMinaSkillAxis[] RAW_OUTPUT_ORDER =
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
        /// (<see cref="RAW_OUTPUT_ORDER"/> order). Shorter spans yield the zero vector.
        /// </summary>
        public static EzSkillsetVector FromRaw(ReadOnlySpan<float> raw)
        {
            if (raw.Length < RAW_LENGTH)
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
