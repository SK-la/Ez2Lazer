// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 验证 <see cref="ManiaReplaySessionService.RunAsync"/> 与直接调用
    /// <see cref="ManiaReplaySession.RunHitEvents"/> 的结果一致性。
    /// P2-B 验收测试。
    /// </summary>
    [TestFixture]
    public class ManiaReplaySessionServiceParityTest
    {
        [SetUp]
        public void SetUp()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
        }

        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, false);
        }

        /// <summary>
        /// Service.RunAsync 与 Session.RunHitEvents HitEvent 序列完全等价。
        /// </summary>
        [Test]
        public async Task TestRunAsyncHitEventsMatchSessionRunHitEvents()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionHitEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var serviceResult = await new ManiaReplaySessionService().RunAsync(score, beatmap, environment).ConfigureAwait(true);
            var serviceHitEvents = serviceResult.ScoreInfo.HitEvents;

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(sessionHitEvents, serviceHitEvents), Is.True);
        }

        /// <summary>
        /// Service.RunAsync 与 Session.Run HitEvents 结果相同。
        /// </summary>
        [Test]
        public async Task TestRunAsyncStatisticsMatchSessionRun()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionHitEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var sessionStats = ManiaReplayParityHelper.AggregateHitEventResults(sessionHitEvents);

            var serviceResult = await new ManiaReplaySessionService().RunAsync(score, beatmap, environment).ConfigureAwait(true);
            var serviceStats = ManiaReplayParityHelper.AggregateHitEventResults(serviceResult.ScoreInfo.HitEvents);

            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(sessionStats, serviceStats), Is.True);
        }

        /// <summary>
        /// Service.RunAsync 与 Session.Run TotalScore 一致。
        /// </summary>
        [Test]
        public async Task TestRunAsyncTotalScoreMatchesSessionRun()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionResult = ManiaReplaySession.Run(score, beatmap, environment);

            var serviceResult = await new ManiaReplaySessionService().RunAsync(score, beatmap, environment).ConfigureAwait(true);

            Assert.That(serviceResult.ScoreInfo.TotalScore, Is.EqualTo(sessionResult.ScoreInfo.TotalScore));
        }

        /// <summary>
        /// Service.RunAsync 与 Session.Run Accuracy 一致。
        /// </summary>
        [Test]
        public async Task TestRunAsyncAccuracyMatchesSessionRun()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionResult = ManiaReplaySession.Run(score, beatmap, environment);

            var serviceResult = await new ManiaReplaySessionService().RunAsync(score, beatmap, environment).ConfigureAwait(true);

            Assert.That(Math.Abs(serviceResult.ScoreInfo.Accuracy - sessionResult.ScoreInfo.Accuracy), Is.LessThan(1e-6));
        }

        /// <summary>
        /// Service 在同一环境下的多次调用应命中缓存，返回相同结果。
        /// </summary>
        [Test]
        public async Task TestRunAsyncCacheReturnsSameResult()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var service = new ManiaReplaySessionService();

            var result1 = await service.RunAsync(score, beatmap, environment).ConfigureAwait(true);
            var result2 = await service.RunAsync(score, beatmap, environment).ConfigureAwait(true);

            Assert.That(result2.ScoreInfo.TotalScore, Is.EqualTo(result1.ScoreInfo.TotalScore));
            Assert.That(result2.ScoreInfo.Accuracy, Is.EqualTo(result1.ScoreInfo.Accuracy));
        }

        /// <summary>
        /// Service.RunTimelineAsync 与 Session.RunTimeline 结果一致。
        /// </summary>
        [Test]
        public async Task TestRunTimelineAsyncMatchesSessionRunTimeline()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionTimeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);
            var serviceTimeline = await new ManiaReplaySessionService().RunTimelineAsync(score, beatmap, environment).ConfigureAwait(true);

            Assert.That(serviceTimeline.FinalTotalScore, Is.EqualTo(sessionTimeline.FinalTotalScore));
            Assert.That(serviceTimeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
        }
    }
}
