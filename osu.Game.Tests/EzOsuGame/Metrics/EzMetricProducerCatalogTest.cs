// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    /// <summary>
    /// The two catalogs are separate namespaces: a player producer may read chart rows, a chart producer may not
    /// read player rows, and neither may reuse the other's id.
    /// </summary>
    [TestFixture]
    public class EzMetricProducerCatalogTest
    {
        private const int osu = EzLocalProfileConstants.OSU_RULESET_ID;
        private const int mania = EzLocalProfileConstants.MANIA_RULESET_ID;

        [Test]
        public void Player_ids_never_collide_with_chart_ids()
        {
            var chartIds = EzChartMetricProducers.All.Select(p => p.Id()).ToHashSet();
            var playerIds = EzPlayerMetricProducers.All.Select(p => p.Id()).ToHashSet();

            Assert.That(playerIds.Overlaps(chartIds), Is.False);
            Assert.That(EzPlayerMetricProducers.All.All(p => p.Id().Id.EndsWith("_player", System.StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void Player_producers_may_only_depend_on_chart_producers()
        {
            foreach (var producer in EzPlayerMetricProducers.All)
                Assert.That(producer.Meta().PlayerDependsOn, Is.Empty, $"{producer} must not read another player producer");

            foreach (var producer in EzChartMetricProducers.All)
                Assert.That(producer.Meta().ChartDependsOn.All(System.Enum.IsDefined), Is.True);
        }

        [Test]
        public void Mania_prefers_its_rating_engine_and_other_rulesets_prefer_ppp()
        {
            var chart = EzChartMetricProducers.CreateRegistry();

            Assert.That(chart.Get(EzChartMetricProducer.Mina.Id())!.IsDefaultRadarFor(mania), Is.True);
            Assert.That(chart.Get(EzChartMetricProducer.Ppp.Id())!.IsDefaultRadarFor(mania), Is.False);
            Assert.That(chart.Get(EzChartMetricProducer.Ppp.Id()).IsDefaultRadarFor(osu), Is.True);
            Assert.That(chart.Get(EzChartMetricProducer.Mina.Id())!.IsDefaultRadarFor(osu), Is.False);
        }

        [Test]
        public void Declared_axis_source_matches_the_engine_it_names()
        {
            var chart = EzChartMetricProducers.CreateRegistry();

            var pppAxes = chart.Get(EzChartMetricProducer.Ppp.Id())!.AxesFor(osu);
            Assert.That(pppAxes.Select(a => a.Id), Is.EqualTo(EzPPPSkillCatalog.MetricsFor(osu).Select(m => m.MetricId)));

            var minaAxes = chart.Get(EzChartMetricProducer.Mina.Id())!.AxesFor(mania);
            Assert.That(minaAxes, Is.Not.Empty);
        }

        [Test]
        public void A_chart_producer_has_no_player_dependency_edge()
        {
            var chart = EzChartMetricProducers.CreateRegistry();
            var playerIds = EzPlayerMetricProducers.All.Select(p => p.Id()).ToHashSet();

            foreach (var producer in chart.All)
                Assert.That(producer.DependsOn.Any(playerIds.Contains), Is.False);
        }
    }
}
