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
        public void RestAtNonZeroPositionIsIdle()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.04;
            processor.StopThresholdMs.Value = 50;

            // 停靠在 60%，不是按下
            processor.Update(0.6f, 0);
            Assert.That(processor.IsPressed.Value, Is.False);

            for (int t = 1; t <= 200; t++)
            {
                processor.Update(0.6f, t);
                Assert.That(processor.IsPressed.Value, Is.False, $"t={t}");
            }
        }

        [Test]
        public void SpinAboveDeadzonePressesAndSetsDirection()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;
            processor.StopThresholdMs.Value = 50;

            processor.Update(0f, 0);
            Assert.That(processor.IsPressed.Value, Is.False);

            processor.Update(0.1f, 16);
            Assert.That(processor.IsPressed.Value, Is.True);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.Clockwise));
        }

        [Test]
        public void JitterBelowDeadzoneDoesNotPress()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.05;
            processor.StopThresholdMs.Value = 50;

            processor.Update(0.5f, 0);
            processor.Update(0.51f, 16);
            processor.Update(0.49f, 32);
            processor.Update(0.52f, 48);

            Assert.That(processor.IsPressed.Value, Is.False);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.None));
        }

        [Test]
        public void StopAfterIdleMsReleases()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;
            processor.StopThresholdMs.Value = 50;

            processor.Update(0f, 0);
            processor.Update(0.1f, 10);
            Assert.That(processor.IsPressed.Value, Is.True);

            processor.Update(0.1f, 40);
            Assert.That(processor.IsPressed.Value, Is.True);

            processor.Update(0.1f, 70);
            Assert.That(processor.IsPressed.Value, Is.False);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.None));
        }

        [Test]
        public void MissingSampleDoesNotTreatAsZero()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;
            processor.StopThresholdMs.Value = 50;

            processor.Update(0.7f, 0);
            processor.UpdateMissing(100);
            Assert.That(processor.IsPressed.Value, Is.False);

            // 再次出现同一停靠点，不应因「相对 0」而按下
            processor.Update(0.7f, 116);
            Assert.That(processor.IsPressed.Value, Is.False);
        }

        [Test]
        public void WrapAroundUsesShortestArc()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;

            processor.Update(0.95f, 0);
            processor.Update(-0.95f, 16);

            Assert.That(processor.IsPressed.Value, Is.True);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.Clockwise));
        }

        [Test]
        public void CounterClockwiseDirection()
        {
            var processor = new ScratchAxisProcessor();
            processor.Deadzone.Value = 0.02;

            processor.Update(0f, 0);
            processor.Update(-0.1f, 16);

            Assert.That(processor.IsPressed.Value, Is.True);
            Assert.That(processor.Direction.Value, Is.EqualTo(ScratchAxisDirection.CounterClockwise));
        }

        [Test]
        public void BindingRoundTripsGuidAndAxis()
        {
            var binding = new ScratchAxisBinding("abcd-ef", 2, "TT-L");
            Assert.That(binding.ToString(), Is.EqualTo("abcd-ef|2"));

            var parsed = ScratchAxisBinding.Parse(binding.ToString());
            Assert.That(parsed.DeviceGuid, Is.EqualTo("abcd-ef"));
            Assert.That(parsed.AxisIndex, Is.EqualTo(2));
        }

        [Test]
        public void BindingParsesLegacyAxisIndex()
        {
            var parsed = ScratchAxisBinding.Parse("3");
            Assert.That(parsed.DeviceGuid, Is.Empty);
            Assert.That(parsed.AxisIndex, Is.EqualTo(3));
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
