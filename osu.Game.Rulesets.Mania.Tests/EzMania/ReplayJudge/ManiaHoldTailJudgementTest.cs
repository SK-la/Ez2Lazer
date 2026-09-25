// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaHoldTailJudgementTest
    {
        [TestCase(EzEnumHitMode.O2Jam)]
        [TestCase(EzEnumHitMode.IIDX_HD)]
        [TestCase(EzEnumHitMode.EZ2AC)]
        public void TestPerfectHoldSessionHasZeroComboBreak(EzEnumHitMode hitMode)
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateHoldPerfectForHitMode(hitMode);
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is TailNote), Is.True);
        }

        [Test]
        public void TestMalodyPerfectHoldSessionHasZeroComboBreak()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateMalodyHoldPerfect();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is HeadNote), Is.True);
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is TailNote), Is.False);
        }

        [Test]
        public void TestEarlyReleaseSessionProducesComboBreak()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2HoldEarlyRelease();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.GreaterThan(0));
        }

        [Test]
        public void TestO2ReleaseInsideTailWindowFollowsLiveSemantics()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2HoldReleaseInsideTailWindow();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            // 尾窗内松手：局内 DrawableHoldNote.OnReleased 先判尾（命中）后写 Body IgnoreHit，不应产生 Miss / ComboBreak。
            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.Miss, out int miss) ? miss : 0, Is.EqualTo(0));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.Not.EqualTo(HitResult.Miss));
        }

        [Test]
        public void TestO2ReleaseOutsideTailWindowBreaksCombo()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2HoldEarlyRelease();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            // 尾窗外松手：局内尾判 Miss 后 Body 补 ComboBreak。
            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.GreaterThan(0));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Miss));
        }

        [Test]
        public void TestTailLateReleaseStoresNonZeroOffset()
        {
            var (score, beatmap, environment) = LazerTapReplayFixtures.CreateSingleHoldLateTailRelease(lateMs: 30);
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.TimeOffset, Is.EqualTo(30).Within(0.01));
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void TestClassicTailLateReleaseUsesClassicWindows()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldLateTailRelease(lateMs: 30);
            var classicEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Classic, EzEnumHealthMode.Lazer);

            var classicTail = ManiaReplaySession.RunHitEvents(score, beatmap, classicEnvironment).Single(e => e.HitObject is TailNote);

            // 与 Lazer 共用同一套 tail 时序/offset 规则；仅窗口区间不同，故 30ms late 在 Classic 下仍判 Great。
            Assert.That(classicTail.TimeOffset, Is.EqualTo(30).Within(0.01));
            Assert.That(classicTail.Result, Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void TestMalodyTailJudgementAfterEnvironmentApply()
        {
            var (_, beatmap, environment) = HitModeReplayFixtures.CreateMalodyHoldPerfect();
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.ManiaHitMode);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            Assert.That(tail.Judgement, Is.TypeOf<MalodyTailJudgement>());
            Assert.That(tail.Judgement.MaxResult, Is.EqualTo(HitResult.IgnoreHit));
        }

        [Test]
        public void TestO2TailJudgementAfterEnvironmentApply()
        {
            var (_, beatmap, environment) = HitModeReplayFixtures.CreateO2HoldPerfect();
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.ManiaHitMode);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            Assert.That(tail.Judgement.MaxResult, Is.EqualTo(HitResult.Perfect));
            Assert.That(tail.Judgement.MinResult, Is.EqualTo(HitResult.Miss));
        }
    }
}
