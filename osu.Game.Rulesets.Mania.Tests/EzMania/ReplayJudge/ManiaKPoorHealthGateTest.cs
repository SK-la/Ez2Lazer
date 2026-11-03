// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaKPoorHealthGateTest
    {
        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestBmsJudgeModeLazerHealthProducesNoExtraPoorFromKPoorRouting()
        {
            var (score, beatmap, bmsHealthEnv) = JudgeModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();
            var lazerHealthEnv = bmsHealthEnv with { HealthMode = EzEnumHealthMode.Lazer };

            var withBmsHealth = ManiaReplaySession.Run(score.DeepClone(), beatmap, bmsHealthEnv);
            var withLazerHealth = ManiaReplaySession.Run(score.DeepClone(), beatmap, lazerHealthEnv);

            int poorBmsHealth = withBmsHealth.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Poor);
            int poorLazerHealth = withLazerHealth.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Poor);

            Assert.That(poorBmsHealth, Is.GreaterThan(poorLazerHealth),
                "BMS HealthMode 应允许 post-Bad KPoor 路由，Lazer HealthMode 不应");
        }

        [Test]
        public void TestPoorEnabledRequiresBmsHealthMode()
        {
            Assert.That(HealthModeHelper.ComputeKPoorEnabled(EzEnumHealthMode.IIDX_HD, true), Is.True);
            Assert.That(HealthModeHelper.ComputeKPoorEnabled(EzEnumHealthMode.Lazer, true), Is.False);
            Assert.That(HealthModeHelper.ComputeKPoorEnabled(EzEnumHealthMode.IIDX_HD, false), Is.False);
        }

        [Test]
        public void TestResolveEnvironmentForStoredUsesEmbeddedModesOrLazerFallback()
        {
            var config = GlobalConfigStore.EzConfig;
            config.SetValue(Ez2Setting.ManiaJudgeMode, EzEnumJudgeMode.IIDX_HD);
            config.SetValue(Ez2Setting.ManiaHealthMode, EzEnumHealthMode.Lazer);
            config.SetValue(Ez2Setting.OffsetPlusMania, 25.0);

            var scoreInfo = new ScoreInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                JudgeMode = (int)EzEnumJudgeMode.LR2_HD,
                HealthMode = (int)EzEnumHealthMode.IIDX_HD,
            };

            var stored = config.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);
            Assert.That(stored.JudgeMode, Is.EqualTo(EzEnumJudgeMode.LR2_HD));
            Assert.That(stored.HealthMode, Is.EqualTo(EzEnumHealthMode.IIDX_HD));
            Assert.That(stored.OffsetPlusMania, Is.EqualTo(0));

            var legacy = config.ResolveEnvironment(ReplayRunPurpose.ForStored, new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo });
            Assert.That(legacy.JudgeMode, Is.EqualTo(EzEnumJudgeMode.Lazer));
            Assert.That(legacy.HealthMode, Is.EqualTo(EzEnumHealthMode.Lazer));

            var live = config.GetGameplayEnvironment(new ManiaRuleset().RulesetInfo);
            Assert.That(live.JudgeMode, Is.EqualTo(EzEnumJudgeMode.IIDX_HD));
            Assert.That(live.HealthMode, Is.EqualTo(EzEnumHealthMode.Lazer));
            Assert.That(live.OffsetPlusMania, Is.EqualTo(25.0));

            var recalcStored = config.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);
            Assert.That(recalcStored.OffsetPlusMania, Is.EqualTo(0));
        }
    }
}
