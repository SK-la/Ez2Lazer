// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    [TestFixture]
    public class OsuReplaySessionServiceParityTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public async Task TestRunAsyncTotalScoreMatchesSessionRun()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var sessionResult = OsuReplaySession.Run(score.DeepClone(), beatmap, environment);
            var serviceResult = await new OsuReplaySessionService().RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.That(serviceResult.ScoreInfo.TotalScore, Is.EqualTo(sessionResult.ScoreInfo.TotalScore));
        }

        [Test]
        public async Task TestRunTimelineAsyncMatchesSessionRunTimeline()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var sessionTimeline = OsuReplaySession.RunTimeline(score.DeepClone(), beatmap, environment);
            var serviceTimeline = await new OsuReplaySessionService().RunTimelineAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.That(serviceTimeline.FinalTotalScore, Is.EqualTo(sessionTimeline.FinalTotalScore));
        }

        [Test]
        public async Task TestRunRequestAsyncMatchesRunWithTimeline()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();
            var service = new OsuReplaySessionService();

            var (directScore, directTimeline) = OsuReplaySession.RunWithTimeline(score.DeepClone(), beatmap, environment);

            var requestResult = await service.RunRequestAsync(new ReplayRunRequest(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored)).ConfigureAwait(true);

            Assert.That(requestResult.Score.ScoreInfo.TotalScore, Is.EqualTo(directScore.ScoreInfo.TotalScore));
            Assert.That(requestResult.Timeline!.FinalTotalScore, Is.EqualTo(directTimeline.FinalTotalScore));
        }

        [Test]
        public async Task TestServiceCacheReusesSingleRun()
        {
            var (score, beatmap, _) = OsuReplayFixtures.CreateTwoCircleTap();
            var service = new OsuReplaySessionService();

            var first = await service.RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);
            var second = await service.RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.That(second.ScoreInfo.TotalScore, Is.EqualTo(first.ScoreInfo.TotalScore));
            Assert.That(second.ScoreInfo.HitEvents.Count, Is.EqualTo(first.ScoreInfo.HitEvents.Count));
        }

        /// <summary>
        /// StatisticsPanel 契约：RunHitEventsAsync 只产出 HitEvents，不得写脏调用方 ScoreInfo；
        /// HitEvents 聚合须对照独立 RunAsync 的 Session Statistics（两端载体）。
        /// </summary>
        [Test]
        public async Task TestRunHitEventsAsyncDoesNotMutateCallerAndMatchesSessionStatistics()
        {
            var (score, beatmap, _) = OsuReplayFixtures.CreateTwoCircleTap();
            score.ScoreInfo.Accuracy = 0.42;
            score.ScoreInfo.TotalScore = 12345;
            score.ScoreInfo.Statistics[HitResult.Great] = 7;
            score.ScoreInfo.Statistics[HitResult.Miss] = 3;

            ScoreInfo frozen = score.ScoreInfo.DeepClone();
            var service = new OsuReplaySessionService();

            var hitEvents = await service.RunHitEventsAsync(score, beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);
            Score session = await service.RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.Multiple(() =>
            {
                Assert.That(score.ScoreInfo.Accuracy, Is.EqualTo(frozen.Accuracy));
                Assert.That(score.ScoreInfo.TotalScore, Is.EqualTo(frozen.TotalScore));
                Assert.That(score.ScoreInfo.Statistics[HitResult.Great], Is.EqualTo(7));
                Assert.That(score.ScoreInfo.Statistics[HitResult.Miss], Is.EqualTo(3));
            });

            var fromEvents = hitEvents.GroupBy(e => e.Result).ToDictionary(g => g.Key, g => g.Count());
            var sessionNonZero = session.ScoreInfo.Statistics.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key)
                                        .Select(kv => $"{kv.Key}={kv.Value}").ToArray();
            var eventsNonZero = fromEvents.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key)
                                          .Select(kv => $"{kv.Key}={kv.Value}").ToArray();

            Assert.That(eventsNonZero, Is.EqualTo(sessionNonZero));
        }
    }
}
