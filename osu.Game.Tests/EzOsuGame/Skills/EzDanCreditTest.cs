// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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

        [Test]
        public void ChartDanFromMsdClassifiesLnSideByHoldRatio()
        {
            var msd = new Dictionary<string, double>
            {
                [EzSkillIds.Msd(EzSkillIds.OVERALL)] = 6.5,
                [EzSkillIds.Msd(EzSkillIds.STREAM)] = 6.0,
                [EzSkillIds.Msd(EzSkillIds.JUMPSTREAM)] = 5.0,
            };

            var rc = EzChartDanEstimator.FromMsd(msd, keyCount: 4, holdRatio: 0.1);
            Assert.That(rc, Is.Not.Null);
            Assert.That(rc!.Side, Is.EqualTo(DanSkillSystem.SIDE_RC));
            Assert.That(rc.RawDan, Is.GreaterThan(0));
            Assert.That(rc.Label, Is.Not.Empty);

            var ln = EzChartDanEstimator.FromMsd(msd, keyCount: 4, holdRatio: 0.5);
            Assert.That(ln, Is.Not.Null);
            Assert.That(ln!.Side, Is.EqualTo(DanSkillSystem.SIDE_LN));
            Assert.That(ln.RawDan, Is.EqualTo(rc.RawDan).Within(0.001));
        }

        [Test]
        public void SrCalibrationChangesHighSrRawDan()
        {
            double plain = EzDanLabels.SrToRawDan(9.0, "stream", calibrate: false);
            double calibrated = EzDanLabels.SrToRawDan(9.0, "stream", calibrate: true);
            Assert.That(calibrated, Is.Not.EqualTo(plain).Within(0.001));
        }
    }
}
