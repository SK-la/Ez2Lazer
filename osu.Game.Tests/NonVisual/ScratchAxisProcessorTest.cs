// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Input;
using osu.Game.Rulesets.Mania.EzMania.Input;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ScratchAxisProcessorTest
    {
        [Test]
        public void SpinAboveDeadzonePressesAndSetsDirection()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;
            processor.StopThreshold.Value = 5;

            processor.Update(0f);
            Assert.That(processor.IsPressed.Value, Is.False);

            processor.Update(0.1f);
            Assert.That(processor.IsPressed.Value, Is.True);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.Clockwise));
        }

        [Test]
        public void JitterBelowDeadzoneDoesNotPress()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.05;
            processor.StopThreshold.Value = 5;

            processor.Update(0f);
            processor.Update(0.01f);
            processor.Update(-0.01f);
            processor.Update(0.02f);

            Assert.That(processor.IsPressed.Value, Is.False);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.None));
        }

        [Test]
        public void StopAfterIdleFramesReleases()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;
            processor.StopThreshold.Value = 3;

            processor.Update(0f);
            processor.Update(0.1f);
            Assert.That(processor.IsPressed.Value, Is.True);

            processor.Update(0.1f);
            processor.Update(0.1f);
            processor.Update(0.1f);
            Assert.That(processor.IsPressed.Value, Is.True);

            processor.Update(0.1f);
            Assert.That(processor.IsPressed.Value, Is.False);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.None));
        }

        [Test]
        public void WrapAroundUsesShortestArc()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;

            processor.Update(0.95f);
            processor.Update(-0.95f);

            Assert.That(processor.IsPressed.Value, Is.True);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.Clockwise));
        }

        [Test]
        public void CounterClockwiseDirection()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;

            processor.Update(0f);
            processor.Update(-0.1f);

            Assert.That(processor.IsPressed.Value, Is.True);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.CounterClockwise));
        }

        [TestCase(12, false, 0, 11)]
        [TestCase(14, false, 0, 13)]
        [TestCase(14, true, 0, 12)]
        [TestCase(16, false, 0, 15)]
        public void ManiaTemplateResolvesColumns(int variant, bool skipEmpty, int left, int right)
        {
            Assert.That(ManiaScratchColumnTemplate.TryResolve(variant, skipEmpty, out int l, out int r), Is.True);
            Assert.That(l, Is.EqualTo(left));
            Assert.That(r, Is.EqualTo(right));
        }

        [Test]
        public void ManiaTemplateRejectsUnsupportedVariants()
        {
            Assert.That(ManiaScratchColumnTemplate.TryResolve(8, false, out _, out _), Is.False);
            Assert.That(ManiaScratchColumnTemplate.TryResolve(10, true, out _, out _), Is.False);
        }
    }
}
