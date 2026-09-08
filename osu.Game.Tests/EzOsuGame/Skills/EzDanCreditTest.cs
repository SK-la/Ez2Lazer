// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanCreditTest
    {
        [Test]
        public void AtBarCreditsFullChartDan()
        {
            double? credited = EzDanCredit.CreditedDanFor(10, 0.96, DanSkillSystem.SIDE_RC, 4);
            Assert.That(credited, Is.EqualTo(10).Within(0.001));
        }

        [Test]
        public void FarBelowBarCreditsNothing()
        {
            double? credited = EzDanCredit.CreditedDanFor(10, 0.80, DanSkillSystem.SIDE_RC, 4);
            Assert.That(credited, Is.Null);
        }

        [Test]
        public void ParseDanLabelsGreek()
        {
            Assert.That(EzDanLabels.LabelFor(11, DanSkillSystem.SIDE_RC, 4), Does.StartWith("alpha"));
            Assert.That(EzDanLabels.LabelFor(10.0, DanSkillSystem.SIDE_LN, 4), Is.EqualTo("10"));
        }

        [Test]
        public void SrToRawDanIncreasesWithSr()
        {
            double low = EzDanLabels.SrToRawDan(4.0, "stream");
            double high = EzDanLabels.SrToRawDan(8.0, "stream");
            Assert.That(high, Is.GreaterThan(low));
        }
    }
}
