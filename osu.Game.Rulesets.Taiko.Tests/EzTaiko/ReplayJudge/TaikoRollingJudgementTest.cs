// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.EzTaiko.ReplayJudge.Judgement;

namespace osu.Game.Rulesets.Taiko.Tests.EzTaiko.ReplayJudge
{
    /// <summary>
    /// TTL-001 深 parity：Strong / DrumRoll tick / Swell 纯函数与 Drawable 语义对齐。
    /// </summary>
    [TestFixture]
    public class TaikoRollingJudgementTest
    {
        [Test]
        public void TickHitUsesAbsWindow()
        {
            Assert.That(TaikoRollingJudgement.IsTickHit(1000, 1000, 30), Is.True);
            Assert.That(TaikoRollingJudgement.IsTickHit(1030, 1000, 30), Is.True);
            Assert.That(TaikoRollingJudgement.IsTickHit(1031, 1000, 30), Is.False);
        }

        [Test]
        public void TickResultMapsHitAndMiss()
        {
            Assert.That(TaikoRollingJudgement.TickResult(true, HitResult.SmallBonus, HitResult.IgnoreMiss), Is.EqualTo(HitResult.SmallBonus));
            Assert.That(TaikoRollingJudgement.TickResult(false, HitResult.SmallBonus, HitResult.IgnoreMiss), Is.EqualTo(HitResult.IgnoreMiss));
        }

        [Test]
        public void SwellMustAlternateAfterFirst()
        {
            Assert.That(TaikoRollingJudgement.IsValidSwellPress(isCentre: true, lastWasCentre: null, mustAlternate: true), Is.True);
            Assert.That(TaikoRollingJudgement.IsValidSwellPress(isCentre: false, lastWasCentre: true, mustAlternate: true), Is.True);
            Assert.That(TaikoRollingJudgement.IsValidSwellPress(isCentre: true, lastWasCentre: true, mustAlternate: true), Is.False);
            Assert.That(TaikoRollingJudgement.IsValidSwellPress(isCentre: true, lastWasCentre: true, mustAlternate: false), Is.True);
        }

        [Test]
        public void SwellBodyAndStrongNestedResults()
        {
            Assert.That(TaikoRollingJudgement.SwellBodyResult(10, 10, HitResult.Great, HitResult.Miss), Is.EqualTo(HitResult.Great));
            Assert.That(TaikoRollingJudgement.SwellBodyResult(9, 10, HitResult.Great, HitResult.Miss), Is.EqualTo(HitResult.Miss));
            Assert.That(TaikoRollingJudgement.StrongNestedResult(true, HitResult.LargeBonus, HitResult.IgnoreMiss), Is.EqualTo(HitResult.LargeBonus));
            Assert.That(TaikoRollingJudgement.StrongNestedResult(false, HitResult.LargeBonus, HitResult.IgnoreMiss), Is.EqualTo(HitResult.IgnoreMiss));
        }

        [Test]
        public void CentreActionDetection()
        {
            Assert.That(TaikoRollingJudgement.IsCentreAction(TaikoAction.LeftCentre), Is.True);
            Assert.That(TaikoRollingJudgement.IsCentreAction(TaikoAction.RightRim), Is.False);
        }
    }
}
