// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.EzOsu.Judgement;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.Judgement
{
    /// <summary>
    /// 内核契约：同一输入序列在不同驱动步长下必须产出逐字段相同的判定序列。
    /// </summary>
    [TestFixture]
    public class OsuJudgementKernelTest
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        [Test]
        public void TestCirclePressJudgedAtInputTime()
        {
            IBeatmap beatmap = createBeatmap(new HitCircle { StartTime = 1000, Position = centre });
            var samples = new ManualSampleSource();

            var events = run(beatmap, samples, new[] { 1005.0 }, new[] { press(1005) });

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(events[0].Result, Is.EqualTo(HitResult.Great));
                Assert.That(events[0].Time, Is.EqualTo(1005));
            });
        }

        [Test]
        public void TestCircleAutoMissUsesDeadlineTime()
        {
            IBeatmap beatmap = createBeatmap(new HitCircle { StartTime = 1000, Position = centre });
            var samples = new ManualSampleSource();

            var fine = run(beatmap, samples, Enumerable.Range(0, 200).Select(i => 900.0 + i * 5), Array.Empty<OsuInputEvent>());
            var coarse = run(beatmap, samples, new[] { 1500.0 }, Array.Empty<OsuInputEvent>());

            Assert.That(describe(fine), Is.EqualTo(describe(coarse)));
            Assert.That(fine.Single().Result, Is.EqualTo(HitResult.Miss));
            Assert.That(fine.Single().Time, Is.LessThan(1500));
        }

        [Test]
        public void TestSliderDeadlinesAreFrameRateIndependent()
        {
            IBeatmap beatmap = createBeatmap(createSlider());
            var samples = new ManualSampleSource();
            var inputs = new[] { press(1000) };

            var fine = run(beatmap, samples, Enumerable.Range(0, 400).Select(i => 950.0 + i * 5), inputs);
            var coarse = run(beatmap, samples, new[] { 1000.0, 2000.0, 3000.0 }, inputs);
            var oneShot = run(beatmap, samples, new[] { 5000.0 }, inputs);

            Assert.That(describe(fine), Is.EqualTo(describe(coarse)));
            Assert.That(describe(fine), Is.EqualTo(describe(oneShot)));
            Assert.That(fine.Select(e => e.Target).Distinct().Count(), Is.GreaterThan(2));
        }

        [Test]
        public void TestSliderTicksAreFrameRateIndependent()
        {
            IBeatmap beatmap = createBeatmap(createLongSlider());
            var slider = (Slider)beatmap.HitObjects[0];

            Assert.That(slider.NestedHitObjects.OfType<SliderTick>().Count(), Is.GreaterThan(0));

            var samples = new ManualSampleSource(slider) { Pressed = true };
            var inputs = new[] { press(1000) };

            var fine = run(beatmap, samples, Enumerable.Range(0, 601).Select(i => 1000.0 + i * 5), inputs);
            // 200ms 采样步长模拟严重掉帧：tick 仍必须在自己的到期时刻结算。
            var coarse = run(beatmap, samples, Enumerable.Range(0, 16).Select(i => 1000.0 + i * 200), inputs);

            Assert.That(describe(fine), Is.EqualTo(describe(coarse)));
            Assert.That(fine.Any(e => e.Target is SliderTick && e.Result.IsHit()), Is.True);
        }

        [Test]
        public void TestRunIsDeterministic()
        {
            IBeatmap beatmap = createBeatmap(createSlider());
            var samples = new ManualSampleSource();

            var first = run(beatmap, samples, Enumerable.Range(0, 400).Select(i => 950.0 + i * 5), new[] { press(1000) });
            var second = run(beatmap, samples, Enumerable.Range(0, 400).Select(i => 950.0 + i * 5), new[] { press(1000) });

            Assert.That(describe(first), Is.EqualTo(describe(second)));
        }

        private static string describe(IReadOnlyList<OsuJudgementEvent> events)
            => string.Join(", ", events.Select(e => $"{e.Target.GetType().Name}@{e.Target.StartTime}:{e.Result}@{e.Time:F3}"));

        private static OsuInputEvent press(double time) => new OsuInputEvent(time, OsuAction.LeftButton, centre, OsuInputKind.Press);

        private static List<OsuJudgementEvent> run(IBeatmap beatmap, IOsuJudgementSampleSource samples, IEnumerable<double> advances, IReadOnlyList<OsuInputEvent> inputs)
        {
            var kernel = new OsuJudgementKernel(beatmap, new OsuJudgementKernelSettings(), samples);
            var events = new List<OsuJudgementEvent>();

            kernel.Judged += e => events.Add(e);

            var times = advances.Concat(inputs.Select(i => i.Time)).Distinct().OrderBy(t => t).ToList();

            foreach (double time in times)
            {
                kernel.AdvanceBefore(time);

                foreach (var input in inputs.Where(i => i.Time == time))
                    kernel.ApplyInput(input);

                kernel.AdvanceTo(time);
            }

            kernel.FinalizeRun();
            return events;
        }

        private static Slider createSlider()
        {
            return new Slider
            {
                StartTime = 1000,
                Position = centre,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    Vector2.Zero,
                    new Vector2(200, 0),
                }),
            };
        }

        private static Slider createLongSlider()
        {
            return new Slider
            {
                StartTime = 1000,
                Position = centre,
                SliderVelocityMultiplier = 0.5f,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    Vector2.Zero,
                    new Vector2(600, 0),
                }),
            };
        }

        private static IBeatmap createBeatmap(params HitObject[] objects)
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = objects.ToList(),
            };

            IBeatmap beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var processor = ruleset.CreateBeatmapProcessor(beatmap);
            processor.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            processor.PostProcess();

            return beatmap;
        }

        private sealed class ManualSampleSource : IOsuJudgementSampleSource
        {
            private readonly Slider? slider;

            public ManualSampleSource(Slider? slider = null)
            {
                this.slider = slider;
            }

            public bool Pressed { get; init; }

            public bool IsRewinding => false;

            public Vector2 PositionAt(double time)
            {
                if (slider == null)
                    return centre;

                double progress = Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1);
                return slider.StackedPosition + slider.CurvePositionAt(progress);
            }

            public bool IsPressedAt(OsuAction action, double time) => Pressed;

            public bool WasPressedAt(OsuAction action, double time) => Pressed;
        }
    }
}
