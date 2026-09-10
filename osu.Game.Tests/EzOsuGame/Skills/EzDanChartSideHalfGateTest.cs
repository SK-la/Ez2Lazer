// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanChartSideHalfGateTest
    {
        [TestCase(0.1, false)]
        [TestCase(0.44, false)]
        [TestCase(0.45, true)]
        [TestCase(0.9, true)]
        public void Allows_ln_half_only_at_or_above_primary_ratio_4k(double hold, bool expected)
        {
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Ln, 4, hold), Is.EqualTo(expected));
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Rc, 4, hold), Is.EqualTo(!expected));
        }

        [Test]
        public void Allows_ln_half_uses_lower_7k_threshold()
        {
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Ln, 7, 0.37), Is.False);
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Ln, 7, 0.375), Is.True);
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Rc, 7, 0.37), Is.True);
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Rc, 7, 0.375), Is.False);
        }
    }
}
