// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaAutoMissDeadlineTest
    {
        [TestCase(EzEnumHitMode.Lazer)]
        [TestCase(EzEnumHitMode.IIDX_HD)]
        [TestCase(EzEnumHitMode.O2Jam)]
        public void NoteDeadlineUsesLateMissWindow(EzEnumHitMode hitMode)
        {
            var note = new Note
            {
                StartTime = 1000,
                HitWindows = new ManiaHitWindows(hitMode),
            };
            note.HitWindows.SetDifficulty(8);

            var drawable = new DrawableNote();
            drawable.Apply(note);

            var windows = (ManiaHitWindows)note.HitWindows;
            Assert.That(
                ManiaLaneController.GetAutoMissEvaluationTime(drawable),
                Is.EqualTo(note.StartTime + windows.WindowFor(HitResult.Miss, false)));
        }

        [TestCase(EzEnumHitMode.Lazer)]
        [TestCase(EzEnumHitMode.Classic)]
        [TestCase(EzEnumHitMode.O2Jam)]
        public void HoldTailIsJudgedLaterThanItsQueueDeadline(EzEnumHitMode hitMode)
        {
            var tail = new TailNote
            {
                StartTime = 2000,
                HitWindows = new ManiaHitWindows(hitMode),
            };
            tail.HitWindows.SetDifficulty(8);

            var drawable = new DrawableHoldNoteTail();
            drawable.Apply(tail);

            var windows = (ManiaHitWindows)tail.HitWindows;

            // 列级队列在 EndTime + MissLateWindow 就认为尾判到期，但那只是「开始每帧轮询」。
            // DrawableHoldNoteTail.CheckForResult 会把 offset 除以 RELEASE_WINDOW_LENIENCE，
            // 所以真正落判要等到 EndTime + 1.5×miss —— 比队列到期时刻更晚。
            double queueDeadline = ManiaLaneController.GetAutoMissEvaluationTime(drawable);

            Assert.That(queueDeadline, Is.EqualTo(tail.StartTime + windows.WindowFor(HitResult.Miss, false)));

            double actualJudgementTime = tail.StartTime + tail.MaximumJudgementOffset;

            Assert.That(
                actualJudgementTime,
                Is.GreaterThan(queueDeadline),
                "尾判实际落判晚于队列到期；因此 HasCompleted 的时钟条件也必须活到 MaximumJudgementOffset，"
                + "而不是只活到 GetLastObjectTime() + 固定常量。");
        }

        [Test]
        public void FutureDeadlineIsNotVisited()
        {
            var note = new Note
            {
                StartTime = 10_000,
                HitWindows = new ManiaHitWindows(EzEnumHitMode.Lazer),
            };
            note.HitWindows.SetDifficulty(8);

            var drawable = new DrawableNote();
            drawable.Apply(note);

            var lane = new ManiaLaneController();
            lane.Register(drawable, scheduleAutoMiss: true);

            Assert.That(lane.ProcessAutoMiss(1000, evaluateResults: false), Is.Zero);
        }
    }
}
