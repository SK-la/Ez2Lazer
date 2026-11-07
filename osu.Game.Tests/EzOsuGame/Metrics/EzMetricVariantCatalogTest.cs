// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    [TestFixture]
    public class EzMetricVariantCatalogTest
    {
        private const int mania = EzLocalProfileConstants.MANIA_RULESET_ID;
        private const int osu = EzLocalProfileConstants.OSU_RULESET_ID;

        private readonly EzMetricVariantCatalog catalog = new EzMetricVariantCatalog(EzMetricRegistries.Current);

        [Test]
        public void Mania_offers_the_three_systems_osu_offers_two()
        {
            Assert.That(catalog.For(mania).Select(v => v.Id), Is.EqualTo(new[] { EzMetricVariantCatalog.OFFICIAL, EzMetricVariantCatalog.SUNNY, EzMetricVariantCatalog.PPP }));
            Assert.That(catalog.For(osu).Select(v => v.Id), Is.EqualTo(new[] { EzMetricVariantCatalog.OFFICIAL, EzMetricVariantCatalog.PPP }));
        }

        [Test]
        public void Every_variant_names_a_star_producer_that_applies_to_its_ruleset()
        {
            foreach (int ruleset in new[] { mania, osu })
            {
                foreach (var variant in catalog.For(ruleset))
                    Assert.That(catalog.Get(variant.Id, ruleset), Is.SameAs(variant), $"{variant.Id} on {ruleset}");
            }
        }

        [Test]
        public void Sunny_is_not_pairable_until_it_declares_a_player_producer()
        {
            foreach (int ruleset in new[] { mania, osu })
            {
                foreach (var variant in catalog.For(ruleset))
                {
                    bool expected = variant.Id != EzMetricVariantCatalog.SUNNY;
                    Assert.That(catalog.IsPairable(variant), Is.EqualTo(expected), $"{variant.Id} on {ruleset}");
                }
            }
        }

        [Test]
        public void Unknown_variant_id_resolves_to_nothing() => Assert.That(catalog.Get("nope", mania), Is.Null);
    }
}
