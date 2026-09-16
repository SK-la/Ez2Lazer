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
        public void TestEmbeddedHitModeControlsDisplayedResults()
        {
            var score = createScore(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
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
                ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal));

            var score = createScore(null, null);
            var displayed = score.GetStatisticsForDisplay().Select(statistic => statistic.Result).ToArray();

            Assert.That(displayed, Does.Contain(HitResult.Great));
            Assert.That(displayed, Does.Contain(HitResult.Ok));
        }

        [Test]
        public void TestLegacyNegativeModesUseLazerDisplay()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(
                ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal));

            var score = new ScoreInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                ManiaHitMode = -1,
                ManiaHealthMode = -1,
            };

            var displayed = score.GetStatisticsForDisplay().Select(statistic => statistic.Result).ToArray();

            Assert.That(displayed, Does.Contain(HitResult.Great));
            Assert.That(displayed, Does.Contain(HitResult.Ok));
        }

        [Test]
        public void TestGlobalBmsHitModeIncludesKpoorInHitResultsForDisplay()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(
                ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD));

            var ruleset = new ManiaRuleset();
            var displayResults = ruleset.GetHitResultsForDisplay().ToArray();

            Assert.That(displayResults.Select(r => r.result), Does.Contain(HitResult.Poor));

            var poorName = displayResults.First(r => r.result == HitResult.Poor).displayName;
            Assert.That(poorName.ToString(), Is.EqualTo("KPoor"));
        }

        private static ScoreInfo createScore(EzEnumHitMode? hitMode, EzEnumHealthMode? healthMode) => new ScoreInfo
        {
            Ruleset = new ManiaRuleset().RulesetInfo,
            ManiaHitMode = hitMode.HasValue ? (int)hitMode.Value : (int)EzEnumHitMode.Lazer,
            ManiaHealthMode = healthMode.HasValue ? (int)healthMode.Value : (int)EzEnumHealthMode.Lazer,
        };
    }
}
