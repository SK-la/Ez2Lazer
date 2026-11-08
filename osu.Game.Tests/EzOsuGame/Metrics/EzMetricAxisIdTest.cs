// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    /// <summary>
    /// The axis ids that identify a star rating or a performance value across systems.
    /// </summary>
    [TestFixture]
    public class EzMetricAxisIdTest
    {
        [Test]
        public void The_ppp_catalog_and_the_shared_star_axis_agree()
        {
            // A PP+ skill id is persisted with the catalog's own id; the shared star axis must be the same string
            // or the two tables would describe the same quantity under different keys.
            Assert.That(EzMetricProducers.STAR_RATING, Is.EqualTo(EzPPPSkillCatalog.STAR_RATING));
        }

        [Test]
        public void Star_and_performance_axes_are_distinct()
        {
            Assert.That(EzMetricProducers.STAR_RATING, Is.Not.EqualTo(EzMetricProducers.PERFORMANCE_POINTS));
        }

        [Test]
        public void No_axis_id_is_empty_or_prefixed()
        {
            // Axis ids are bare: the producer identity carries the system, so a prefixed axis would encode it twice.
            foreach (string id in new[] { EzMetricProducers.STAR_RATING, EzMetricProducers.PERFORMANCE_POINTS })
            {
                Assert.That(id, Is.Not.Empty);
                Assert.That(id, Does.Not.Contain("."));
                Assert.That(id, Does.Not.StartWith("$"));
            }
        }
    }
}
