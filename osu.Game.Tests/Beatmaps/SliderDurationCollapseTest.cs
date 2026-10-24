// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Tests.Beatmaps
{
    /// <summary>
    /// Sliders ending past <see cref="Slider.EndTimeLimit"/> collapse to zero length. Aspire maps carry such lengths
    /// deliberately; keeping them would leave the object alive for days and make an autoplay replay enormous.
    /// </summary>
    [TestFixture]
    public class SliderDurationCollapseTest
    {
        private const double beat_length = 500;
        private const double slider_multiplier = 1.4f;

        /// <summary>100 * SliderMultiplier / BeatLength.</summary>
        private const double velocity = 100 * slider_multiplier / beat_length;

        [Test]
        public void TestEndTimeBeyondLimitCollapses()
        {
            var slider = createSlider(distanceForTotalDuration(int.MaxValue + 1e6), limit: 200_000);

            Assert.That(slider.EndTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.SpanDuration, Is.Zero);
            Assert.That(slider.NestedHitObjects, Has.Count.EqualTo(2));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Empty);
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>(), Is.Empty);
            Assert.That(slider.HeadCircle, Is.Not.Null);
            Assert.That(slider.TailCircle, Is.Not.Null);
            Assert.That(slider.HeadCircle!.StartTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.TailCircle!.StartTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.TailCircle.Position, Is.EqualTo(slider.Position));
            Assert.That(slider.EndPosition, Is.EqualTo(slider.Position));
        }

        [Test]
        public void TestNoLimitKeepsLiteralDuration()
        {
            var slider = createSlider(distanceForTotalDuration(int.MaxValue + 1e6), limit: null);

            Assert.That(slider.EndTime, Is.GreaterThan(slider.StartTime));
            Assert.That(slider.SpanDuration, Is.GreaterThan(int.MaxValue));
        }

        [Test]
        public void TestDurationWithinLimitIsUnchanged()
        {
            const double distance = 300;

            var limited = createSlider(distance, limit: 600_000);
            var unlimited = createSlider(distance, limit: null);

            Assert.That(limited.EndTime, Is.EqualTo(unlimited.EndTime));
            Assert.That(limited.SpanDuration, Is.EqualTo(unlimited.SpanDuration));
            Assert.That(limited.NestedHitObjects.Count, Is.EqualTo(unlimited.NestedHitObjects.Count));
            Assert.That(limited.EndPosition, Is.EqualTo(unlimited.EndPosition));
            Assert.That(limited.NestedHitObjects.OfType<SliderTick>(), Is.Not.Empty);
        }

        [Test]
        public void TestRepeatsStillCollapsePastLimit()
        {
            var slider = createSlider(distanceForTotalDuration((double)int.MaxValue / 4 + 1e6), repeatCount: 3, limit: 200_000);

            Assert.That(slider.EndTime, Is.EqualTo(slider.StartTime));
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>(), Is.Empty);
            Assert.That(slider.NestedHitObjects, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestAspireModLimitsEndTimeToTheMap()
        {
            var beyondMap = createSlider(distanceForTotalDuration(10_000_000), limit: null);
            beyondMap.StartTime = 1_000;

            var normal = createSlider(300, limit: null);
            normal.StartTime = 5_000;

            var beatmap = new OsuBeatmap { HitObjects = { beyondMap, normal } };

            new OsuModAspireAutoplay().ApplyToBeatmap(beatmap);

            double expectedLimit = 5_000 + OsuModAspireAutoplay.END_TIME_MARGIN;

            Assert.That(beyondMap.EndTimeLimit, Is.EqualTo(expectedLimit));
            Assert.That(normal.EndTimeLimit, Is.EqualTo(expectedLimit));
        }

        [Test]
        public void TestStrictTrackingKeepsLimit()
        {
            var slider = createSlider(300, limit: 600_000);
            var beatmap = new OsuBeatmap { HitObjects = { slider } };

            new OsuModStrictTracking().ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            Assert.That(beatmap.HitObjects[0], Is.Not.SameAs(slider));
            Assert.That(((Slider)beatmap.HitObjects[0]).EndTimeLimit, Is.EqualTo(600_000));
        }

        [Test]
        public void TestAspireModIsAutomationAutoplay()
        {
            var mod = new OsuModAspireAutoplay();

            Assert.That(mod.Acronym, Is.EqualTo("AA"));
            Assert.That(mod.Type, Is.EqualTo(global::osu.Game.Rulesets.Mods.ModType.Automation));
            Assert.That(mod.UserPlayable, Is.False);
            Assert.That(new OsuRuleset().GetModsFor(global::osu.Game.Rulesets.Mods.ModType.Automation).OfType<OsuModAspireAutoplay>(), Is.Not.Empty);
        }

        /// <summary>
        /// The author-set length that yields <paramref name="totalDuration"/> milliseconds over a single span.
        /// </summary>
        private static double distanceForTotalDuration(double totalDuration) => totalDuration * velocity;

        private static Slider createSlider(double expectedDistance, int repeatCount = 0, double? limit = 600_000)
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
                EndTimeLimit = limit,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, expectedDistance),
            };

            slider.ApplyDefaults(controlPoints, difficulty);

            return slider;
        }
    }
}
