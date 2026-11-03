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
    public class OsuShadowSliderSessionTest
    {
        [Test]
        public void TestSliderBothKeysTrackingHitsAllNested()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSliderBothKeysTracking();
            var hitEvents = OsuReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(hitEvents.All(e => e.Result.IsHit()), Is.True);
            Assert.That(hitEvents.Any(e => e.HitObject is SliderHeadCircle), Is.True);
            Assert.That(hitEvents.Any(e => e.HitObject is SliderTailCircle), Is.True);
        }
    }
}
