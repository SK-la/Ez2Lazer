// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>LeoBlack patterns thin-pipeline output (hub <c>fromChart</c> / <c>analyzePatternFromText</c>).</summary>
    public sealed class EzLeoBlackPatternResult
    {
        public IReadOnlyList<EzLeoBlackCluster> TopFiveClusters { get; init; } = [];

        /// <summary>Headline category, e.g. <c>Jack</c>, <c>Stream</c>, <c>Tech</c>, <c>Trill</c>, <c>Handstream</c>.</summary>
        public string Category { get; init; } = "";

        /// <summary>Pruned cluster list used for categorise (Importance-sorted).</summary>
        public IReadOnlyList<EzLeoBlackCluster> Clusters { get; init; } = [];
    }

    /// <summary>One clustered pattern window pool (hub cluster object).</summary>
    public sealed class EzLeoBlackCluster
    {
        /// <summary>Core pattern name, e.g. <c>Jacks</c>, <c>Chordstream</c>, <c>Stream</c>.</summary>
        public string Pattern { get; init; } = "";

        public double Bpm { get; init; }

        public double Amount { get; init; }

        /// <summary><c>Amount * RatingMultiplier * BPM</c> as in hub clustering.</summary>
        public double Importance { get; init; }

        /// <summary>Optional display label (hub <c>format()</c>); may equal <see cref="Pattern"/>.</summary>
        public string Label { get; init; } = "";
    }
}
