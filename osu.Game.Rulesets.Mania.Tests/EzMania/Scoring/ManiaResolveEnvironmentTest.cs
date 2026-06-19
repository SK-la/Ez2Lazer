// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Scoring
{
    [TestFixture]
    public class ManiaResolveEnvironmentTest
    {
        private Ez2ConfigManager config = null!;

        private ManiaGameplayEnvironment liveBaseline = null!;

        [SetUp]
        public void SetUp()
        {
            config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;

            liveBaseline = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer, offsetPlusMania: 12.0, bmsPoorHitResultEnable: true);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(liveBaseline);
        }

        [Test]
        public void TestForLiveAnalysisReadsAllLiveFields()
        {
            var env = ManiaRuleset.ResolveEnvironment(null, config, ReplayRunPurpose.ForLiveAnalysis);

            Assert.That(env, Is.EqualTo(liveBaseline));
        }

        [Test]
        public void TestForStoredStatisticsUsesEmbeddedModes()
        {
            var scoreInfo = new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo };
            ReplayJudgeTestConfig.ApplyEmbeddedModes(new Score { ScoreInfo = scoreInfo }, ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD));

            var env = ManiaRuleset.ResolveEnvironment(scoreInfo, config, ReplayRunPurpose.ForStoredStatistics);

            Assert.That(env.ManiaHitMode, Is.EqualTo(EzEnumHitMode.IIDX_HD));
            Assert.That(env.ManiaHealthMode, Is.EqualTo(EzEnumHealthMode.IIDX_HD));
            Assert.That(env.JudgePrecedence, Is.EqualTo(liveBaseline.JudgePrecedence));
            Assert.That(env.OffsetPlusMania, Is.EqualTo(liveBaseline.OffsetPlusMania));
            Assert.That(env.BmsPoorHitResultEnable, Is.EqualTo(liveBaseline.BmsPoorHitResultEnable));
        }

        [Test]
        public void TestForStoredStatisticsWithoutEmbeddedFallsBackToLive()
        {
            var scoreInfo = new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo };

            var live = ManiaRuleset.ResolveEnvironment(null, config, ReplayRunPurpose.ForLiveAnalysis);
            var stored = ManiaRuleset.ResolveEnvironment(scoreInfo, config, ReplayRunPurpose.ForStoredStatistics);

            Assert.That(stored, Is.EqualTo(live));
        }

        [Test]
        public void TestBmsPoorComesFromConfigNotImplicitDefault()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(liveBaseline with { BmsPoorHitResultEnable = false });

            var env = ManiaRuleset.ResolveEnvironment(null, config, ReplayRunPurpose.ForLiveAnalysis);

            Assert.That(env.BmsPoorHitResultEnable, Is.False);
        }
    }
}
