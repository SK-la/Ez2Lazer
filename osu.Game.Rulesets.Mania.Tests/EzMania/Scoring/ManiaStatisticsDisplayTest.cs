// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Scoring
{
    [TestFixture]
    public class ManiaStatisticsDisplayTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestEmbeddedJudgeModeControlsDisplayedResults()
        {
            var score = createScore(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            score.Statistics[HitResult.Great] = 7;
            score.Statistics[HitResult.Meh] = 3;

            var displayed = score.GetStatisticsForDisplay().Select(statistic => statistic.Result).ToArray();

            Assert.That(displayed, Does.Contain(HitResult.Meh));
            Assert.That(displayed, Does.Not.Contain(HitResult.Great));
            Assert.That(displayed, Does.Not.Contain(HitResult.Ok));
            Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(7));
            Assert.That(score.Statistics[HitResult.Meh], Is.EqualTo(3));
        }

        [Test]
        public void TestMissingEmbeddedModesUseLazerDisplay()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(
                ReplayJudgeTestConfig.Create(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal));

            var score = createScore(null, null);
            var displayed = score.GetStatisticsForDisplay().Select(statistic => statistic.Result).ToArray();

            Assert.That(displayed, Does.Contain(HitResult.Great));
            Assert.That(displayed, Does.Contain(HitResult.Ok));
        }

        [Test]
        public void TestLegacyNegativeModesUseLazerDisplay()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(
                ReplayJudgeTestConfig.Create(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal));

            var score = new ScoreInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                JudgeMode = -1,
                HealthMode = -1,
            };

            var displayed = score.GetStatisticsForDisplay().Select(statistic => statistic.Result).ToArray();

            Assert.That(displayed, Does.Contain(HitResult.Great));
            Assert.That(displayed, Does.Contain(HitResult.Ok));
        }

        [Test]
        public void TestGlobalBmsJudgeModeIncludesKpoorInHitResultsForDisplay()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(
                ReplayJudgeTestConfig.Create(EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD));

            var ruleset = new ManiaRuleset();
            var displayResults = ruleset.GetHitResultsForDisplay().ToArray();

            Assert.That(displayResults.Select(r => r.result), Does.Contain(HitResult.Poor));

            var poorName = displayResults.First(r => r.result == HitResult.Poor).displayName;
            Assert.That(poorName.ToString(), Is.EqualTo("KPoor"));
        }

        private static ScoreInfo createScore(EzEnumJudgeMode? judgeMode, EzEnumHealthMode? healthMode) => new ScoreInfo
        {
            Ruleset = new ManiaRuleset().RulesetInfo,
            JudgeMode = judgeMode.HasValue ? (int)judgeMode.Value : (int)EzEnumJudgeMode.Lazer,
            HealthMode = healthMode.HasValue ? (int)healthMode.Value : (int)EzEnumHealthMode.Lazer,
        };

        /// <summary>
        /// 成绩表格列的集合必须逐份按成绩自身嵌入的 JudgeMode 取并集，不能看当前全局设置：
        /// 全局是 Lazer 时 Poor 不在有效集合里，只含 BMS 成绩的 KPoor 列会被整列丢掉。
        /// </summary>
        [Test]
        public void TestScoreSetDisplayUnionsPerScoreJudgeModes()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer));

            var lazerScore = createScore(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);
            var bmsScore = createScore(EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            var ruleset = new ManiaRuleset();

            // 单份重载只看全局：Lazer 下不应出现 Poor。
            Assert.That(ruleset.GetHitResultsForDisplay().Select(r => r.result), Does.Not.Contain(HitResult.Poor));

            var displayResults = ruleset.GetHitResultsForDisplay(new[] { lazerScore, bmsScore }).ToArray();

            Assert.That(displayResults.Select(r => r.result), Does.Contain(HitResult.Poor));
            Assert.That(displayResults.First(r => r.result == HitResult.Poor).displayName.ToString(), Is.EqualTo("KPoor"));
        }
    }
}
