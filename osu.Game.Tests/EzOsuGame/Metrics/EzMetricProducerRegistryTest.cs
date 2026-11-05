// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    [TestFixture]
    public class EzMetricProducerRegistryTest
    {
        private const int mania = EzLocalProfileConstants.MANIA_RULESET_ID;
        private const int osu = EzLocalProfileConstants.OSU_RULESET_ID;

        private static readonly EzMetricProducerId chart_rating = new EzMetricProducerId("rating", EzSkillScope.Beatmap);
        private const string chart_rating_id = "rating";
        private static readonly EzMetricProducerId chart_tags = new EzMetricProducerId("tags", EzSkillScope.Beatmap);
        private static readonly EzMetricProducerId performance = new EzMetricProducerId("pp", EzSkillScope.Beatmap);
        private static readonly EzMetricProducerId player_rating = new EzMetricProducerId("rating", EzSkillScope.Player);

        [Test]
        public void Same_name_on_two_scopes_is_two_producers()
        {
            Assert.That(chart_rating, Is.Not.EqualTo(player_rating));
            Assert.That(chart_rating.CompareTo(player_rating), Is.Not.EqualTo(0));
        }

        [Test]
        public void Folding_the_same_graph_twice_yields_the_same_revision()
        {
            var registry = buildRegistry();
            int first = registry.RevisionOf(performance, mania);
            int second = buildRegistry().RevisionOf(performance, mania);

            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Bumping_a_dependency_retires_its_consumers()
        {
            int before = buildRegistry(ratingVersion: 1).RevisionOf(performance, mania);
            int after = buildRegistry(ratingVersion: 2).RevisionOf(performance, mania);

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void Bumping_an_unrelated_producer_does_not_retire_a_consumer()
        {
            int before = buildRegistry(tagVersion: 1).RevisionOf(performance, mania);
            int after = buildRegistry(tagVersion: 2).RevisionOf(performance, mania);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void Shared_axis_vocabulary_is_not_a_version_edge()
        {
            // A player rating reuses the chart rating's axis names and engine but reads none of its rows,
            // so a chart-side bump must leave the player value valid.
            int before = buildRegistry(ratingVersion: 1).RevisionOf(player_rating, mania);
            int after = buildRegistry(ratingVersion: 99).RevisionOf(player_rating, mania);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void A_dependency_chain_folds_transitively()
        {
            var registry = new EzMetricProducerRegistry(new[]
            {
                producer(chart_rating, "rating", _ => 1),
                producer(chart_tags, "tags", _ => 1, dependsOn: new[] { chart_rating }),
                producer(performance, "pp", _ => 1, dependsOn: new[] { chart_tags }),
            });

            int baseRevision = registry.RevisionOf(performance, mania);

            var bumped = new EzMetricProducerRegistry(new[]
            {
                producer(chart_rating, "rating", _ => 2),
                producer(chart_tags, "tags", _ => 1, dependsOn: new[] { chart_rating }),
                producer(performance, "pp", _ => 1, dependsOn: new[] { chart_tags }),
            });

            Assert.That(bumped.RevisionOf(performance, mania), Is.Not.EqualTo(baseRevision));
        }

        [Test]
        public void Unknown_or_inapplicable_producers_have_no_revision()
        {
            var registry = buildRegistry();
            var unknown = new EzMetricProducerId("nope", EzSkillScope.Beatmap);

            Assert.That(registry.RevisionOf(unknown, mania), Is.EqualTo(0));
            Assert.That(registry.RevisionOf(performance, osu), Is.EqualTo(0));
        }

        [Test]
        public void A_declared_cycle_fails_loudly()
        {
            var registry = new EzMetricProducerRegistry(new[]
            {
                producer(chart_rating, "a", _ => 1, dependsOn: new[] { chart_tags }),
                producer(chart_tags, "b", _ => 1, dependsOn: new[] { chart_rating }),
            });

            Assert.Throws<InvalidOperationException>(() => registry.RevisionOf(chart_rating, mania));
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

            // The group does not cover osu, so nothing is owed there.
            Assert.That(perRuleset.IsCompleteFor(0, osu, empty), Is.True);
            Assert.That(perRuleset.IsCompleteFor(0, mania, empty), Is.False);
        }

        [Test]
        public void A_producer_may_declare_a_different_axis_set_per_ruleset()
        {
            var registry = EzMetricProducers.CreateDefault(new FixedUpstream());
            var ppp = registry.Get(EzMetricProducers.PPP_CHART);

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
            Assert.That(producerId, Is.EqualTo(chart_rating_id));
            Assert.That(axisId, Is.EqualTo("jump"));

            Assert.That(EzMetricSkillId.TrySplit("no-separator", out _, out _), Is.False);
            Assert.That(EzMetricSkillId.TrySplit(null, out _, out _), Is.False);
        }

        private static EzMetricProducerRegistry buildRegistry(int ratingVersion = 1, int tagVersion = 1)
        {
            return new EzMetricProducerRegistry(new[]
            {
                producer(chart_rating, "rating", _ => ratingVersion),
                producer(chart_tags, "tags", _ => tagVersion),
                producer(performance, "pp", _ => 1, dependsOn: new[] { chart_rating }),
                // Same name, player side: shares the axis vocabulary but reads no chart rows.
                new EzMetricProducer(player_rating, EzMetricTier.Skill, EzMetricUnit.SkillPoint, _ => true)
                {
                    AxisFamilyId = chart_rating_id,
                    RevisionProbe = _ => 1,
                },
            });
        }

        private static EzMetricProducer producer(EzMetricProducerId id, string family, Func<int, int> probe,
                                                 IReadOnlyList<EzMetricProducerId>? dependsOn = null)
        {
            return new EzMetricProducer(id, EzMetricTier.Star, EzMetricUnit.Star, ruleset => ruleset == mania)
            {
                AxisFamilyId = family,
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
