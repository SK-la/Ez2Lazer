// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Tests.Beatmaps
{
    /// <summary>
    /// Sliders whose duration cannot be represented on the timeline collapse to zero length. Aspire maps carry such
    /// lengths deliberately, and keeping them would leave the object alive indefinitely while forcing a path
    /// approximation over hundreds of thousands of control points.
    /// </summary>
    [TestFixture]
    public class SliderDurationCollapseTest
    {
        private const double beat_length = 500;
        private const double slider_multiplier = 1.4f;

        /// <summary>100 * SliderMultiplier / BeatLength.</summary>
        private const double velocity = 100 * slider_multiplier / beat_length;

        [Test]
        public void TestUnrepresentableLengthCollapses()
        {
            var slider = createSlider(distanceForTotalDuration(int.MaxValue + 1e6));

            Assert.That(slider.EndTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.SpanDuration, Is.Zero);
            Assert.That(slider.NestedHitObjects, Has.Count.EqualTo(2));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Empty);
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>(), Is.Empty);
            Assert.That(slider.HeadCircle, Is.Not.Null);
            Assert.That(slider.TailCircle, Is.Not.Null);
            Assert.That(slider.TailCircle!.StartTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.TailCircle.Position, Is.EqualTo(slider.Position));
        }

        [Test]
        public void TestRepresentableLengthKeepsDuration()
        {
            const double distance = 300;

            var slider = createSlider(distance);

            Assert.That(slider.Velocity, Is.EqualTo(velocity).Within(1e-6));
            Assert.That(slider.EndTime, Is.GreaterThan(slider.StartTime));
            // Path lengths are float-based, so the resulting duration carries float precision.
            Assert.That(slider.SpanDuration, Is.EqualTo(distance / velocity).Within(1e-3));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Not.Empty);
            Assert.That(slider.HeadCircle, Is.Not.Null);
            Assert.That(slider.TailCircle, Is.Not.Null);
        }

        [Test]
        public void TestRepeatsStillCollapseWithUnrepresentableLength()
        {
            var slider = createSlider(distanceForTotalDuration((double)int.MaxValue / 4 + 1e6), repeatCount: 3);

            Assert.That(slider.EndTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>(), Is.Empty);
            Assert.That(slider.NestedHitObjects, Has.Count.EqualTo(2));
        }

        /// <summary>
        /// The author-set length that yields <paramref name="totalDuration"/> milliseconds over a single span.
        /// </summary>
        private static double distanceForTotalDuration(double totalDuration) => totalDuration * velocity;

        private static Slider createSlider(double expectedDistance, int repeatCount = 0)
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = beat_length });

            var difficulty = new BeatmapDifficulty
            {
                SliderMultiplier = (float)slider_multiplier,
                SliderTickRate = 1,
            };

            var slider = new Slider
            {
                StartTime = 1000,
                RepeatCount = repeatCount,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, expectedDistance),
            };

            slider.ApplyDefaults(controlPoints, difficulty);

            return slider;
        }
    }
}
