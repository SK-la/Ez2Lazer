// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Identity of one producer. The scope is part of the identity because the same name can carry
    /// unrelated algorithms on the two sides — a chart rating and a player rating share an axis
    /// vocabulary but never share a dependency edge or a stored row.
    /// </summary>
    public readonly record struct EzMetricProducerId(string Id, EzSkillScope Scope) : IComparable<EzMetricProducerId>
    {
        public int CompareTo(EzMetricProducerId other)
        {
            int byId = string.CompareOrdinal(Id, other.Id);
            return byId != 0 ? byId : Scope.CompareTo(other.Scope);
        }

        public override string ToString() => $"{Id}/{Scope}";
    }
}
