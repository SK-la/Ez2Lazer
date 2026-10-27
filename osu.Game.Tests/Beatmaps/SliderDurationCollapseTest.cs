// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
    /// <see cref="Slider.LimitDurationToGeometry"/> 让滑条只持续绘制路径那么长，作者设定长度只能截短。
    /// osu!stable 从不延伸路径；aspire 谱会声明否则把几单位滑条变成持续数天的长度。
    /// </summary>
    [TestFixture]
    public class SliderDurationCollapseTest
    {
        private const double beat_length = 500;
        private const double slider_multiplier = 1.4f;

        /// <summary>100 * SliderMultiplier / BeatLength.</summary>
        private const double velocity = 100 * slider_multiplier / beat_length;

        [Test]
        public void TestDeclaredLengthDoesNotExtendDuration()
        {
            // 几何跨度 100 单位，作者声称的长度会持续数天。
            var slider = createSlider(geometryDistance: 100, declaredDistance: 2_000_000_000, limitToGeometry: true);

            Assert.That(slider.Path.Distance, Is.GreaterThan(1_000_000));
            Assert.That(slider.Path.CalculatedDistance, Is.EqualTo(100).Within(0.01));
            Assert.That(slider.Duration, Is.EqualTo(100 / velocity).Within(0.01));
        }

        [Test]
        public void TestDeclaredLengthStillShortens()
        {
            var slider = createSlider(geometryDistance: 200, declaredDistance: 50, limitToGeometry: true);

            Assert.That(slider.Path.Distance, Is.EqualTo(50).Within(0.01));
            Assert.That(slider.Duration, Is.EqualTo(50 / velocity).Within(0.01));
        }

        [Test]
        public void TestWithoutTheFlagDurationIsUnchanged()
        {
            const double declared = 2_000_000_000;

            var slider = createSlider(geometryDistance: 100, declaredDistance: declared, limitToGeometry: false);

            Assert.That(slider.Path.Distance, Is.EqualTo(slider.Path.CalculatedDistance).Within(0.01).Or.EqualTo(declared).Within(declared * 0.01));
            Assert.That(slider.Duration, Is.GreaterThan(1_000_000));
        }

        [Test]
        public void TestTicksUseTheEffectiveDistance()
        {
            // 几何短、声明巨大时不得生成数天份量的 tick。
            var slider = createSlider(geometryDistance: 200, declaredDistance: 2_000_000_000, limitToGeometry: true);

            Assert.That(slider.Duration, Is.EqualTo(200 / velocity).Within(0.01));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>().Count(), Is.LessThan(10));
        }

        [Test]
        public void TestRepeatsScaleTheEffectiveDistance()
        {
            var slider = createSlider(geometryDistance: 100, declaredDistance: 2_000_000_000, limitToGeometry: true, repeatCount: 3);

            Assert.That(slider.Duration, Is.EqualTo(3 * 100 / velocity).Within(0.01));
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>(), Has.Count.EqualTo(3));
        }

        [Test]
        public void TestAspireModAppliesTheFlag()
        {
            var huge = createSlider(geometryDistance: 100, declaredDistance: 2_000_000_000, limitToGeometry: false);
            var normal = createSlider(geometryDistance: 100, declaredDistance: 300, limitToGeometry: false);

            var beatmap = new OsuBeatmap { HitObjects = { huge, normal } };

            new OsuModAspire().ApplyToBeatmap(beatmap);

            Assert.That(huge.LimitDurationToGeometry, Is.True);
            Assert.That(normal.LimitDurationToGeometry, Is.True);
        }

        [Test]
        public void TestAspireModDoesNotChangeRepeatCount()
        {
            var slider = createSlider(geometryDistance: 100, declaredDistance: 300, limitToGeometry: false, repeatCount: 9000);
            var beatmap = new OsuBeatmap { HitObjects = { slider } };

            new OsuModAspire().ApplyToBeatmap(beatmap);

            Assert.That(slider.RepeatCount, Is.EqualTo(9000));
        }

        [Test]
        public void TestStrictTrackingKeepsTheFlag()
        {
            var slider = createSlider(geometryDistance: 100, declaredDistance: 300, limitToGeometry: true);
            var beatmap = new OsuBeatmap { HitObjects = { slider } };

            new OsuModStrictTracking().ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            Assert.That(beatmap.HitObjects[0], Is.Not.SameAs(slider));
            Assert.That(((Slider)beatmap.HitObjects[0]).LimitDurationToGeometry, Is.True);
        }

        [Test]
        public void TestAspireModIsPlayableAndTogglesAutoplay()
        {
            var mod = new OsuModAspire();

            Assert.That(mod.Acronym, Is.EqualTo("ASP"));
            Assert.That(mod.Type, Is.EqualTo(global::osu.Game.Rulesets.Mods.ModType.Automation));
            Assert.That(mod.UserPlayable, Is.True);
            Assert.That(new OsuRuleset().GetModsFor(global::osu.Game.Rulesets.Mods.ModType.Automation).OfType<OsuModAspire>(), Is.Not.Empty);
        }

        [Test]
        public void TestAutoplayToggleControlsReplayGeneration()
        {
            var mod = new OsuModAspire();

            Assert.That(mod.Autoplay.Value, Is.True);
            Assert.That(mod.ReplayGenerationEnabled, Is.True);

            mod.Autoplay.Value = false;
            Assert.That(mod.ReplayGenerationEnabled, Is.False);
        }

        [Test]
        public void TestCoincidentSpinnersAreMerged()
        {
            var beatmap = new OsuBeatmap();

            beatmap.HitObjects.Add(createZeroLengthSpinner(1_000));
            beatmap.HitObjects.Add(createZeroLengthSpinner(1_000));
            beatmap.HitObjects.Add(createZeroLengthSpinner(1_000));
            beatmap.HitObjects.Add(createZeroLengthSpinner(2_000));

            var realSpinner = createZeroLengthSpinner(3_000);
            realSpinner.Duration = 500;
            beatmap.HitObjects.Add(realSpinner);

            new OsuModAspire().ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(3));
            Assert.That(beatmap.HitObjects.Count(h => h.StartTime == 1_000), Is.EqualTo(1));
            Assert.That(beatmap.HitObjects.Count(h => h.StartTime == 2_000), Is.EqualTo(1));
            // 有真实时长的转盘即使与另一个同时开始也必须保留。
            Assert.That(beatmap.HitObjects.Count(h => h.StartTime == 3_000), Is.EqualTo(1));
        }

        [Test]
        public void TestDistinctSpinnersAreNotMerged()
        {
            var beatmap = new OsuBeatmap();

            var first = createZeroLengthSpinner(1_000);
            var second = createZeroLengthSpinner(1_000);
            second.Position = new Vector2(100, 100);

            beatmap.HitObjects.Add(first);
            beatmap.HitObjects.Add(second);

            new OsuModAspire().ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestSlidersAreNeverMerged()
        {
            var beatmap = new OsuBeatmap();

            beatmap.HitObjects.Add(createSlider(geometryDistance: 100, declaredDistance: 300, limitToGeometry: false));
            beatmap.HitObjects.Add(createSlider(geometryDistance: 100, declaredDistance: 300, limitToGeometry: false));

            new OsuModAspire().ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestGetPathToProgressMatchesFullRange()
        {
            var path = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(60, 120), new Vector2(180, -40), new Vector2(240, 0) }, 200);
            var range = new List<Vector2>();

            path.GetPathToProgress(range, 0, 1);

            Assert.That(range, Is.Not.Empty);
            Assert.That(range[0].X, Is.EqualTo(path.PositionAt(0).X).Within(0.01f));
            Assert.That(range[^1].X, Is.EqualTo(path.PositionAt(1).X).Within(0.01f));
        }

        private static Spinner createZeroLengthSpinner(double startTime) => new Spinner { StartTime = startTime, Duration = 0 };

        /// <summary>
        /// 构建绘制路径跨 <paramref name="geometryDistance"/> 单位、同时声明 <paramref name="declaredDistance"/> 的滑条。
        /// </summary>
        private static Slider createSlider(double geometryDistance, double declaredDistance, bool limitToGeometry, int repeatCount = 0)
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
                LimitDurationToGeometry = limitToGeometry,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2((float)geometryDistance, 0) }, declaredDistance),
            };

            slider.ApplyDefaults(controlPoints, difficulty);

            return slider;
        }
    }
}
