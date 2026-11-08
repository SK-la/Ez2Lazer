// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Analysis facets whose produced rows are stamped with a revision number and read back
    /// through it. Declaring the dependency edges here (rather than only in docs) is what makes
    /// "bump one version -> exactly its consumers recompute" auditable.
    /// </summary>
    /// <remarks>
    /// Dependency edges of the Ez chart skill chain:
    /// <list type="bullet">
    /// <item><see cref="Msd"/> depends on nothing.</item>
    /// <item><see cref="ChartSkillInfo"/> depends on nothing (pure chart structure; it no longer reads MSD).</item>
    /// <item><see cref="ChartDan"/> depends on <see cref="ChartSkillInfo"/> and <see cref="Msd"/>
    /// (hold ratio falls back to CSI LnRatio; skillset stamps read CSI; MSD supplies the rating axes).</item>
    /// </list>
    /// Rule: bumping an upstream facet invalidates every downstream facet; bumping a downstream
    /// facet invalidates only itself.
    /// </remarks>
    [Flags]
    public enum EzAnalysisFacet
    {
        None = 0,

        Msd = 1 << 0,

        ChartSkillInfo = 1 << 1,

        ChartDan = 1 << 2,

        /// <summary>Player SSR vectors / pattern ratings (Local Profile). Not part of the chart chain.</summary>
        SsrAggregation = 1 << 3,

        /// <summary>Player dan clears / dan estimate rows.</summary>
        DanAlgorithm = 1 << 4,

        /// <summary>Local Profile partition payloads.</summary>
        Aggregation = 1 << 5,

        /// <summary>PPP chart skills. Not part of the mania MSD chain.</summary>
        PPP = 1 << 6,
    }

    /// <summary>
    /// Facet-scoped revision numbers for the Ez chart skill chain.
    /// <para>
    /// The numbers come from <see cref="EzMetricProducerRegistry"/>, which folds every declared data
    /// dependency of a producer. A producer that reads no other producer's rows keeps its own probe,
    /// so installing the registry does not move numbers that are already on disk.
    /// </para>
    /// <para>
    /// A persisted row stores the revision of the moment it was produced, so a plain equality read is
    /// a staleness test: bumping any input makes old rows read as a miss and the normal incremental
    /// backfill recomputes them. No schema change, no bulk clearing, and "stale" stays observable
    /// (it is just "row revision != current revision").
    /// </para>
    /// <para>
    /// This intentionally deviates from the star / xxysr shape (one applied scalar per ruleset that is
    /// compared and then reset to -1). Those two are siblings sharing one
    /// <c>LastAppliedDifficultyVersion</c>; the chart chain has real fan-in into Dan, which a single
    /// scalar cannot express without also invalidating a sibling on an unrelated bump.
    /// </para>
    /// </summary>
    public static class EzAnalysisRevision
    {
        private const int chain_ruleset = EzLocalProfileConstants.MANIA_RULESET_ID;

        /// <summary>
        /// Installs the registry that supplies the revisions. Call once during startup, before any row
        /// is stamped; the pre-install default has no upstream revisions, which keeps unit tests free of
        /// a ruleset database.
        /// </summary>
        public static void Configure(EzMetricProducerRegistry producerRegistry)
        {
            ArgumentNullException.ThrowIfNull(producerRegistry);
            Registry = producerRegistry;
        }

        public static EzMetricProducerRegistry Registry { get; private set; } = EzMetricProducers.CreateDefault(EzNullUpstreamRevisions.INSTANCE);

        /// <summary>Revision stamped on the mania Mina axis rows.</summary>
        public static int Msd => Registry.RevisionOf(EzMetricProducers.MINA_CHART, chain_ruleset);

        /// <summary>Revision stamped on <c>EzBeatmapChartSkillInfo.InfoVersion</c>.</summary>
        public static int ChartSkillInfo => Registry.RevisionOf(EzMetricProducers.CSI_CHART, chain_ruleset);

        /// <summary>Revision stamped on <c>EzBeatmapChartDan.AlgorithmVersion</c>.</summary>
        public static int ChartDan => Registry.RevisionOf(EzMetricProducers.DAN_CHART, chain_ruleset);

        /// <summary>Current revision for a single chart-chain facet. Returns 0 for non-chart facets.</summary>
        public static int For(EzAnalysisFacet facet)
        {
            switch (facet)
            {
                case EzAnalysisFacet.Msd:
                    return Msd;

                case EzAnalysisFacet.ChartSkillInfo:
                    return ChartSkillInfo;

                case EzAnalysisFacet.ChartDan:
                    return ChartDan;

                default:
                    return 0;
            }
        }

        /// <summary>Facets that must be recomputed when <paramref name="facet"/> is bumped (excluding itself).</summary>
        public static EzAnalysisFacet DownstreamOf(EzAnalysisFacet facet)
        {
            switch (facet)
            {
                case EzAnalysisFacet.Msd:
                    return EzAnalysisFacet.ChartDan;

                case EzAnalysisFacet.ChartSkillInfo:
                    return EzAnalysisFacet.ChartDan;

                default:
                    return EzAnalysisFacet.None;
            }
        }

        /// <summary>Human-readable breakdown of the CSI revision inputs.</summary>
        public static string DescribeChartSkillInfo() => Registry.DescribeRevision(EzMetricProducers.CSI_CHART, chain_ruleset);

        /// <summary>Human-readable breakdown of the dan revision inputs.</summary>
        public static string DescribeChartDan() => Registry.DescribeRevision(EzMetricProducers.DAN_CHART, chain_ruleset);
    }
}
