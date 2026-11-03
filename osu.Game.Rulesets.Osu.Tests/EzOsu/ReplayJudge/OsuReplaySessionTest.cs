// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.EzOsu.Statistics;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    [TestFixture]
    public class OsuReplaySessionTest
    {
        [Test]
        public void TestRunHitEventsProducesHits()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var hitEvents = OsuReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(hitEvents.Count, Is.EqualTo(2));
            Assert.That(hitEvents.Single(e => e.HitObject.StartTime == 1000).Result.IsHit(), Is.True);
            Assert.That(hitEvents.Single(e => e.HitObject.StartTime == 2000).Result.IsHit(), Is.True);
        }

        [Test]
        public void TestRunTimelineFinalScoreMatchesSessionRunTotal()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            long sessionTotal = OsuReplaySession.Run(score, beatmap, environment).ScoreInfo.TotalScore;
            var timeline = OsuReplaySession.RunTimeline(score, beatmap, environment);

            Assert.That(sessionTotal, Is.GreaterThan(0));
            Assert.That(timeline.FinalTotalScore, Is.EqualTo(sessionTotal));
            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(2500).TotalScore, Is.EqualTo(timeline.FinalTotalScore));
        }

        [Test]
        public void TestRunWithTimelineMatchesSeparateRunAndTimeline()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            long separateTotal = OsuReplaySession.Run(score.DeepClone(), beatmap, environment).ScoreInfo.TotalScore;
            var separateTimeline = OsuReplaySession.RunTimeline(score.DeepClone(), beatmap, environment);
            var (combinedScore, combinedTimeline) = OsuReplaySession.RunWithTimeline(score.DeepClone(), beatmap, environment);

            Assert.That(combinedScore.ScoreInfo.TotalScore, Is.EqualTo(separateTotal));
            Assert.That(combinedTimeline.FinalTotalScore, Is.EqualTo(separateTimeline.FinalTotalScore));
        }

        [Test]
        public void TestRunHitEventsMatchesRunScoreInfoHitEvents()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var sessionEvents = OsuReplaySession.RunHitEvents(score, beatmap, environment);
            var runEvents = OsuReplaySession.Run(score.DeepClone(), beatmap, environment).ScoreInfo.HitEvents;

            Assert.That(runEvents.Count, Is.EqualTo(sessionEvents.Count));
            Assert.That(runEvents.Select(e => e.Result), Is.EqualTo(sessionEvents.Select(e => e.Result)));
        }

        [Test]
        public void TestGeneratorGenerateMatchesSessionRunHitEvents()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var generatorEvents = OsuScoreHitEventGenerator.Instance.Generate(score, beatmap);
            var sessionEvents = OsuReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(generatorEvents.Count, Is.EqualTo(sessionEvents.Count));

            for (int i = 0; i < generatorEvents.Count; i++)
            {
                Assert.That(generatorEvents[i].Result, Is.EqualTo(sessionEvents[i].Result));
                Assert.That(generatorEvents[i].TimeOffset, Is.EqualTo(sessionEvents[i].TimeOffset).Within(0.001));
                Assert.That(generatorEvents[i].HitObject.StartTime, Is.EqualTo(sessionEvents[i].HitObject.StartTime));
            }
        }
    }
}
