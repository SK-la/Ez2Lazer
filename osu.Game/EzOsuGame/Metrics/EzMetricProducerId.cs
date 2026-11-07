// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Identity of one producer within its side: the chart catalog or the player catalog.
    /// </summary>
    /// <remarks>
    /// The two sides are separate namespaces, so an id only has to be unique within one of them. Player ids
    /// carry a suffix because a player producer declares a dependency on a chart producer and the two must not
    /// be confused for each other.
    /// </remarks>
    public readonly record struct EzMetricProducerId(string Id) : IComparable<EzMetricProducerId>
    {
        public int CompareTo(EzMetricProducerId other) => string.CompareOrdinal(Id, other.Id);

        public override string ToString() => Id;
    }
}
