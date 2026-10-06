// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Catch.EzCatch.ReplayJudge.Judgement;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;

namespace osu.Game.Rulesets.Catch.Tests.EzCatch.ReplayJudge
{
    [TestFixture]
    public class CatchPlateJudgementTest
    {
        [Test]
        public void IsInPlateMatchesHalfWidthBounds()
        {
            Assert.That(CatchPlateJudgement.IsInPlate(100, 100, 50), Is.True);
            Assert.That(CatchPlateJudgement.IsInPlate(150, 100, 50), Is.True);
            Assert.That(CatchPlateJudgement.IsInPlate(50, 100, 50), Is.True);
            Assert.That(CatchPlateJudgement.IsInPlate(151, 100, 50), Is.False);
            Assert.That(CatchPlateJudgement.IsInPlate(49, 100, 50), Is.False);
        }

        [Test]
        public void IsCaughtInWindowUsesStartTimeAndOneMsGrace()
        {
            var fruit = new Fruit { StartTime = 1000, X = 200 };
            fruit.OriginalXBindable.Value = 200;

            var frames = new[]
            {
                new CatchReplayFrame(999, 0),
                new CatchReplayFrame(1000, 200),
            };

            Assert.That(CatchPlateJudgement.IsCaughtInWindow(fruit, frames, halfCatchWidth: 50, earlyMs: 0, lateMs: 0), Is.True);
        }
    }
}
