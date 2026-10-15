// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// POLICY-PARITY：叠键谱面下 Earliest / Combo / Duration Session 路径确定性；
    /// Drawable ≡ Session 见 <c>TestSceneReplaySessionParity</c> 同名用例。
    /// </summary>
    [TestFixture]
    public class ManiaJudgePrecedenceParityTest
    {
        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, EzEnumJudgePrecedence.Earliest);
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestLazerOverlappingJackSessionIsDeterministic(EzEnumJudgePrecedence precedence)
        {
            var (score, beatmap, environment) = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, precedence);

            var first = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var second = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(first, second), Is.True,
                () => $"Lazer precedence={precedence} session not deterministic: [{ManiaReplayParityHelper.DescribeHitEvents(first)}]");
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestIidxOverlappingJackSessionIsDeterministic(EzEnumJudgePrecedence precedence)
        {
            var (score, beatmap, environment) = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, precedence);

            var first = ManiaReplaySession.RunHitEvents(score, beatmap, environment);
            var second = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(first, second), Is.True,
                () => $"IIDX precedence={precedence} session not deterministic: [{ManiaReplayParityHelper.DescribeHitEvents(first)}]");
        }

        [Test]
        public void TestComboAndDurationMayDifferOnOverlappingPress()
        {
            var (score, beatmap, _) = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, EzEnumJudgePrecedence.Earliest);

            var comboEnv = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, EzEnumJudgePrecedence.Combo);
            var durationEnv = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, EzEnumJudgePrecedence.Duration);

            var comboEvents = ManiaReplaySession.RunHitEvents(score, beatmap, comboEnv);
            var durationEvents = ManiaReplaySession.RunHitEvents(score, beatmap, durationEnv);

            // 叠键重叠区按键：Combo 与 Duration 至少应产生可判定的首击（路由逻辑不同，但均需稳定输出）。
            Assert.That(comboEvents, Is.Not.Empty);
            Assert.That(durationEvents, Is.Not.Empty);
        }
    }
}
