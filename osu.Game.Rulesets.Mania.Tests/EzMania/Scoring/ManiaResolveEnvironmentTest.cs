// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Scoring
{
    [TestFixture]
    public class ManiaResolveEnvironmentTest
    {
        private Ez2ConfigManager config = null!;

        private GameplayEnvironment liveBaseline = null!;

        [SetUp]
        public void SetUp()
        {
            config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;

            liveBaseline = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, offsetPlusMania: 12.0, bmsPoorHitResultEnable: true);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(liveBaseline);
        }

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestForLiveAnalysisReadsAllLiveFields()
        {
            var env = GlobalConfigStore.EzConfig.GetGameplayEnvironment();

            Assert.That(env, Is.EqualTo(liveBaseline));
        }

        [Test]
        public void TestForStoredStatisticsUsesEmbeddedModes()
        {
            var scoreInfo = new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo };
            ReplayJudgeTestConfig.ApplyEmbeddedModes(new Score { ScoreInfo = scoreInfo }, ReplayJudgeTestConfig.Create(EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD));

            var env = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            Assert.That(env.JudgeMode, Is.EqualTo(EzEnumJudgeMode.IIDX_HD));
            Assert.That(env.HealthMode, Is.EqualTo(EzEnumHealthMode.IIDX_HD));
            Assert.That(env.JudgePrecedence, Is.EqualTo(liveBaseline.JudgePrecedence));
            Assert.That(env.OffsetPlusMania, Is.EqualTo(0));
            Assert.That(env.BmsPoorHitResultEnable, Is.EqualTo(liveBaseline.BmsPoorHitResultEnable));
        }

        [Test]
        public void TestForStoredStatisticsWithoutEmbeddedUsesLazerModesForMania()
        {
            var scoreInfo = new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo };

            var stored = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            Assert.That(stored.JudgeMode, Is.EqualTo(EzEnumJudgeMode.Lazer));
            Assert.That(stored.HealthMode, Is.EqualTo(EzEnumHealthMode.Lazer));
            Assert.That(stored.JudgePrecedence, Is.EqualTo(liveBaseline.JudgePrecedence));
            Assert.That(stored.OffsetPlusMania, Is.EqualTo(0));
        }

        [Test]
        public void TestResolveEnvironmentZerosOffsetForSession()
        {
            var scoreInfo = new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo };

            var session = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForLive, scoreInfo, ignoreOffset: true);
            var live = GlobalConfigStore.EzConfig.GetGameplayEnvironment();

            Assert.That(session.OffsetPlusMania, Is.EqualTo(0));
            Assert.That(live.OffsetPlusMania, Is.EqualTo(liveBaseline.OffsetPlusMania));
            Assert.That(session with { OffsetPlusMania = live.OffsetPlusMania }, Is.EqualTo(live));
        }

        [Test]
        public void TestBmsPoorComesFromConfigNotImplicitDefault()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(liveBaseline with { BmsPoorHitResultEnable = false });

            var env = GlobalConfigStore.EzConfig.GetGameplayEnvironment();

            Assert.That(env.BmsPoorHitResultEnable, Is.False);
        }
    }
}
