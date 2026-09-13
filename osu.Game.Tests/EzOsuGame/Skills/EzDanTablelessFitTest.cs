// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// 5K / 8K–18K have no community xxy→dan table, so their chart dan is star-fitted
    /// (<see cref="EzDanLabels.FitTablelessRawDan"/>) instead of the 4K-calibrated MSD means table.
    /// </summary>
    [TestFixture]
    public class EzDanTablelessFitTest
    {
        private static Dictionary<string, double> msd(double overall, double dominantAxisValue = 12)
        {
            return new Dictionary<string, double>
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = overall,
                [EzMinaSkillAxis.Stream.ToMsdSkillId()] = dominantAxisValue,
            };
        }

        [TestCase(4)]
        [TestCase(6)]
        [TestCase(7)]
        public void KeymodesWithAnXxyTableKeepTheCalibratedMeansTable(int keyCount)
        {
            Assert.That(EzDanLabels.HasXxyDanTable(keyCount), Is.True);

            double? fitted = EzDanLabels.TryFallbackRawDan(keyCount, overallMsd: 20, EzMinaSkillAxis.Stream, xxySr: 6);

            Assert.That(fitted, Is.EqualTo(EzDanLabels.SrToRawDan(20, EzMinaSkillAxis.Stream)).Within(0.0001));
        }

        [TestCase(5)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(18)]
        public void TablelessKeymodesFitTheStarRating(int keyCount)
        {
            Assert.That(EzDanLabels.HasXxyDanTable(keyCount), Is.False);

            double? fitted = EzDanLabels.TryFallbackRawDan(keyCount, overallMsd: 20, EzMinaSkillAxis.Stream, xxySr: 6);

            // 20 × 6 × 0.05
            Assert.That(fitted, Is.EqualTo(6.0).Within(0.0001));
        }

        [Test]
        public void FitNeedsBothRatingAndStarRating()
        {
            Assert.That(EzDanLabels.FitTablelessRawDan(20, null), Is.Null);
            Assert.That(EzDanLabels.FitTablelessRawDan(20, -1), Is.Null);
            Assert.That(EzDanLabels.FitTablelessRawDan(20, 0), Is.Null);
            Assert.That(EzDanLabels.FitTablelessRawDan(0, 6), Is.Null);
            Assert.That(EzDanLabels.FitTablelessRawDan(double.NaN, 6), Is.Null);
        }

        [Test]
        public void FitIsMonotoneInBothInputs()
        {
            double? highSr = EzDanLabels.FitTablelessRawDan(24, 6);
            double? lowSr = EzDanLabels.FitTablelessRawDan(20, 6);
            double? highStar = EzDanLabels.FitTablelessRawDan(20, 7);

            Assert.That(highSr, Is.Not.Null);
            Assert.That(lowSr, Is.Not.Null);
            Assert.That(highStar, Is.Not.Null);
            Assert.That(highSr!.Value, Is.GreaterThan(lowSr!.Value));
            Assert.That(highStar!.Value, Is.GreaterThan(lowSr.Value));
        }

        [Test]
        public void TablelessChartDanLowersTheRawMsdHeuristic()
        {
            var verdict = EzChartDanEstimator.FromMsd(msd(20), keyCount: 5, holdRatio: 0.1, xxySr: 6);

            Assert.That(verdict, Is.Not.Null);
            Assert.That(verdict!.RawDan, Is.EqualTo(6.0).Within(0.0001));
            Assert.That(verdict.Label, Is.EqualTo("6"));
            Assert.That(verdict.RawDan, Is.LessThan(EzDanLabels.SrToRawDan(20, EzMinaSkillAxis.Stream)));
        }

        [Test]
        public void TablelessChartDanWithoutStarRatingIsNotInvented()
        {
            Assert.That(EzChartDanEstimator.FromMsd(msd(20), keyCount: 5, holdRatio: 0.1), Is.Null);
            Assert.That(EzChartDanEstimator.FromMsd(msd(20), keyCount: 8, holdRatio: 0.1, xxySr: -1), Is.Null);
        }

        [Test]
        public void PersistedChartDanOtherHalfUsesTheSameFit()
        {
            var computed = EzPersistedChartDan.TryComputeFromStored(
                "tableless-8k",
                System.Guid.NewGuid(),
                msd(20),
                keyCount: 8,
                holdRatio: 0.1,
                xxySr: 6,
                chartInfo: null);

            Assert.That(computed, Is.Not.Null);
            Assert.That(computed!.HasSide(EzDanSide.Rc), Is.True);
            Assert.That(computed.RcRawDan, Is.EqualTo(6.0).Within(0.0001));
            Assert.That(computed.RcLabel, Is.EqualTo("6"));
        }

        [Test]
        public void PersistedChartDanWithoutStarRatingStaysEmpty()
        {
            var computed = EzPersistedChartDan.TryComputeFromStored(
                "tableless-8k-noxxy",
                System.Guid.NewGuid(),
                msd(20),
                keyCount: 8,
                holdRatio: 0.1,
                xxySr: null,
                chartInfo: null);

            Assert.That(computed, Is.Null);
        }
    }
}
