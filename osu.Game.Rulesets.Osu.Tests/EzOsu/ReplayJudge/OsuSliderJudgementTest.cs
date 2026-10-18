// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    /// <summary>
    /// <see cref="OsuSliderJudgement"/> 的纯函数契约：Drawable 与 Session 都只读这里的口径。
    /// </summary>
    [TestFixture]
    public class OsuSliderJudgementTest
    {
        [TestCase(1, 4, HitResult.Meh)]
        [TestCase(2, 4, HitResult.Ok)]
        [TestCase(3, 4, HitResult.Ok)]
        [TestCase(4, 4, HitResult.Great)]
        // 过半即 100，恰好一半也算；不足一半落 50。
        [TestCase(5, 9, HitResult.Ok)]
        [TestCase(4, 9, HitResult.Meh)]
        public void TestBodyResultByHitFraction(int hitNested, int totalNested, HitResult expected)
        {
            Assert.That(
                OsuSliderJudgement.BodyResultFromNestedHits(true, totalNested, hitNested, HitResult.Great, HitResult.Miss),
                Is.EqualTo(expected));
        }

        [Test]
        public void TestBodyResultKeepsLazerThresholdsOutsideClassic()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.BodyResultFromNestedHits(false, 5, 1, HitResult.Great, HitResult.Miss), Is.EqualTo(HitResult.Great));
                Assert.That(OsuSliderJudgement.BodyResultFromNestedHits(false, 5, 0, HitResult.Great, HitResult.Miss), Is.EqualTo(HitResult.Miss));
            });
        }

        [Test]
        public void TestBodyResultCountsHeadAsOneNestedObject()
        {
            Slider slider = createSlider(duration: 1000, repeats: 1, classic: true);
            int total = slider.NestedHitObjects.Count;

            // 分母含滑条头自身，所以「只中头」不会是 300，也不会是 0。
            Assert.That(total, Is.GreaterThan(2));

            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.BodyResultFromNestedObjects(slider, o => o is SliderHeadCircle), Is.EqualTo(HitResult.Meh));
                Assert.That(OsuSliderJudgement.BodyResultFromNestedObjects(slider, _ => true), Is.EqualTo(HitResult.Great));
                Assert.That(OsuSliderJudgement.BodyResultFromNestedObjects(slider, _ => false), Is.EqualTo(HitResult.Miss));
            });
        }

        [Test]
        public void TestTailDueTimeIsThirtySixMillisecondsBeforeEnd()
        {
            Assert.That(OsuSliderJudgement.TailDueTime(1000, 500), Is.EqualTo(1464));
        }

        [Test]
        public void TestTailDueTimeMovesToHalfForShortSliders()
        {
            // 时长 < 72ms 时结束前 36ms 会早于起点，stable 改到半程。
            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.TailDueTime(1000, 60), Is.EqualTo(1030));
                Assert.That(OsuSliderJudgement.TailDueTime(1000, 72), Is.EqualTo(1036));
                Assert.That(OsuSliderJudgement.TailDueTime(1000, 73), Is.EqualTo(1037));
            });
        }

        [Test]
        public void TestNestedDueTimeUsesTailRuleOnlyForTail()
        {
            Slider slider = createSlider(duration: 1000, repeats: 0, classic: false);
            var tail = slider.NestedHitObjects.OfType<SliderTailCircle>().Single();
            var tick = slider.NestedHitObjects.OfType<SliderTick>().FirstOrDefault();

            Assert.That(OsuSliderJudgement.NestedDueTime(tail, slider.StartTime, slider.Duration), Is.EqualTo(OsuSliderJudgement.TailDueTime(slider.StartTime, slider.Duration)));

            if (tick != null)
                Assert.That(OsuSliderJudgement.NestedDueTime(tick, slider.StartTime, slider.Duration), Is.EqualTo(tick.StartTime));
        }

        [Test]
        public void TestTrackingGateRequiresTrackingBeforeDueTime()
        {
            const double due = 1250;

            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.WasTrackingAtDueTime(tickAt(due), null, 1000, 1000), Is.False, "从未跟踪");
                Assert.That(OsuSliderJudgement.WasTrackingAtDueTime(tickAt(due), due - 1, 1000, 1000), Is.True, "到期前开始跟踪");
                Assert.That(OsuSliderJudgement.WasTrackingAtDueTime(tickAt(due), due, 1000, 1000), Is.True, "到期瞬间开始跟踪也算");
                Assert.That(OsuSliderJudgement.WasTrackingAtDueTime(tickAt(due), due + 1, 1000, 1000), Is.False, "过期后重新接上不能补判");
            });
        }

        [Test]
        public void TestIsNestedDueUsesDueTimeNotObjectTime()
        {
            Slider slider = createSlider(duration: 500, repeats: 0, classic: false);
            var tail = slider.NestedHitObjects.OfType<SliderTailCircle>().Single();
            double dueTime = OsuSliderJudgement.TailDueTime(slider.StartTime, slider.Duration);

            Assert.Multiple(() =>
            {
                Assert.That(dueTime, Is.LessThan(tail.StartTime));
                Assert.That(OsuSliderJudgement.IsNestedDue(tail, dueTime - 1, slider.StartTime, slider.Duration), Is.False);
                Assert.That(OsuSliderJudgement.IsNestedDue(tail, dueTime, slider.StartTime, slider.Duration), Is.True);
                Assert.That(OsuSliderJudgement.IsNestedDue(tail, tail.StartTime, slider.StartTime, slider.Duration), Is.True);
            });
        }

        [Test]
        public void TestPrerequisitesOnlyGateTail()
        {
            Slider slider = createSlider(duration: 1000, repeats: 1, classic: false);
            var tail = slider.NestedHitObjects.OfType<SliderTailCircle>().Single();
            var tick = slider.NestedHitObjects.OfType<SliderTick>().FirstOrDefault();

            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.ArePrerequisitesJudged(tail, _ => false, slider.NestedHitObjects), Is.False);
                Assert.That(OsuSliderJudgement.ArePrerequisitesJudged(tail, o => o is SliderTick or SliderRepeat, slider.NestedHitObjects), Is.True);

                if (tick != null)
                    Assert.That(OsuSliderJudgement.ArePrerequisitesJudged(tick, _ => false, slider.NestedHitObjects), Is.True);
            });
        }

        [Test]
        public void TestDeadlineIsDueTimeInClassicButEndTimeOtherwise()
        {
            Slider slider = createSlider(duration: 1000, repeats: 0, classic: false);
            var tail = slider.NestedHitObjects.OfType<SliderTailCircle>().Single();
            double dueTime = OsuSliderJudgement.TailDueTime(slider.StartTime, slider.Duration);

            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.NestedDeadline(tail, true, slider.StartTime, slider.Duration), Is.EqualTo(dueTime), "classic 到期即截止");
                Assert.That(OsuSliderJudgement.NestedDeadline(tail, false, slider.StartTime, slider.Duration), Is.EqualTo(tail.StartTime), "默认轨宽限到滑条结束");
                Assert.That(OsuSliderJudgement.IsNestedSettled(tail, dueTime, true, slider.StartTime, slider.Duration), Is.True);
                Assert.That(OsuSliderJudgement.IsNestedSettled(tail, dueTime, false, slider.StartTime, slider.Duration), Is.False);
            });
        }

        [Test]
        public void TestTickDeadlineIsItsOwnTimeInBothModes()
        {
            const double startTime = 1250;

            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.NestedDeadline(tickAt(startTime), true, 1000, 1000), Is.EqualTo(startTime));
                Assert.That(OsuSliderJudgement.NestedDeadline(tickAt(startTime), false, 1000, 1000), Is.EqualTo(startTime));
            });
        }

        [Test]
        public void TestHeadMissResultFollowsClassicMode()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OsuSliderJudgement.HeadMissResult(true), Is.EqualTo(HitResult.LargeTickMiss));
                Assert.That(OsuSliderJudgement.HeadMissResult(false), Is.EqualTo(HitResult.Miss));
            });
        }

        private static SliderTick tickAt(double startTime) => new SliderTick { StartTime = startTime };

        private static Slider createSlider(double duration, int repeats, bool classic)
        {
            var ruleset = new OsuRuleset();
            var slider = new Slider
            {
                StartTime = 1000,
                Position = new Vector2(256, 192),
                RepeatCount = repeats,
                ClassicSliderBehaviour = classic,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    Vector2.Zero,
                    new Vector2(200, 0),
                }),
            };

            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { slider },
            };

            IBeatmap beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var processor = ruleset.CreateBeatmapProcessor(beatmap);
            processor.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            processor.PostProcess();

            var prepared = (Slider)beatmap.HitObjects[0];
            Assert.That(prepared.Duration, Is.GreaterThan(0));
            return prepared;
        }
    }
}
