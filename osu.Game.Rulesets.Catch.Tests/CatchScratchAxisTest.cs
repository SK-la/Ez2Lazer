// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Input;

namespace osu.Game.Rulesets.Catch.Tests
{
    [TestFixture]
    public class CatchScratchAxisTest
    {
        [Test]
        public void TestResolveActive_NonePressed_ReturnsNull()
        {
            var left = createProcessor(pressed: false);
            var right = createProcessor(pressed: false);

            Assert.That(CatchScratchAxisResolver.ResolveActive(left, right), Is.Null);
        }

        [Test]
        public void TestResolveActive_OnlyLeft_ReturnsLeft()
        {
            var left = createProcessor(pressed: true, lastMotionTime: 100);
            var right = createProcessor(pressed: false);

            Assert.That(CatchScratchAxisResolver.ResolveActive(left, right), Is.SameAs(left));
        }

        [Test]
        public void TestResolveActive_OnlyRight_ReturnsRight()
        {
            var left = createProcessor(pressed: false);
            var right = createProcessor(pressed: true, lastMotionTime: 100);

            Assert.That(CatchScratchAxisResolver.ResolveActive(left, right), Is.SameAs(right));
        }

        [Test]
        public void TestResolveActive_BothPressed_PicksMostRecentMotion()
        {
            var left = createProcessor(pressed: true, lastMotionTime: 100, direction: ScratchAxisDirection.CounterClockwise);
            var right = createProcessor(pressed: true, lastMotionTime: 200, direction: ScratchAxisDirection.Clockwise);

            Assert.That(CatchScratchAxisResolver.ResolveActive(left, right), Is.SameAs(right));
            Assert.That(CatchScratchAxisResolver.ResolveActive(
                createProcessor(pressed: true, lastMotionTime: 300),
                right), Is.Not.SameAs(right));
        }

        [Test]
        public void TestJudgmentWindow_AssistExpandsTiming()
        {
            Assert.That(CatchScratchJudgmentWindow.ShouldBeginChecking(-10, assist: true), Is.True);
            Assert.That(CatchScratchJudgmentWindow.ShouldBeginChecking(-11, assist: true), Is.False);

            Assert.That(CatchScratchJudgmentWindow.ShouldApplyMiss(20, assist: true), Is.False);
            Assert.That(CatchScratchJudgmentWindow.ShouldApplyMiss(21, assist: true), Is.True);
        }

        [Test]
        public void TestJudgmentWindow_WithoutAssistMatchesLegacyTiming()
        {
            Assert.That(CatchScratchJudgmentWindow.ShouldBeginChecking(-1, assist: false), Is.False);
            Assert.That(CatchScratchJudgmentWindow.ShouldBeginChecking(0, assist: false), Is.True);

            Assert.That(CatchScratchJudgmentWindow.ShouldApplyMiss(0, assist: false), Is.False);
            Assert.That(CatchScratchJudgmentWindow.ShouldApplyMiss(1, assist: false), Is.True);
        }

        [Test]
        public void TestEz2Activation_SingleTickWithHalfDeadzone()
        {
            var processor = new ScratchAxisProcessor
            {
                Deadzone = { Value = 0.01 },
                DeadzoneMultiplier = { Value = 0.5 },
                RequiredActivationTicks = { Value = 1 },
            };

            processor.Update(0, 0);
            processor.Update(0.006f, 1);

            Assert.That(processor.IsPressed.Value, Is.True);
        }

        [Test]
        public void TestDefaultActivation_RequiresTwoTicks()
        {
            var processor = new ScratchAxisProcessor
            {
                Deadzone = { Value = 0.01 },
                RequiredActivationTicks = { Value = 2 },
            };

            processor.Update(0, 0);
            processor.Update(0.02f, 1);
            Assert.That(processor.IsPressed.Value, Is.False);

            processor.Update(0.04f, 2);
            Assert.That(processor.IsPressed.Value, Is.True);
        }

        [Test]
        public void TestSpeedMapper_ScalesUpToOnePointFive()
        {
            Assert.That(CatchScratchSpeedMapper.Map(0), Is.EqualTo(1));

            Assert.That(
                CatchScratchSpeedMapper.Map(CatchScratchSpeedMapper.ReferenceVelocity * 0.5),
                Is.EqualTo(1.25).Within(0.001));

            Assert.That(
                CatchScratchSpeedMapper.Map(CatchScratchSpeedMapper.ReferenceVelocity),
                Is.EqualTo(1.5).Within(0.001));

            Assert.That(
                CatchScratchSpeedMapper.Map(CatchScratchSpeedMapper.ReferenceVelocity * 3),
                Is.EqualTo(1.5).Within(0.001));
        }

        [Test]
        public void TestSpeedMapper_FastSpinVelocity_IsClearlyAboveWalkSpeed()
        {
            double multiplier = CatchScratchSpeedMapper.Map(0.001);
            Assert.That(multiplier, Is.GreaterThan(1.2));
        }

        [Test]
        public void TestAngularVelocity_IsFrameRateIndependent()
        {
            var lowFps = new ScratchAxisProcessor();
            lowFps.Update(0, 0);
            lowFps.Update(0.02f, 16);

            var highFps = new ScratchAxisProcessor();
            highFps.Update(0, 0);

            for (int i = 1; i <= 16; i++)
                highFps.Update(i * 0.02f / 16, i);

            Assert.That(highFps.AngularVelocity, Is.EqualTo(lowFps.AngularVelocity).Within(0.0001));
        }

        [Test]
        public void TestSmoothedAngularVelocity_DecaysWithWallClock()
        {
            var processor = new ScratchAxisProcessor
            {
                RequiredActivationTicks = { Value = 1 },
            };

            processor.Update(0, 0);
            processor.Update(0.02f, 10);
            processor.Update(0.04f, 20);

            double peak = processor.SmoothedAngularVelocity;
            Assert.That(peak, Is.EqualTo(0.002).Within(0.0001));

            processor.Update(0.0401f, 45);
            Assert.That(processor.SmoothedAngularVelocity, Is.LessThan(peak));
            Assert.That(processor.SmoothedAngularVelocity, Is.GreaterThan(0));
        }

        [Test]
        public void TestAngularVelocity_ComputedFromMotionInterval()
        {
            var processor = new ScratchAxisProcessor();

            processor.Update(0, 0);
            processor.Update(0.01f, 5);
            processor.Update(0.02f, 10);

            Assert.That(processor.AngularVelocity, Is.EqualTo(0.01 / 5).Within(0.0001));
        }

        private static ScratchAxisProcessor createProcessor(
            bool pressed,
            double lastMotionTime = 100,
            ScratchAxisDirection direction = ScratchAxisDirection.Clockwise)
        {
            var processor = new ScratchAxisProcessor();

            if (!pressed)
                return processor;

            float step = direction == ScratchAxisDirection.Clockwise ? 0.02f : -0.02f;

            processor.Update(0, lastMotionTime - 2);
            processor.Update(step * 0.5f, lastMotionTime - 1);
            processor.Update(step, lastMotionTime);

            return processor;
        }
    }
}
