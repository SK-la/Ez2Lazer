// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    [TestFixture]
    public class OsuShadowSpinnerSessionTest
    {
        [Test]
        public void TestSingleSpinAwardsSpinnerTick()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSpinnerSingleSpin();
            var hitEvents = OsuReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(hitEvents.Count(e => e.HitObject is SpinnerTick && e.Result.IsHit()), Is.EqualTo(1));
        }
    }
}
