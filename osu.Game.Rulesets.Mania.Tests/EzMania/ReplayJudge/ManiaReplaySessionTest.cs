// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaReplaySessionTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, false);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);
        }

        [Test]
        public void TestLazerTapReplayProducesExpectedHitEvents()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var hitEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(hitEvents.Count, Is.EqualTo(2));
            Assert.That(hitEvents.Single(e => e.HitObject.StartTime == 1000).Result.IsHit(), Is.True);
            Assert.That(hitEvents.Single(e => e.HitObject.StartTime == 2000).Result.IsHit(), Is.True);
            Assert.That(hitEvents, Has.All.Matches<HitEvent>(e => e.GameplayRate != null));
        }

        [Test]
        public void TestRunTimelineFinalScoreMatchesSessionRunTotal()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var hitEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            long sessionTotal = ManiaReplaySession.Run(score, beatmap, environment).ScoreInfo.TotalScore;
            var timeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);

            Assert.That(hitEvents, Has.Count.EqualTo(2));
            Assert.That(sessionTotal, Is.GreaterThan(0));
            Assert.That(timeline.FinalTotalScore, Is.EqualTo(sessionTotal));
            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(2500).TotalScore, Is.EqualTo(timeline.FinalTotalScore));
        }

        [Test]
        public void TestRunTimelineIsDeterministic()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var timeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);
            var timelineRepeat = ManiaReplaySession.RunTimeline(score, beatmap, environment);

            Assert.That(timelineRepeat.FinalTotalScore, Is.EqualTo(timeline.FinalTotalScore));
        }

        [Test]
        public void TestRunTimelineUsesLiveHitModeNotScoreEmbedded()
        {
            var (score, beatmap, lazerEnvironment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var iidxEnvironment = BmsTapReplayFixtures.CreateTwoNoteColumnTap().environment;

            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, iidxEnvironment);

            var liveTimeline = ManiaReplaySession.RunTimeline(score, beatmap, ManiaRuleset.ResolveEnvironment(null, GlobalConfigStore.EzConfig, ReplayRunPurpose.ForRaceTimeline));
            var lazerTimeline = ManiaReplaySession.RunTimeline(score, beatmap, lazerEnvironment);
            var iidxTimeline = ManiaReplaySession.RunTimeline(score, beatmap, iidxEnvironment);

            Assert.That(liveTimeline.FinalTotalScore, Is.EqualTo(lazerTimeline.FinalTotalScore));
            Assert.That(liveTimeline.FinalTotalScore, Is.Not.EqualTo(iidxTimeline.FinalTotalScore));

            _ = ManiaScoreHitEventGenerator.Instance;
            var bridgeTimeline = EzScoreTimelineBridge.TryBuildManiaTimeline(score, beatmap);

            Assert.That(bridgeTimeline, Is.Not.Null);
            Assert.That(bridgeTimeline!.FinalTotalScore, Is.EqualTo(lazerTimeline.FinalTotalScore));
        }

        [Test]
        public void TestManiaTimelineBridgeMatchesRunTimeline()
        {
            _ = ManiaScoreHitEventGenerator.Instance;

            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionTimeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);
            var bridgeTimeline = EzScoreTimelineBridge.TryBuildManiaTimeline(score, beatmap);

            Assert.That(bridgeTimeline, Is.Not.Null);
            Assert.That(bridgeTimeline!.FinalTotalScore, Is.EqualTo(sessionTimeline.FinalTotalScore));
            Assert.That(bridgeTimeline.QueryAtTime(2500).TotalScore, Is.EqualTo(sessionTimeline.FinalTotalScore));
        }

        [Test]
        public void TestRunTimelineReturnsEmptyForEmptyReplayFrames()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            score.Replay!.Frames.Clear();

            var timeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);

            Assert.That(timeline.FinalTotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
        }

        [Test]
        public void TestLazerTapSessionMatchesGeneratorJudgements()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestLazerHoldPerfectSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateSingleHoldPerfect();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
            Assert.That(sessionEvents.Single(e => e.HitObject is HeadNote).Result, Is.EqualTo(HitResult.Perfect));
            Assert.That(sessionEvents.Single(e => e.HitObject is TailNote).Result, Is.EqualTo(HitResult.Perfect));
        }

        [Test]
        public void TestLazerHoldLateReleaseSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateSingleHoldLateRelease();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
            Assert.That(sessionEvents.Single(e => e.HitObject is HeadNote).Result, Is.EqualTo(HitResult.Perfect));
            Assert.That(sessionEvents.Single(e => e.HitObject is TailNote).Result, Is.EqualTo(HitResult.Miss));
        }

        [Test]
        public void TestIidxTapSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = BmsTapReplayFixtures.CreateTwoNoteColumnTap();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestRegistryReturnsBmsStrategyForIidx()
        {
            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            Assert.That(ManiaJudgementRegistry.GetHitModeJudgement(EzEnumHitMode.IIDX_HD), Is.SameAs(BmsHitModeJudgement.Instance));
            Assert.That(ManiaJudgementRegistry.GetNoteStrategy(environment), Is.SameAs(BmsHitModeJudgement.Instance));
            Assert.That(ManiaJudgementRegistry.GetHoldStrategy(environment), Is.SameAs(BmsHitModeJudgement.Instance));
            Assert.That(BmsHitModeJudgement.MapTo(BmsJudge.Bad), Is.EqualTo(HitResult.Meh));
            Assert.That(BmsHitModeJudgement.MapTo(BmsJudge.KPoor), Is.EqualTo(HitResult.Poor));
        }

        [Test]
        public void TestGeneratorUsesScoreEmbeddedHitModeNotLiveConfig()
        {
            var (score, beatmap, iidxEnvironment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, iidxEnvironment);

            var lazerEnvironment = LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment;
            ReplayJudgeTestConfig.ApplyToGlobalConfig(lazerEnvironment);

            var fromScore = ManiaRuleset.ResolveEnvironment(score.ScoreInfo, GlobalConfigStore.EzConfig, ReplayRunPurpose.ForStoredStatistics);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);
            var sessionFromScore = ManiaReplaySession.RunHitEvents(score, beatmap, fromScore);
            var sessionLazer = ManiaReplaySession.RunHitEvents(score, beatmap, lazerEnvironment);

            Assert.That(fromScore.ManiaHitMode, Is.EqualTo(EzEnumHitMode.IIDX_HD));
            Assert.That(GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode), Is.EqualTo(EzEnumHitMode.Lazer));
            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionFromScore), Is.True,
                () => ManiaReplayParityHelper.DescribeJudgements(generatorEvents));
            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionLazer), Is.False);
        }

        private static void runSessionAndGeneratorParity(
            Score score,
            IBeatmap beatmap,
            ManiaGameplayEnvironment environment,
            Action? configure = null)
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);
            configure?.Invoke();

            var sessionEnvironment = ManiaRuleset.ResolveEnvironment(score.ScoreInfo, GlobalConfigStore.EzConfig, ReplayRunPurpose.ForStoredStatistics);
            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, sessionEnvironment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestBmsEarlyBadSessionProducesBadJudge()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(sessionEvents.Any(e => e.HitObject.StartTime == 1000 && HitModeReplayFixtures.ToBmsJudge(e.Result) == BmsJudge.Bad), Is.True,
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestBmsPostBadKPoorSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();
            runSessionAndGeneratorParity(score, beatmap, environment);
        }

        [Test]
        public void TestBmsEarlyKPoorPressSessionProducesKPoor()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyKPoorPress();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(sessionEvents.Any(e => HitModeReplayFixtures.ToBmsJudge(e.Result) == BmsJudge.KPoor), Is.True,
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestO2PillUpgradesBadRangeToCool()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2PillUpgradeOnBadRange();
            const int pill_note_time = 1000 + 15 * 400;

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(sessionEvents.Any(e => e.HitObject.StartTime == pill_note_time && HitModeReplayFixtures.ToO2Judge(e.Result) == O2Judge.Cool), Is.True,
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestO2PillSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2PillUpgradeOnBadRange();
            runSessionAndGeneratorParity(score, beatmap, environment);
        }

        [Test]
        public void TestEz2AcHoldHeadGreatSoftenedToPerfect()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(sessionEvents.Single(e => e.HitObject is HeadNote).Result, Is.EqualTo(Ez2AcHitModeJudgement.MapTo(Ez2AcJudge.Kool)),
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestEz2AcHoldHeadSoftenSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestO2TapSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2TwoNoteTap();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestMalodyHoldSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateMalodyHoldPerfect();
            runSessionAndGeneratorParity(score, beatmap, environment);
        }

        [Test]
        public void TestMalodyHoldEarlyReleaseSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateMalodyHoldEarlyRelease();
            runSessionAndGeneratorParity(score, beatmap, environment);
        }

        [Test]
        public void TestEz2AcHoldHeadSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadPerfect();

            var sessionEvents = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
            Assert.That(sessionEvents.Single(e => e.HitObject is HeadNote).Result, Is.EqualTo(Ez2AcHitModeJudgement.MapTo(Ez2AcJudge.Kool)));
        }

        [Test]
        public void TestEz2AcTimelineTotalScoreMonotonicAndScalesFromZero()
        {
            const int note_count = 20;
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcManyNoteTap(note_count);
            var timeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);

            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0));

            long firstJudgementScore = timeline.QueryAtTime(1000).TotalScore;
            Assert.That(firstJudgementScore, Is.GreaterThan(0));
            Assert.That(firstJudgementScore, Is.LessThan(timeline.FinalTotalScore * 0.2));

            long previousScore = 0;

            for (int i = 0; i < note_count; i++)
            {
                double time = 1000 + i * 500;
                long scoreAtTime = timeline.QueryAtTime(time).TotalScore;
                Assert.That(scoreAtTime, Is.GreaterThanOrEqualTo(previousScore), $"TotalScore decreased at t={time}");
                previousScore = scoreAtTime;
            }

            Assert.That(timeline.QueryAtTime(1000 + (note_count - 1) * 500).TotalScore, Is.EqualTo(timeline.FinalTotalScore));
        }

        [Test]
        public void TestRunTimelineFinalTotalScoreMatchesRunTotalScore()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcManyNoteTap();
            long sessionTotal = ManiaReplaySession.Run(score, beatmap, environment).ScoreInfo.TotalScore;
            var timeline = ManiaReplaySession.RunTimeline(score, beatmap, environment);

            Assert.That(timeline.FinalTotalScore, Is.EqualTo(sessionTotal));
        }
    }
}
