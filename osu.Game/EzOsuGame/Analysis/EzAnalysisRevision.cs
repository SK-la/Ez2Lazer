// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills;

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
    /// <item><see cref="ChartSkillInfo"/> depends on <see cref="Msd"/> (HandstreamEndurance reads stored axes).</item>
    /// <item><see cref="ChartDan"/> depends on <see cref="ChartSkillInfo"/> and <see cref="Msd"/>
    /// (hold ratio falls back to CSI LnRatio; skillset stamps read CSI).</item>
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
    }

    /// <summary>
    /// Facet-scoped revision numbers for the Ez chart skill chain (MSD -> ChartSkillInfo -> ChartDan).
    /// <para>
    /// Each revision encodes the facet's own version constant plus every upstream constant it reads,
    /// into base-100 slots. A persisted row stores the revision of the moment it was produced, so a
    /// plain equality read is a staleness test: bumping any input makes old rows read as a miss and
    /// the normal incremental backfill recomputes them. No schema change, no bulk clearing, and
    /// "stale" stays observable (it is just "row revision != current revision").
    /// </para>
    /// <para>
    /// This intentionally deviates from the star / xxysr shape (one applied scalar per ruleset that
    /// is compared and then reset to -1). Those two are siblings sharing one
    /// <c>LastAppliedDifficultyVersion</c>; the chart chain has real { MSD -> CSI -> ChartDan }
    /// cascade, which a single scalar cannot express without also invalidating MSD on a CSI bump.
    /// </para>
    /// </summary>
    public static class EzAnalysisRevision
    {
        /// <summary>
        /// Policy slots: bump when a facet's production changes in a way none of the algorithm
        /// constants capture (a new dependency edge, a changed read path). Slot 1 is the first
        /// stamped-on value, which retires every pre-revision row exactly once.
        /// </summary>
        internal const int CSI_POLICY = 1;

        /// <inheritdoc cref="CSI_POLICY"/>
        internal const int DAN_POLICY = 1;

        /// <summary>Number base of each slot. Every input constant must stay below this.</summary>
        internal const int SLOT_RADIX = 100;

        /// <summary>
        /// MSD rows keep the raw <see cref="EzManiaSkillAlgorithm.VERSION"/>: <see cref="EzBeatmapSkillValue"/>
        /// is shared with player SSR / pattern rows and several readers pass the algorithm version
        /// explicitly, so composing it would be a cross-facet hazard. MSD staleness is already handled
        /// by the <c>RulesetInfo.LastAppliedManiaSkillVersion</c> scalar.
        /// </summary>
        public static int Msd => EzManiaSkillAlgorithm.VERSION;

        /// <summary>Revision stamped on <c>EzBeatmapChartSkillInfo.InfoVersion</c>.</summary>
        public static int ChartSkillInfo
            => compose(CSI_POLICY, EzChartSkillInfo.VERSION, EzManiaSkillAlgorithm.VERSION);

        /// <summary>Revision stamped on <c>EzBeatmapChartDan.AlgorithmVersion</c>.</summary>
        public static int ChartDan
            => compose(DAN_POLICY, EzDanAlgorithm.VERSION, EzChartSkillInfo.VERSION, EzManiaSkillAlgorithm.VERSION);

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
                    return EzAnalysisFacet.ChartSkillInfo | EzAnalysisFacet.ChartDan;

                case EzAnalysisFacet.ChartSkillInfo:
                    return EzAnalysisFacet.ChartDan;

                default:
                    return EzAnalysisFacet.None;
            }
        }

        public static string DescribeChartSkillInfo(int revision)
            => $"policy={slotAt(revision, 3, 0)} csi={slotAt(revision, 3, 1)} msd={slotAt(revision, 3, 2)}";

        public static string DescribeChartDan(int revision)
            => $"policy={slotAt(revision, 4, 0)} dan={slotAt(revision, 4, 1)} csi={slotAt(revision, 4, 2)} msd={slotAt(revision, 4, 3)}";

        /// <summary>Base-100 composition of ordered slots (most significant first).</summary>
        private static int compose(params int[] slots)
        {
            int value = 0;

            foreach (int slot in slots)
                value = value * SLOT_RADIX + slot;

            return value;
        }

        /// <summary>Reads one slot out of a composed revision, where index 0 is the most significant.</summary>
        private static int slotAt(int revision, int totalSlots, int index)
        {
            int divisor = 1;

            for (int i = totalSlots - 1 - index; i > 0; i--)
                divisor *= SLOT_RADIX;

            return revision / divisor % SLOT_RADIX;
        }
    }
}
