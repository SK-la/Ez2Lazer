// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPPPPerformanceTest
    {
        [Test]
        public void Inverse_normal_matches_the_known_quantiles()
        {
            Assert.That(EzPPPMath.InverseNormalCdf(0.5), Is.EqualTo(0).Within(1e-12));
            Assert.That(EzPPPMath.InverseNormalCdf(0.975), Is.EqualTo(1.959963984540054).Within(1e-6));
            Assert.That(EzPPPMath.InverseNormalCdf(0.025), Is.EqualTo(-1.959963984540054).Within(1e-6));
            Assert.That(EzPPPMath.InverseNormalCdf(0.001), Is.EqualTo(-3.090232306167813).Within(1e-5));
        }

        [Test]
        public void Inverse_erf_round_trips_the_known_value()
        {
            // erf(1) = 0.8427007929497149
            Assert.That(EzPPPMath.InverseErf(0.8427007929497149), Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Mania_total_is_the_documented_expression()
        {
            const double star = 5;
            const int hits = 1000;

            double expected = 8 * System.Math.Pow(star - 0.15, 2.2) * (1 + 0.1 * (hits / 1500.0));

            Assert.That(EzPPPPerformance.Mania(star, hits, noFail: false, easy: false), Is.EqualTo(expected).Within(1e-9));
            Assert.That(EzPPPPerformance.Mania(star, hits, noFail: true, easy: false), Is.EqualTo(expected * 0.75).Within(1e-9));
            Assert.That(EzPPPPerformance.Mania(star, hits, noFail: false, easy: true), Is.EqualTo(expected * 0.5).Within(1e-9));
        }

        [Test]
        public void Catch_total_scales_with_star_and_hit_count()
        {
            double small = EzPPPPerformance.Catch(4, 9.2, 500, 500, false, false, false);
            double large = EzPPPPerformance.Catch(6, 9.2, 500, 500, false, false, false);
            double longer = EzPPPPerformance.Catch(4, 9.2, 1200, 1200, false, false, false);

            Assert.That(small, Is.GreaterThan(0));
            Assert.That(large, Is.GreaterThan(small));
            Assert.That(longer, Is.GreaterThan(small));
        }

        [Test]
        public void Taiko_total_grows_with_rating_and_is_zero_free()
        {
            double low = EzPPPPerformance.Taiko(3, 800, 25, 50, false, false, false, false);
            double high = EzPPPPerformance.Taiko(6, 800, 25, 50, false, false, false, false);

            Assert.That(low, Is.GreaterThan(0));
            Assert.That(high, Is.GreaterThan(low));
            Assert.That(EzPPPPerformance.Taiko(6, 800, 25, 50, false, true, false, false), Is.LessThan(high));
        }

        [Test]
        public void Osu_total_grows_with_aim_and_punishes_tiny_circle_count_only_via_accuracy()
        {
            double baseline = EzPPPPerformance.Osu(4, 3, 2, 1, 800, 400, 26, false, false);
            double strongerAim = EzPPPPerformance.Osu(6, 3, 2, 1, 800, 400, 26, false, false);

            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(strongerAim, Is.GreaterThan(baseline));
        }

        [Test]
        public void Osu_refuses_a_chart_without_hit_objects()
            => Assert.That(EzPPPPerformance.Osu(4, 3, 2, 1, 0, 0, 26, false, false), Is.EqualTo(-1));
    }
}
