// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    ///     Chart analysis fields used by hub-aligned dan skillset filing
    ///     (mirror of mania-hub <c>ChartSkillInfo</c> filing subset).
    ///     Persisted as typed Realm <see cref="EzBeatmapChartSkillInfo"/> (EZ≥9).
    /// </summary>
    public sealed class EzChartSkillInfo
    {
        public const int VERSION = 1;

        public string[] Patterns { get; init; } = [];

        /// <summary>Structurally detected 4K quadstream/minijack/jack-marathon demand.</summary>
        public bool JackDemand { get; init; }

        public double? JackShare { get; init; }

        public double? StreamShare { get; init; }

        public bool? TechCategory { get; init; }

        public bool? ClusterTrill { get; init; }

        public bool? HandstreamCluster { get; init; }

        public bool HandstreamEndurance { get; init; }

        public double TechScore { get; init; }

        public double ChordjackScore { get; init; }

        /// <summary>Pattern analyzer jack score; null when unset.</summary>
        public double? JackScore { get; init; }

        public EzMotionFeatures? Motion { get; init; }

        public double? LnRatio { get; init; }

        public bool Vibro { get; init; }

        /// <summary>False when object structure makes dan evidence unsafe.</summary>
        public bool DanEligible { get; init; } = true;

        public double? LengthSeconds { get; init; }

        public int? KeyCount { get; init; }
    }
}
