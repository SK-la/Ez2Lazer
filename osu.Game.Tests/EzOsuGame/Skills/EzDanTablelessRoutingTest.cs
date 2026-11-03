// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// 5K / 8K–18K have no community xxy→dan table of their own, so their chart dan borrows the
    /// 4K interval table on the same side (<see cref="EzDanLabels.TryResolveFallbackDan"/>) instead
    /// of running the 4K-calibrated MSD means table, which inflated those keymodes.
    /// </summary>
    [TestFixture]
    public class EzDanTablelessRoutingTest
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
        public void KeymodesWithTheirOwnTableKeepTheMsdMeansFallback(int keyCount)
        {
            Assert.That(EzDanLabels.HasXxyDanTable(keyCount), Is.True);

            var resolved = EzDanLabels.TryResolveFallbackDan(keyCount, EzDanSide.Rc, overallMsd: 20, EzMinaSkillAxis.Stream, xxySr: null);

            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.RawDan, Is.EqualTo(EzDanLabels.SrToRawDan(20, EzMinaSkillAxis.Stream)).Within(0.0001));
            Assert.That(resolved.Value.Label, Is.EqualTo(EzDanLadders.For(keyCount, EzDanSide.Rc).ParseLabel(resolved.Value.RawDan)));
        }

        [TestCase(5)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(18)]
        public void TablelessKeymodesBorrowThe4KIntervalTable(int keyCount)
        {
            Assert.That(EzDanLabels.HasXxyDanTable(keyCount), Is.False);
            Assert.That(EzSunnyDanIntervals.TryLookup(4, DanSkillSystem.SIDE_RC, 6.0, out var fourK), Is.True);

            var resolved = EzDanLabels.TryResolveFallbackDan(keyCount, EzDanSide.Rc, overallMsd: 20, EzMinaSkillAxis.Stream, xxySr: 6.0);

            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.RawDan, Is.EqualTo(fourK.RawDan).Within(0.0001));
            Assert.That(resolved.Value.Label, Is.EqualTo(fourK.DisplayLabel));
        }

        [Test]
        public void TablelessLnSideBorrowsThe4KLnTable()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(4, DanSkillSystem.SIDE_LN, 7.0, out var fourKLn), Is.True);

            var resolved = EzDanLabels.TryResolveFallbackDan(8, EzDanSide.Ln, overallMsd: 20, EzMinaSkillAxis.Stream, xxySr: 7.0);

            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Label, Is.EqualTo(fourKLn.DisplayLabel));
        }

        [Test]
        public void BorrowedDanIsFarBelowTheRawMsdHeuristic()
        {
            var resolved = EzDanLabels.TryResolveFallbackDan(5, EzDanSide.Rc, overallMsd: 20, EzMinaSkillAxis.Stream, xxySr: 6.0);

            Assert.That(resolved, Is.Not.Null);
            // The old 5K path ran Overall MSD through the 4K means table and clamped to the ladder top.
            Assert.That(resolved!.Value.RawDan, Is.LessThan(0.5 * EzDanLabels.SrToRawDan(20, EzMinaSkillAxis.Stream)));
        }

        [Test]
        public void TablelessDanWithoutAStarRatingIsNotInvented()
        {
            Assert.That(EzDanLabels.TryResolveFallbackDan(5, EzDanSide.Rc, 20, EzMinaSkillAxis.Stream, xxySr: null), Is.Null);
            Assert.That(EzDanLabels.TryResolveFallbackDan(8, EzDanSide.Rc, 20, EzMinaSkillAxis.Stream, xxySr: -1), Is.Null);
        }

        [Test]
        public void TablelessChartDanUsesTheBorrowedTable()
        {
            var verdict = EzChartDanEstimator.FromMsd(msd(20), keyCount: 5, holdRatio: 0.1, xxySr: 6);
            Assert.That(EzSunnyDanIntervals.TryLookup(4, DanSkillSystem.SIDE_RC, 6.0, out var fourK), Is.True);

            Assert.That(verdict, Is.Not.Null);
            Assert.That(verdict!.Label, Is.EqualTo(fourK.DisplayLabel));
            Assert.That(verdict.RawDan, Is.EqualTo(fourK.RawDan).Within(0.0001));
        }

        [Test]
        public void TablelessChartDanWithoutStarRatingIsNotInvented()
        {
            Assert.That(EzChartDanEstimator.FromMsd(msd(20), keyCount: 5, holdRatio: 0.1), Is.Null);
            Assert.That(EzChartDanEstimator.FromMsd(msd(20), keyCount: 8, holdRatio: 0.1, xxySr: -1), Is.Null);
        }

        [Test]
        public void PersistedChartDanOtherHalfBorrowsTheTable()
        {
            var computed = EzPersistedChartDan.TryComputeFromStored(
                "tableless-8k",
                System.Guid.NewGuid(),
                msd(20),
                keyCount: 8,
                holdRatio: 0.1,
                xxySr: 6,
                chartInfo: null);
            Assert.That(EzSunnyDanIntervals.TryLookup(4, DanSkillSystem.SIDE_RC, 6.0, out var fourK), Is.True);

            Assert.That(computed, Is.Not.Null);
            Assert.That(computed!.HasSide(EzDanSide.Rc), Is.True);
            Assert.That(computed.RcLabel, Is.EqualTo(fourK.DisplayLabel));
            Assert.That(computed.RcRawDan, Is.EqualTo(fourK.RawDan).Within(0.0001));
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
