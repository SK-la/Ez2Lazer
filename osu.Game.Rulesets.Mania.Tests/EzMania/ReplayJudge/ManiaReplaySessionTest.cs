// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
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

            var hitEvents = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(hitEvents.Count, Is.EqualTo(2));
            Assert.That(hitEvents.Single(e => e.HitObject.StartTime == 1000).Result.IsHit(), Is.True);
            Assert.That(hitEvents.Single(e => e.HitObject.StartTime == 2000).Result.IsHit(), Is.True);
        }

        [Test]
        public void TestLazerTapSessionMatchesGeneratorJudgements()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestLazerHoldPerfectSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateSingleHoldPerfect();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
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

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
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

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestRegistryReturnsBmsStrategyForIidx()
        {
            var environment = new GameplayEnvironment
            {
                ManiaHitMode = EzEnumHitMode.IIDX_HD,
                ManiaHealthMode = EzEnumHealthMode.IIDX_HD,
                JudgePrecedence = EzEnumJudgePrecedence.Earliest,
            };

            Assert.That(ManiaJudgementRegistry.GetHitModeJudgement(EzEnumHitMode.IIDX_HD), Is.SameAs(BmsHitModeJudgement.Instance));
            Assert.That(ManiaJudgementRegistry.GetNoteStrategy(environment), Is.SameAs(BmsHitModeJudgement.Instance));
            Assert.That(ManiaJudgementRegistry.GetHoldStrategy(environment), Is.SameAs(BmsHitModeJudgement.Instance));
            Assert.That(BmsHitModeJudgement.MapTo(BmsJudge.Bad), Is.EqualTo(HitResult.Meh));
            Assert.That(BmsHitModeJudgement.MapTo(BmsJudge.KPoor), Is.EqualTo(HitResult.Poor));
        }

        private static void runSessionAndGeneratorParity(
            Score score,
            IBeatmap beatmap,
            GameplayEnvironment environment,
            Action? configure = null)
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            configure?.Invoke();
            var live = GameplayEnvironment.FromLive(GlobalConfigStore.EzConfig);

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, live);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestBmsEarlyBadSessionProducesBadJudge()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(sessionEvents.Any(e => e.HitObject.StartTime == 1000 && HitModeReplayFixtures.ToBmsJudge(e.Result) == BmsJudge.Bad), Is.True,
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestBmsPostBadKPoorSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();
            runSessionAndGeneratorParity(score, beatmap, environment, () =>
                GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, true));
        }

        [Test]
        public void TestBmsEarlyKPoorPressSessionProducesKPoor()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyKPoorPress();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(sessionEvents.Any(e => HitModeReplayFixtures.ToBmsJudge(e.Result) == BmsJudge.KPoor), Is.True,
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestO2PillUpgradesBadRangeToCool()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2PillUpgradeOnBadRange();
            const int pill_note_time = 1000 + 15 * 400;

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);

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

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(sessionEvents.Single(e => e.HitObject is HeadNote).Result, Is.EqualTo(Ez2AcHitModeJudgement.MapTo(Ez2AcJudge.Perfect)),
                () => ManiaReplayParityHelper.DescribeJudgements(sessionEvents));
        }

        [Test]
        public void TestEz2AcHoldHeadSoftenSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestO2TapSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2TwoNoteTap();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
        }

        [Test]
        public void TestEz2AcHoldHeadSessionMatchesGenerator()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateEz2AcHoldHeadPerfect();

            var sessionEvents = ManiaReplaySession.Run(score, beatmap, environment);
            var generatorEvents = ManiaScoreHitEventGenerator.Instance.Generate(score, beatmap);

            Assert.That(ManiaReplayParityHelper.AreJudgementsEquivalent(generatorEvents, sessionEvents), Is.True,
                () => $"generator=[{ManiaReplayParityHelper.DescribeJudgements(generatorEvents)}] session=[{ManiaReplayParityHelper.DescribeJudgements(sessionEvents)}]");
            Assert.That(sessionEvents.Single(e => e.HitObject is HeadNote).Result, Is.EqualTo(Ez2AcHitModeJudgement.MapTo(Ez2AcJudge.Perfect)));
        }
    }
}
