// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class SessionDualExportHitEventsTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public async Task TestRunHitEventsAsyncMatchesRunAsyncHitEvents()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var service = new ManiaReplaySessionService();

            var fromHitEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var fromRunAsync = (await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true)).ScoreInfo.HitEvents;

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromHitEvents, fromRunAsync.ToList()), Is.True);
        }

        [Test]
        public async Task TestRunHitEventsAsyncMatchesRunRequestAsyncHitEvents()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var service = new ManiaReplaySessionService();

            var fromHitEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var fromRequest = await service.RunRequestAsync(new ReplayRunRequest(score, beatmap, ReplayRunPurpose.ForLive)).ConfigureAwait(true);

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromHitEvents, fromRequest.Score.ScoreInfo.HitEvents.ToList()), Is.True);
        }

        [Test]
        public async Task TestBmsKpoorDualExportHitEventsMatch()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);
            var service = new ManiaReplaySessionService();

            var fromHitEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var fromRunAsync = (await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true)).ScoreInfo.HitEvents;

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromHitEvents, fromRunAsync.ToList()), Is.True);
        }

        [Test]
        public async Task TestEz2AcHoldDualExportHitEventsMatch()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);
            var service = new ManiaReplaySessionService();

            var fromHitEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var fromRunAsync = (await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true)).ScoreInfo.HitEvents;

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromHitEvents, fromRunAsync.ToList()), Is.True);
        }

        [Test]
        public async Task TestRunHitEventsAsyncMatchesRunAsyncHitEventsForEz2Hold()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            var service = new ManiaReplaySessionService();
            var fromHitEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var serviceEvents = (await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true)).ScoreInfo.HitEvents;

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromHitEvents, serviceEvents.ToList()), Is.True);
        }
    }
}
