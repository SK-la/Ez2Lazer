// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    [TestFixture]
    public class EzMetricProducerRegistryTest
    {
        private const int mania = EzLocalProfileConstants.MANIA_RULESET_ID;
        private const int osu = EzLocalProfileConstants.OSU_RULESET_ID;

        private static readonly EzMetricProducerId chart_rating = new EzMetricProducerId("rating");
        private static readonly EzMetricProducerId chart_tags = new EzMetricProducerId("tags");
        private static readonly EzMetricProducerId performance = new EzMetricProducerId("pp");
        private static readonly EzMetricProducerId player_rating = new EzMetricProducerId("rating_player");

        [Test]
        public void The_two_catalogs_share_no_id()
        {
            var chart = EzChartMetricProducers.CreateRegistry();
            var player = EzPlayerMetricProducers.CreateRegistry();

            var chartIds = chart.All.Select(p => p.Id).ToList();
            var playerIds = player.All.Select(p => p.Id).ToList();

            Assert.That(chartIds, Has.None.Matches<EzMetricProducerId>(playerIds.Contains));
        }

        [Test]
        public void Derived_ids_are_unique_and_bare()
        {
            foreach (var producer in EzChartMetricProducers.CreateRegistry().All.Concat(EzPlayerMetricProducers.CreateRegistry().All))
            {
                Assert.That(producer.Id.Id, Is.Not.Empty);
                Assert.That(producer.Id.Id, Does.Not.Contain("."));
                Assert.That(producer.Id.Id, Is.EqualTo(producer.Id.Id.ToLowerInvariant()));
            }
        }

        [Test]
        public void Folding_the_same_graph_twice_yields_the_same_revision()
        {
            var resolver = buildResolver();
            int first = resolver.RevisionOf(performance, mania);
            int second = buildResolver().RevisionOf(performance, mania);

            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Bumping_a_dependency_retires_its_consumers()
        {
            int before = buildResolver(ratingVersion: 1).RevisionOf(performance, mania);
            int after = buildResolver(ratingVersion: 2).RevisionOf(performance, mania);

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void Bumping_an_unrelated_producer_does_not_retire_a_consumer()
        {
            int before = buildResolver(tagVersion: 1).RevisionOf(performance, mania);
            int after = buildResolver(tagVersion: 2).RevisionOf(performance, mania);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void Shared_axis_vocabulary_is_not_a_version_edge()
        {
            // A player rating reuses the chart rating's axis names and engine but reads none of its rows,
            // so a chart-side bump must leave the player value valid.
            int before = buildResolver(ratingVersion: 1).RevisionOf(player_rating, mania);
            int after = buildResolver(ratingVersion: 99).RevisionOf(player_rating, mania);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void A_player_producer_may_read_a_chart_producer_across_catalogs()
        {
            var resolver = buildResolver();

            var playerReadsChart = new EzMetricProducer(player_rating, EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
            {
                DependsOn = new[] { chart_rating },
                RevisionProbe = _ => 1,
            };

            var withEdge = new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[] { producer(chart_rating, _ => 1) }),
                new EzMetricProducerRegistry(new[] { playerReadsChart }));

            var bumpedChart = new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[] { producer(chart_rating, _ => 2) }),
                new EzMetricProducerRegistry(new[] { playerReadsChart }));

            Assert.That(withEdge.RevisionOf(player_rating, mania), Is.Not.EqualTo(bumpedChart.RevisionOf(player_rating, mania)));

            // The reverse edge does not exist: a chart producer never resolves a player id.
            Assert.That(resolver.RevisionOf(player_rating, mania), Is.GreaterThan(0));
        }

        [Test]
        public void A_dependency_chain_folds_transitively()
        {
            int baseRevision = buildResolver().RevisionOf(performance, mania);

            var bumped = new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[]
                {
                    producer(chart_rating, _ => 2),
                    producer(chart_tags, _ => 1, dependsOn: new[] { chart_rating }),
                    producer(performance, _ => 1, dependsOn: new[] { chart_tags }),
                }),
                new EzMetricProducerRegistry());

            Assert.That(bumped.RevisionOf(performance, mania), Is.Not.EqualTo(baseRevision));
        }

        [Test]
        public void Unknown_or_inapplicable_producers_have_no_revision()
        {
            var resolver = buildResolver();
            var unknown = new EzMetricProducerId("nope");

            Assert.That(resolver.RevisionOf(unknown, mania), Is.EqualTo(0));
            Assert.That(resolver.RevisionOf(performance, osu), Is.EqualTo(0));
        }

        [Test]
        public void A_declared_cycle_fails_loudly()
        {
            var resolver = new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[]
                {
                    producer(chart_rating, _ => 1, dependsOn: new[] { chart_tags }),
                    producer(chart_tags, _ => 1, dependsOn: new[] { chart_rating }),
                }),
                new EzMetricProducerRegistry());

            Assert.Throws<InvalidOperationException>(() => resolver.RevisionOf(chart_rating, mania));
        }

        [Test]
        public void A_group_only_gates_the_slices_it_applies_to()
        {
            var axis = new EzMetricAxis("x", "X", "#ffffff", true, 0);
            var producerWithSliceGate = new EzMetricProducer(chart_rating, EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
            {
                Sliced = true,
                AxisGroups = new[] { new EzMetricAxisGroup("g", new[] { axis }, static slice => slice == 7) },
            };

            var empty = new Dictionary<string, double>(StringComparer.Ordinal);
            if (empty == null)
                throw new ArgumentNullException(nameof(empty));

            var filled = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricSkillId.For(chart_rating, "x")] = 1,
            };

            Assert.That(producerWithSliceGate.IsCompleteFor(4, mania, empty), Is.True);
            Assert.That(producerWithSliceGate.IsCompleteFor(7, mania, empty), Is.False);
            Assert.That(producerWithSliceGate.IsCompleteFor(7, mania, filled), Is.True);
        }

        [Test]
        public void A_group_only_gates_the_rulesets_it_applies_to()
        {
            var axis = new EzMetricAxis("x", "X", "#ffffff", true, 0);
            var perRuleset = new EzMetricProducer(chart_rating, EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
            {
                AxisGroups = new[] { new EzMetricAxisGroup("g", new[] { axis }, appliesToRuleset: r => r == mania) },
            };

            var empty = new Dictionary<string, double>(StringComparer.Ordinal);
            if (empty == null)
                throw new ArgumentNullException(nameof(empty));

            // The group does not cover osu, so nothing is owed there.
            Assert.That(perRuleset.IsCompleteFor(0, osu, empty), Is.True);
            Assert.That(perRuleset.IsCompleteFor(0, mania, empty), Is.False);
        }

        [Test]
        public void A_producer_may_declare_a_different_axis_set_per_ruleset()
        {
            var registry = EzChartMetricProducers.CreateRegistry(new FixedUpstream());
            var ppp = registry.Get(EzChartMetricProducer.Ppp.Id());

            Assert.That(ppp, Is.Not.Null);

            var osuAxes = ppp!.AxesFor(osu);
            var maniaAxes = ppp.AxesFor(mania);

            Assert.That(osuAxes.Count, Is.GreaterThan(maniaAxes.Count));
            Assert.That(osuAxes.Select(a => a.Id), Does.Contain(EzPPPSkillCatalog.STAR_RATING));
            Assert.That(maniaAxes.Select(a => a.Id), Does.Contain(EzPPPSkillCatalog.STRAIN));
        }

        [Test]
        public void Every_declared_axis_is_required_for_completeness()
        {
            var axes = new[]
            {
                new EzMetricAxis("a", "A", "#ffffff", true, 0),
                new EzMetricAxis("b", "B", "#ffffff", true, 1),
            };

            var producerWithTwoAxes = new EzMetricProducer(chart_rating, EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
            {
                AxisGroups = new[] { new EzMetricAxisGroup("g", axes) },
            };

            var partial = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricSkillId.For(chart_rating, "a")] = 1,
            };

            Assert.That(producerWithTwoAxes.IsCompleteFor(0, mania, partial), Is.False);
        }

        [Test]
        public void Skill_ids_round_trip_through_the_producer_name()
        {
            Assert.That(EzMetricSkillId.For(chart_rating, "jump"), Is.EqualTo("rating.jump"));

            Assert.That(EzMetricSkillId.TrySplit("rating.jump", out string producerId, out string axisId), Is.True);
            Assert.That(producerId, Is.EqualTo("rating"));
            Assert.That(axisId, Is.EqualTo("jump"));

            Assert.That(EzMetricSkillId.TrySplit("no-separator", out _, out _), Is.False);
            Assert.That(EzMetricSkillId.TrySplit(null, out _, out _), Is.False);
        }

        private static EzMetricRevisionResolver buildResolver(int ratingVersion = 1, int tagVersion = 1)
        {
            return new EzMetricRevisionResolver(
                new EzMetricProducerRegistry(new[]
                {
                    producer(chart_rating, _ => ratingVersion),
                    producer(chart_tags, _ => tagVersion),
                    producer(performance, _ => 1, dependsOn: new[] { chart_rating }),
                }),
                new EzMetricProducerRegistry(new[]
                {
                    // Same axis vocabulary as the chart side, but reads none of its rows.
                    new EzMetricProducer(player_rating, EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
                    {
                        Family = EzMetricFamily.Official,
                        RevisionProbe = _ => 1,
                    },
                }));
        }

        private static EzMetricProducer producer(EzMetricProducerId id, Func<int, int> probe, IReadOnlyList<EzMetricProducerId>? dependsOn = null)
        {
            return new EzMetricProducer(id, EzMetricTier.Star, EzMetricUnit.Star, ruleset => ruleset == mania)
            {
                Family = EzMetricFamily.Official,
                DependsOn = dependsOn ?? Array.Empty<EzMetricProducerId>(),
                RevisionProbe = probe,
            };
        }

        private sealed class FixedUpstream : IEzMetricUpstreamRevisions
        {
            public int OfficialDifficulty(int rulesetOnlineId) => 3;

            public int Ssr(int rulesetOnlineId) => 20250415;
        }
    }
}
