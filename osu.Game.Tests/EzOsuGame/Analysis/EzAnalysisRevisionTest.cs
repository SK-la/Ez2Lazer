// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// Pure composition/decoding rules of the facet revision numbers. No Realm is opened.
    /// </summary>
    [TestFixture]
    public class EzAnalysisRevisionTest
    {
        [Test]
        public void Msd_keeps_the_raw_algorithm_version()
        {
            // MSD rows keep the plain constant: EzBeatmapSkillValue is shared with player SSR /
            // pattern rows and readers pass the algorithm version explicitly.
            Assert.That(EzAnalysisRevision.Msd, Is.EqualTo(EzManiaSkillAlgorithm.VERSION));
        }

        [Test]
        public void Chart_skill_info_folds_its_own_version_and_msd()
        {
            Assert.That(EzAnalysisRevision.DescribeChartSkillInfo(EzAnalysisRevision.ChartSkillInfo),
                Is.EqualTo($"policy={EzAnalysisRevision.CSI_POLICY} csi={EzChartSkillInfo.VERSION} msd={EzManiaSkillAlgorithm.VERSION}"));

            // Distinct upstream values must produce distinct revisions (no slot collision).
            Assert.That(EzAnalysisRevision.ChartSkillInfo, Is.Not.EqualTo(EzAnalysisRevision.Msd));
        }

        [Test]
        public void Chart_dan_folds_dan_csi_and_msd()
        {
            Assert.That(EzAnalysisRevision.DescribeChartDan(EzAnalysisRevision.ChartDan),
                Is.EqualTo($"policy={EzAnalysisRevision.DAN_POLICY} dan={EzDanAlgorithm.VERSION} csi={EzChartSkillInfo.VERSION} msd={EzManiaSkillAlgorithm.VERSION}"));

            Assert.That(EzAnalysisRevision.ChartDan, Is.Not.EqualTo(EzAnalysisRevision.ChartSkillInfo));
        }

        [Test]
        public void Downstream_edges_match_the_declared_dependency_chain()
        {
            // Msd -> ChartSkillInfo -> ChartDan. Bumping an upstream facet must invalidate every
            // downstream facet, which is what makes the incremental backfill recompute them.
            Assert.That(EzAnalysisRevision.DownstreamOf(EzAnalysisFacet.Msd),
                Is.EqualTo(EzAnalysisFacet.ChartSkillInfo | EzAnalysisFacet.ChartDan));

            Assert.That(EzAnalysisRevision.DownstreamOf(EzAnalysisFacet.ChartSkillInfo),
                Is.EqualTo(EzAnalysisFacet.ChartDan));

            Assert.That(EzAnalysisRevision.DownstreamOf(EzAnalysisFacet.ChartDan),
                Is.EqualTo(EzAnalysisFacet.None));
        }

        [Test]
        public void For_maps_each_chart_facet_to_its_revision()
        {
            Assert.That(EzAnalysisRevision.For(EzAnalysisFacet.Msd), Is.EqualTo(EzAnalysisRevision.Msd));
            Assert.That(EzAnalysisRevision.For(EzAnalysisFacet.ChartSkillInfo), Is.EqualTo(EzAnalysisRevision.ChartSkillInfo));
            Assert.That(EzAnalysisRevision.For(EzAnalysisFacet.ChartDan), Is.EqualTo(EzAnalysisRevision.ChartDan));

            // Non-chart facets are not part of this chain.
            Assert.That(EzAnalysisRevision.For(EzAnalysisFacet.SsrAggregation), Is.EqualTo(0));
            Assert.That(EzAnalysisRevision.For(EzAnalysisFacet.Aggregation), Is.EqualTo(0));
        }

        [Test]
        public void Every_input_constant_fits_its_base_100_slot()
        {
            // A constant >= SLOT_RADIX would carry into the neighbouring slot and silently make two
            // different inputs share a revision.
            Assert.That(EzManiaSkillAlgorithm.VERSION, Is.LessThan(EzAnalysisRevision.SLOT_RADIX));
            Assert.That(EzChartSkillInfo.VERSION, Is.LessThan(EzAnalysisRevision.SLOT_RADIX));
            Assert.That(EzDanAlgorithm.VERSION, Is.LessThan(EzAnalysisRevision.SLOT_RADIX));
            Assert.That(EzAnalysisRevision.CSI_POLICY, Is.LessThan(EzAnalysisRevision.SLOT_RADIX));
            Assert.That(EzAnalysisRevision.DAN_POLICY, Is.LessThan(EzAnalysisRevision.SLOT_RADIX));
        }
    }
}
