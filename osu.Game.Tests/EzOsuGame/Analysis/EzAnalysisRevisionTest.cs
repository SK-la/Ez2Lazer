// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The facet revisions are read off the producer registry. No Realm is opened.
    /// </summary>
    [TestFixture]
    public class EzAnalysisRevisionTest
    {
        private const int mania = EzLocalProfileConstants.MANIA_RULESET_ID;

        [SetUp]
        public void SetUp()
        {
            EzAnalysisRevision.Configure(new EzMetricRevisionResolver(
                EzChartMetricProducers.CreateRegistry(EzNullUpstreamRevisions.INSTANCE),
                EzPlayerMetricProducers.CreateRegistry()));
        }

        [Test]
        public void Msd_keeps_the_raw_algorithm_version()
        {
            // Mina declares no data dependency, so its revision stays the engine constant already on disk.
            // EzBeatmapSkillValue is shared with player SSR / pattern rows and readers pass the version explicitly.
            Assert.That(EzAnalysisRevision.Msd, Is.EqualTo(EzMinaSkillAlgorithm.VERSION));
        }

        [Test]
        public void Chart_skill_info_keeps_its_own_version()
        {
            // CSI is pure chart structure; it reads no MSD axes, so MSD is not an input.
            Assert.That(EzAnalysisRevision.ChartSkillInfo, Is.EqualTo(EzChartSkillInfo.VERSION));
            Assert.That(EzAnalysisRevision.ChartSkillInfo, Is.Not.EqualTo(EzAnalysisRevision.Msd));
        }

        [Test]
        public void Chart_dan_folds_dan_csi_and_msd()
        {
            int danOnly = danRegistry(Array.Empty<EzMetricProducerId>())
                .RevisionOf(EzChartMetricProducer.Dan.Id(), mania);

            // Folding the two inputs must move the number away from the dan constant alone.
            Assert.That(EzAnalysisRevision.ChartDan, Is.Not.EqualTo(danOnly));
            Assert.That(EzAnalysisRevision.ChartDan, Is.Not.EqualTo(EzAnalysisRevision.ChartSkillInfo));
        }

        [Test]
        public void Chart_dan_changes_when_either_input_bumps()
        {
            int baseline = EzAnalysisRevision.ChartDan;

            EzAnalysisRevision.Configure(registryWith(csiVersion: EzChartSkillInfo.VERSION + 1));
            int afterCsi = EzAnalysisRevision.ChartDan;
            Assert.That(afterCsi, Is.Not.EqualTo(baseline));

            EzAnalysisRevision.Configure(registryWith(minaVersion: EzMinaSkillAlgorithm.VERSION + 1));
            Assert.That(EzAnalysisRevision.ChartDan, Is.Not.EqualTo(afterCsi));
        }

        [Test]
        public void Downstream_edges_match_the_declared_dependency_chain()
        {
            // MSD -> ChartDan and ChartSkillInfo -> ChartDan: the two chart facets are independent
            // inputs and fan in at Dan. Bumping an upstream facet must invalidate every downstream
            // facet, which is what makes the incremental backfill recompute them.
            Assert.That(EzAnalysisRevision.DownstreamOf(EzAnalysisFacet.Msd),
                Is.EqualTo(EzAnalysisFacet.ChartDan));

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

        private static EzMetricRevisionResolver danRegistry(IReadOnlyList<EzMetricProducerId> dependsOn)
            => new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[]
                {
                    new EzMetricProducer(EzChartMetricProducer.Dan.Id(), EzMetricTier.Dan, EzMetricUnit.DanRank, _ => true)
                    {
                        DependsOn = dependsOn,
                        RevisionProbe = _ => EzDanAlgorithm.VERSION,
                    },
                }),
                new EzMetricProducerRegistry());

        private static EzMetricRevisionResolver registryWith(int? csiVersion = null, int? minaVersion = null)
            => new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[]
                {
                    new EzMetricProducer(EzChartMetricProducer.Csi.Id(), EzMetricTier.Tags, EzMetricUnit.Count, _ => true)
                    {
                        RevisionProbe = _ => csiVersion ?? EzChartSkillInfo.VERSION,
                    },
                    new EzMetricProducer(EzChartMetricProducer.Mina.Id(), EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
                    {
                        RevisionProbe = _ => minaVersion ?? EzMinaSkillAlgorithm.VERSION,
                    },
                    new EzMetricProducer(EzChartMetricProducer.Dan.Id(), EzMetricTier.Dan, EzMetricUnit.DanRank, _ => true)
                    {
                        DependsOn = new[] { EzChartMetricProducer.Mina.Id(), EzChartMetricProducer.Csi.Id() },
                        RevisionProbe = _ => EzDanAlgorithm.VERSION,
                    },
                }),
                new EzMetricProducerRegistry());
    }
}
