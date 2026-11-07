// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Declares one metric producer on an enum field, so identity, layer and wiring live in one place.
    /// </summary>
    /// <remarks>
    /// The producer's id is not written here: it is derived from the enum member name, so the member is the single
    /// authority. Behaviour that cannot be a constant (the revision probe, the axis set) is named by an enum member
    /// and turned into code by the resolver and the axis builder.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EzMetricProducerMetaAttribute : Attribute
    {
        public EzMetricProducerMetaAttribute(EzMetricTier tier, EzMetricUnit unit)
        {
            Tier = tier;
            Unit = unit;
        }

        public EzMetricTier Tier { get; }

        public EzMetricUnit Unit { get; }

        /// <summary>Storage namespace the producer writes under. Empty means "no rows of its own".</summary>
        public string SystemId { get; init; } = string.Empty;

        /// <summary>Axis family this producer's vector is comparable within.</summary>
        public EzMetricFamily Family { get; init; } = EzMetricFamily.None;

        /// <summary>Whether the producer stores one row per slice (keymode) rather than one per subject.</summary>
        public bool Sliced { get; init; }

        /// <summary>Which rulesets the producer applies to.</summary>
        public EzMetricRulesetScope Rulesets { get; init; } = EzMetricRulesetScope.All;

        /// <summary>Where the producer's axes come from.</summary>
        public EzMetricAxisSource Axes { get; init; } = EzMetricAxisSource.None;

        /// <summary>Where the producer's own revision comes from.</summary>
        public EzMetricRevisionSource Revision { get; init; } = EzMetricRevisionSource.Fixed;

        /// <summary>
        /// Whether this producer is the default radar source of its side.
        /// </summary>
        /// <remarks>
        /// Several producers can be candidates on the same ruleset (mania has both a rating engine and a PP+ vector),
        /// so the default is the highest <see cref="RadarPriority"/> among those that apply. A negative priority is
        /// not a radar candidate at all.
        /// </remarks>
        public bool DefaultRadar { get; init; }

        /// <summary>Priority among radar candidates; the highest applicable one wins.</summary>
        public int RadarPriority { get; init; }

        /// <summary>Chart producers whose stored rows this producer reads. Only these create a version edge.</summary>
        public EzChartMetricProducer[] ChartDependsOn { get; init; } = Array.Empty<EzChartMetricProducer>();

        /// <summary>Player producers whose stored rows this producer reads.</summary>
        public EzPlayerMetricProducer[] PlayerDependsOn { get; init; } = Array.Empty<EzPlayerMetricProducer>();
    }

    /// <summary>
    /// Axis families: producers in one family emit comparable axes, which is what makes a two-colour radar possible.
    /// </summary>
    public enum EzMetricFamily
    {
        /// <summary>Scalar-only producer with no axis vocabulary.</summary>
        None,

        Official,

        Sunny,

        Ppp,

        Mina,
    }

    /// <summary>Which rulesets a producer applies to.</summary>
    public enum EzMetricRulesetScope
    {
        /// <summary>Every ruleset.</summary>
        All,

        /// <summary>Mania only.</summary>
        ManiaOnly,

        /// <summary>Rulesets the PP+ engine rates (osu, taiko, catch, mania).</summary>
        PppSupported,
    }

    /// <summary>Where a producer's axes come from.</summary>
    public enum EzMetricAxisSource
    {
        /// <summary>Scalar producer: it stores a single value, not a vector.</summary>
        None,

        /// <summary>The PP+ skill catalog for the ruleset.</summary>
        PppCatalog,

        /// <summary>The MinaCalc skillset enum.</summary>
        MinaEngine,

        /// <summary>Chart structure: long-note frame, subtype scores and RC pattern tags.</summary>
        ChartStructure,
    }

    /// <summary>Where a producer's own revision comes from.</summary>
    public enum EzMetricRevisionSource
    {
        /// <summary>Constant, so the producer never retires itself.</summary>
        Fixed,

        UpstreamOfficialDifficulty,

        UpstreamSsr,

        /// <summary>Per-ruleset PP+ bundle revision.</summary>
        PppBundle,

        /// <summary>PP+ facet revision, shared by every ruleset's player rows.</summary>
        PppFacet,

        MinaSkill,

        ChartSkillInfo,

        DanAlgorithm,
    }
}
