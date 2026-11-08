// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
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

        private static EzMetricProducerRegistry registry =>
            EzMetricProducers.CreateDefault(new FixedUpstream());

        [Test]
        public void The_same_declaration_yields_the_same_order()
        {
            var first = registry.Items(mania, EzSkillScope.Beatmap);
            var second = EzMetricProducers.CreateDefault(new FixedUpstream()).Items(mania, EzSkillScope.Beatmap);

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
            var items = registry.Items(mania, EzSkillScope.Beatmap);

            foreach (var item in items)
            {
                var producer = registry.Get(item.Producer);

                Assert.That(producer, Is.Not.Null);

                Assert.That(producer!.AxisGroups, Has.Some.Matches<EzMetricAxisGroup>(g => g.Id == item.GroupId),
                    $"{item.SkillId} reports group '{item.GroupId}'");
            }
        }

        [Test]
        public void Items_are_scoped_to_one_side()
        {
            var chart = registry.Items(mania, EzSkillScope.Beatmap);
            var player = registry.Items(mania, EzSkillScope.Player);

            Assert.That(chart, Has.All.Matches<EzMetricSkillItem>(i => i.Scope == EzSkillScope.Beatmap));
            Assert.That(player, Has.All.Matches<EzMetricSkillItem>(i => i.Scope == EzSkillScope.Player));
        }

        [Test]
        public void Mina_chart_axes_are_all_listed()
        {
            var mina = registry.Items(mania, EzSkillScope.Beatmap)
                               .Where(i => i.Producer == EzMetricProducers.MINA_CHART)
                               .ToList();

            Assert.That(mina.Count, Is.EqualTo(EzMinaSkillAxisExtensions.All.Length));

            foreach (var axis in EzMinaSkillAxisExtensions.All)
                Assert.That(mina, Has.Some.Matches<EzMetricSkillItem>(i => i.Axis.Id == axis.ToId()));
        }

        private static List<EzMetricSkillItem> pppItems(int rulesetOnlineId)
            => registry.Items(rulesetOnlineId, EzSkillScope.Beatmap)
                       .Where(i => i.Producer == EzMetricProducers.PPP_CHART)
                       .ToList();

        private sealed class FixedUpstream : IEzMetricUpstreamRevisions
        {
            public int OfficialDifficulty(int rulesetOnlineId) => 3;

            public int Ssr(int rulesetOnlineId) => 20250415;
        }
    }
}
