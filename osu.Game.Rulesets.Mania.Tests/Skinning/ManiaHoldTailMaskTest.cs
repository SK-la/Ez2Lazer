// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Mania.Skinning;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [TestFixture]
    public class ManiaHoldTailMaskTest
    {
        [Test]
        public void TestDefaultLevelIsTwoSixteenthOfBeat()
        {
            const double beat_length = 500;

            Assert.That(ManiaHoldTailMask.GetDuration(4, beat_length), Is.EqualTo(beat_length * 2 / 16).Within(0.001));
            Assert.That(ManiaHoldTailMask.GetDuration(4, beat_length), Is.EqualTo(beat_length / 8).Within(0.001));
        }

        [Test]
        public void TestLevelDividesByThirtyTwo()
        {
            const double beat_length = 480;

            Assert.That(ManiaHoldTailMask.GetDuration(1, beat_length), Is.EqualTo(beat_length / 32).Within(0.001));
            Assert.That(ManiaHoldTailMask.GetDuration(2, beat_length), Is.EqualTo(beat_length / 16).Within(0.001));
            Assert.That(ManiaHoldTailMask.GetDuration(8, beat_length), Is.EqualTo(beat_length / 4).Within(0.001));
        }

        [Test]
        public void TestInvalidInputsReturnZero()
        {
            Assert.That(ManiaHoldTailMask.GetDuration(4, 0), Is.EqualTo(0));
            Assert.That(ManiaHoldTailMask.GetDuration(0, 500), Is.EqualTo(0));
            Assert.That(ManiaHoldTailMask.GetDuration(-1, 500), Is.EqualTo(0));
        }

        [Test]
        public void TestZeroLevelDisablesMaskWork()
        {
            Assert.That(ManiaHoldTailMask.IsEnabled(0), Is.False);
            Assert.That(ManiaHoldTailMask.IsEnabled(4), Is.True);
        }
    }
}
