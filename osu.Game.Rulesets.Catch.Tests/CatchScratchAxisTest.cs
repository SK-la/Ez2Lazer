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
