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
        [TestCase(EzEnumJudgeMode.Lazer)]
        [TestCase(EzEnumJudgeMode.IIDX_HD)]
        [TestCase(EzEnumJudgeMode.O2Jam)]
        public void NoteDeadlineUsesLateMissWindow(EzEnumJudgeMode judgeMode)
        {
            var note = new Note
            {
                StartTime = 1000,
                HitWindows = new ManiaHitWindows(judgeMode),
            };
            note.HitWindows.SetDifficulty(8);

            var drawable = new DrawableNote();
            drawable.Apply(note);

            var windows = (ManiaHitWindows)note.HitWindows;
            Assert.That(
                ManiaLaneController.GetAutoMissEvaluationTime(drawable),
                Is.EqualTo(note.StartTime + windows.WindowFor(HitResult.Miss, false)));
        }

        [Test]
        public void FutureDeadlineIsNotVisited()
        {
            var note = new Note
            {
                StartTime = 10_000,
                HitWindows = new ManiaHitWindows(EzEnumJudgeMode.Lazer),
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
