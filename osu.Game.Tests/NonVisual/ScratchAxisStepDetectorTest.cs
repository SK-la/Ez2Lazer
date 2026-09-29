// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Input;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ScratchAxisStepDetectorTest
    {
        private static ScratchAxisStepDetector createDetector(double deadzone, double stepSize) => new ScratchAxisStepDetector
        {
            Deadzone =
            {
                Value = deadzone
            },
            StepSize =
            {
                Value = stepSize
            }
        };

        [Test]
        public void RestAtNonZeroPositionProducesNoSteps()
        {
            var detector = createDetector(0.001, 0.2);

            detector.Update(0.6f, 0);

            for (int i = 1; i <= 100; i++)
                Assert.That(detector.Update(0.6f, i * 16), Is.Zero, $"frame={i}");
        }

        [Test]
        public void StepEmittedAfterAccumulatingStepSize()
        {
            var detector = createDetector(0.001, 0.1);

            Assert.That(detector.Update(0f, 0), Is.Zero);
            Assert.That(detector.Update(0.05f, 16), Is.Zero, "accumulated only half a step");
            Assert.That(detector.Update(0.1f, 32), Is.EqualTo(1), "accumulated a full step");
        }

        [Test]
        public void LargeJumpIsClampedToOneStepAndDoesNotCarryOver()
        {
            var detector = createDetector(0.001, 0.1);

            detector.Update(0f, 0);

            Assert.That(detector.Update(0.5f, 16), Is.EqualTo(1), "same-frame multi-step must collapse to one");
            Assert.That(detector.Update(0.5f, 32), Is.Zero, "overflow must be discarded, not queued");
        }

        [Test]
        public void ReverseRotationEmitsNegativeStep()
        {
            var detector = createDetector(0.001, 0.1);

            detector.Update(0.5f, 0);

            Assert.That(detector.Update(0.35f, 16), Is.EqualTo(-1));
        }

        [Test]
        public void SubDeadzoneJitterProducesNoSteps()
        {
            var detector = createDetector(0.001, 0.05);

            detector.Update(0f, 0);

            for (int i = 1; i <= 100; i++)
                Assert.That(detector.Update(i % 2 == 0 ? 0.0005f : 0f, i * 16), Is.Zero, $"frame={i}");
        }

        [Test]
        public void WrapAroundCountsAsForwardRotation()
        {
            var detector = createDetector(0.001, 0.05);

            detector.Update(0.95f, 0);

            // 0.95 -> -0.95 在 [-1,1] 环上是最短弧 +0.1（前进），不是大步后退。
            Assert.That(detector.Update(-0.95f, 16), Is.EqualTo(1));
        }

        [Test]
        public void SlowSpinStillAccumulates()
        {
            // 每帧位移低于 Mania/Catch 的打击死区（默认 0.005）也必须能累积出格，
            // 否则慢速转动会完全不响应。
            var detector = createDetector(0.001, 0.05);

            detector.Update(0f, 0);

            int steps = 0;

            for (int i = 1; i <= 60; i++)
                steps += detector.Update(i * 0.004f, i * 16);

            Assert.That(steps, Is.EqualTo(4), "0.24 total travel / 0.05 per step");
        }

        [Test]
        public void IdleDiscardsPendingOverflow()
        {
            var detector = createDetector(0.001, 0.1);

            detector.Update(0f, 0);

            Assert.That(detector.Update(0.15f, 16), Is.EqualTo(1));

            // 停转超过 IdleResetMs 后欠账被丢弃，松手不会继续跳格。
            Assert.That(detector.Update(0.15f, 1000), Is.Zero);
        }

        [Test]
        public void ResetDiscardsAccumulationAndResamples()
        {
            var detector = createDetector(0.001, 0.05);

            detector.Update(0f, 0);
            Assert.That(detector.Update(0.06f, 16), Is.EqualTo(1));

            detector.Reset();

            Assert.That(detector.Update(10f, 32), Is.Zero, "first sample after reset never emits");
            Assert.That(detector.Update(10f, 48), Is.Zero);
        }
    }
}
