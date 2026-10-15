// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaCrossSourceInvariantTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestRunHitEventsAggregateMatchesStatistics()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateEz2AcManyNoteTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var run = snapshotRun(score, beatmap, environment);
            var aggregated = ManiaReplayParityHelper.AggregateHitEventResults(run.hitEvents);

            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(run.statistics, aggregated), Is.True,
                () => $"sp=[{ManiaReplayParityHelper.DescribeStatistics(run.statistics)}] agg=[{ManiaReplayParityHelper.DescribeStatistics(aggregated)}]");
        }

        [Test]
        public void TestRunIsDeterministic()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateEz2AcManyNoteTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var first = snapshotRun(score, beatmap, environment);
            var second = snapshotRun(score, beatmap, environment);
            var third = snapshotRun(score, beatmap, environment);

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(first.hitEvents, second.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(second.hitEvents, third.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(first.statistics, third.statistics), Is.True);
        }

        [Test]
        public async Task TestMixedEntryDeterministicAcrossFreshServices()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            var snapshots = new List<(List<HitEvent> hitEvents, Dictionary<HitResult, int> statistics, double accuracy, long totalScore)>();

            for (int i = 0; i < 5; i++)
            {
                var service = new ManiaReplaySessionService();

                switch (i % 3)
                {
                    case 0:
                        snapshots.Add(await snapshotFromRunHitEventsAsync(service, score, beatmap).ConfigureAwait(true));
                        break;

                    case 1:
                        snapshots.Add(await snapshotFromRunAsync(service, score, beatmap).ConfigureAwait(true));
                        break;

                    default:
                        snapshots.Add(await snapshotFromRunRequestAsync(service, score, beatmap).ConfigureAwait(true));
                        break;
                }
            }

            for (int i = 1; i < snapshots.Count; i++)
            {
                Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(snapshots[0].hitEvents, snapshots[i].hitEvents), Is.True, $"HitEvents drift at run {i}");
                Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(snapshots[0].statistics, snapshots[i].statistics), Is.True, $"Statistics drift at run {i}");
                Assert.That(snapshots[i].accuracy, Is.EqualTo(snapshots[0].accuracy));
                Assert.That(snapshots[i].totalScore, Is.EqualTo(snapshots[0].totalScore));
            }
        }

        [Test]
        public void TestOffsetZeroSessionMatchesOriginalStatistics()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            environment = environment with { OffsetPlusMania = 0 };

            var session = ManiaReplaySession.Run(score, beatmap, environment);
            var aggregated = ManiaReplayParityHelper.AggregateHitEventResults(session.ScoreInfo.HitEvents);

            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(session.ScoreInfo.Statistics, aggregated), Is.True);
            Assert.That(session.ScoreInfo.Accuracy, Is.GreaterThan(0));
        }

        [Test]
        public void TestSameEnvironmentFromScoreMatchesFromLive()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateEz2AcManyNoteTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            var fromScore = snapshotRun(score, beatmap, GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo));
            var fromLive = snapshotRun(score, beatmap, GlobalConfigStore.EzConfig.GetGameplayEnvironment());

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromScore.hitEvents, fromLive.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(fromScore.statistics, fromLive.statistics), Is.True,
                () => $"fromScore=[{ManiaReplayParityHelper.DescribeStatistics(fromScore.statistics)}] fromLive=[{ManiaReplayParityHelper.DescribeStatistics(fromLive.statistics)}]");
        }

        [Test]
        public async Task TestEz2AcHoldHeadSoftenRunMatchesGeneratorStatistics()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            var session = snapshotRun(score, beatmap, environment);
            var service = new ManiaReplaySessionService();
            var generatorEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var generatorRun = await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true);
            var generatorStats = generatorRun.ScoreInfo.Statistics.ToDictionary();

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(generatorEvents, session.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(session.statistics, generatorStats), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(
                session.statistics,
                ManiaReplayParityHelper.AggregateHitEventResults(generatorEvents)), Is.True);
        }

        [Test]
        public void TestEz2AcHoldHeadSoftenRunHitEventsAggregateMatchesStatistics()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var run = snapshotRun(score, beatmap, environment);
            var aggregated = ManiaReplayParityHelper.AggregateHitEventResults(run.hitEvents);

            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(run.statistics, aggregated), Is.True,
                () => $"sp=[{ManiaReplayParityHelper.DescribeStatistics(run.statistics)}] agg=[{ManiaReplayParityHelper.DescribeStatistics(aggregated)}]");
        }

        [Test]
        public void TestDifferentJudgeModeFromLiveChangesHitEvents()
        {
            var (score, beatmap, lazerEnvironment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var iidxEnvironment = BmsTapReplayFixtures.CreateTwoNoteColumnTap().environment;

            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, iidxEnvironment);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(lazerEnvironment);

            var fromScore = snapshotRun(score, beatmap, GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo));
            var fromLive = snapshotRun(score, beatmap, GlobalConfigStore.EzConfig.GetGameplayEnvironment());

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromScore.hitEvents, fromLive.hitEvents), Is.False);
        }

        private static (List<HitEvent> hitEvents, Dictionary<HitResult, int> statistics) snapshotRun(Score score, IBeatmap beatmap, IGameplayEnvironment environment)
        {
            var result = ManiaReplaySession.Run(score, beatmap, environment);
            return (result.ScoreInfo.HitEvents.ToList(), result.ScoreInfo.Statistics.ToDictionary());
        }

        private static async Task<(List<HitEvent> hitEvents, Dictionary<HitResult, int> statistics, double accuracy, long totalScore)> snapshotFromRunHitEventsAsync(
            ManiaReplaySessionService service, Score score, IBeatmap beatmap)
        {
            var hitEvents = await service.RunHitEventsAsync(score, beatmap).ConfigureAwait(true);
            var run = await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true);
            return (hitEvents, run.ScoreInfo.Statistics.ToDictionary(), run.ScoreInfo.Accuracy, run.ScoreInfo.TotalScore);
        }

        private static async Task<(List<HitEvent> hitEvents, Dictionary<HitResult, int> statistics, double accuracy, long totalScore)> snapshotFromRunAsync(
            ManiaReplaySessionService service, Score score, IBeatmap beatmap)
        {
            var result = await service.RunAsync(score, beatmap, ReplayRunPurpose.ForLive).ConfigureAwait(true);
            return (result.ScoreInfo.HitEvents.ToList(), result.ScoreInfo.Statistics.ToDictionary(), result.ScoreInfo.Accuracy, result.ScoreInfo.TotalScore);
        }

        private static async Task<(List<HitEvent> hitEvents, Dictionary<HitResult, int> statistics, double accuracy, long totalScore)> snapshotFromRunRequestAsync(
            ManiaReplaySessionService service, Score score, IBeatmap beatmap)
        {
            var combined = await service.RunRequestAsync(new ReplayRunRequest(score, beatmap, ReplayRunPurpose.ForLive)).ConfigureAwait(true);
            var info = combined.Score.ScoreInfo;
            return (info.HitEvents.ToList(), info.Statistics.ToDictionary(), info.Accuracy, info.TotalScore);
        }
    }
}
