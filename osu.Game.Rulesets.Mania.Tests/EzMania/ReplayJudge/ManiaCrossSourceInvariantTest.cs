// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaCrossSourceInvariantTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [Test]
        public void TestRunHitEventsAggregateMatchesStatistics()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcManyNoteTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var run = snapshotRun(score, beatmap, environment);
            var aggregated = ManiaReplayParityHelper.AggregateHitEventResults(run.hitEvents);

            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(run.statistics, aggregated), Is.True,
                () => $"sp=[{ManiaReplayParityHelper.DescribeStatistics(run.statistics)}] agg=[{ManiaReplayParityHelper.DescribeStatistics(aggregated)}]");
        }

        [Test]
        public void TestRunIsDeterministic()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcManyNoteTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var first = snapshotRun(score, beatmap, environment);
            var second = snapshotRun(score, beatmap, environment);
            var third = snapshotRun(score, beatmap, environment);

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(first.hitEvents, second.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(second.hitEvents, third.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(first.statistics, third.statistics), Is.True);
        }

        [Test]
        public void TestSameEnvironmentFromScoreMatchesFromLive()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcManyNoteTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            var fromScore = snapshotRun(score, beatmap, ManiaRuleset.ResolveEnvironment(score.ScoreInfo, ReplayRunPurpose.ForStored));
            var fromLive = snapshotRun(score, beatmap, ManiaRuleset.ResolveEnvironment(null, ReplayRunPurpose.ForLive));

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromScore.hitEvents, fromLive.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(fromScore.statistics, fromLive.statistics), Is.True,
                () => $"fromScore=[{ManiaReplayParityHelper.DescribeStatistics(fromScore.statistics)}] fromLive=[{ManiaReplayParityHelper.DescribeStatistics(fromLive.statistics)}]");
        }

        [Test]
        public void TestEz2AcHoldHeadSoftenRunMatchesGeneratorStatistics()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var session = snapshotRun(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);
            var generatorAgg = ManiaReplayParityHelper.AggregateHitEventResults(generatorEvents);

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(generatorEvents, session.hitEvents), Is.True);
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(session.statistics, generatorAgg), Is.True);
        }

        [Test]
        public void TestDifferentHitModeFromLiveChangesHitEvents()
        {
            var (score, beatmap, lazerEnvironment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var iidxEnvironment = BmsTapReplayFixtures.CreateTwoNoteColumnTap().environment;

            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, iidxEnvironment);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(lazerEnvironment);

            var fromScore = snapshotRun(score, beatmap, ManiaRuleset.ResolveEnvironment(score.ScoreInfo, ReplayRunPurpose.ForStored));
            var fromLive = snapshotRun(score, beatmap, ManiaRuleset.ResolveEnvironment(null, ReplayRunPurpose.ForLive));

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(fromScore.hitEvents, fromLive.hitEvents), Is.False);
        }

        private static (List<HitEvent> hitEvents, Dictionary<HitResult, int> statistics) snapshotRun(Score score, IBeatmap beatmap, IGameplayEnvironment environment)
        {
            var result = ManiaReplaySession.Run(score, beatmap, environment);
            return (result.ScoreInfo.HitEvents.ToList(), result.ScoreInfo.Statistics.ToDictionary());
        }
    }
}
