// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    /// <summary>
    /// The flattened axis catalog. Read-only: no Realm, no compute.
    /// </summary>
    [TestFixture]
    public class EzMetricSkillCatalogTest
    {
        private const int mania = EzLocalProfileConstants.MANIA_RULESET_ID;
        private const int osu = EzLocalProfileConstants.OSU_RULESET_ID;

        private static EzMetricProducerRegistry chartRegistry =>
            EzChartMetricProducers.CreateRegistry(new FixedUpstream());

        private static EzMetricProducerRegistry playerRegistry =>
            EzPlayerMetricProducers.CreateRegistry();

        [Test]
        public void The_same_declaration_yields_the_same_order()
        {
            var first = chartRegistry.Items(mania);
            var second = EzChartMetricProducers.CreateRegistry(new FixedUpstream()).Items(mania);

            Assert.That(first.Count, Is.GreaterThan(0));
            Assert.That(second.Count, Is.EqualTo(first.Count));

            for (int i = 0; i < first.Count; i++)
                Assert.That(second[i].SkillId, Is.EqualTo(first[i].SkillId), $"item {i}");
        }

        [Test]
        public void Items_cover_the_axis_set_declared_for_each_ruleset()
        {
            var osuPpp = pppItems(osu);
            var maniaPpp = pppItems(mania);

            // PP+ declares a different axis set per ruleset; osu has the full skill vector, mania only strain.
            Assert.That(osuPpp.Count, Is.GreaterThan(maniaPpp.Count));
            Assert.That(osuPpp, Has.Some.Matches<EzMetricSkillItem>(i => i.Axis.Id == EzPPPSkillCatalog.STAR_RATING));
            Assert.That(maniaPpp, Has.Some.Matches<EzMetricSkillItem>(i => i.Axis.Id == EzPPPSkillCatalog.STRAIN));
        }

        [Test]
        public void Group_id_matches_the_declared_axis_group()
        {
            var items = chartRegistry.Items(mania);

            foreach (var item in items)
            {
                var producer = chartRegistry.Get(item.Producer);

                Assert.That(producer, Is.Not.Null);

                Assert.That(producer!.AxisGroups, Has.Some.Matches<EzMetricAxisGroup>(g => g?.Id == item.GroupId),
                    $"{item.SkillId} reports group '{item.GroupId}'");
            }
        }

        [Test]
        public void Both_sides_declare_their_own_axes()
        {
            var chart = chartRegistry.Items(mania);
            var player = playerRegistry.Items(mania);

            Assert.That(chart, Is.Not.Empty);
            Assert.That(player, Is.Not.Empty);

            // The two catalogs are separate namespaces, so no producer can appear on both sides.
            Assert.That(chart.Select(i => i.Producer), Has.None.Matches<EzMetricProducerId>(p => player.Any(x => x.Producer == p)));
        }

        [Test]
        public void Mina_chart_axes_are_all_listed()
        {
            var mina = chartRegistry.Items(mania)
                                    .Where(i => i.Producer == EzChartMetricProducer.Mina.Id())
                                    .ToList();

            Assert.That(mina.Count, Is.EqualTo(EzMinaSkillAxisExtensions.All.Length));

            foreach (var axis in EzMinaSkillAxisExtensions.All)
                Assert.That(mina, Has.Some.Matches<EzMetricSkillItem>(i => i.Axis.Id == axis.ToId()));
        }

        private static List<EzMetricSkillItem> pppItems(int rulesetOnlineId)
            => chartRegistry.Items(rulesetOnlineId)
                            .Where(i => i.Producer == EzChartMetricProducer.Ppp.Id())
                            .ToList();

        private sealed class FixedUpstream : IEzMetricUpstreamRevisions
        {
            public int OfficialDifficulty(int rulesetOnlineId) => 3;

            public int Ssr(int rulesetOnlineId) => 20250415;
        }
    }
}
