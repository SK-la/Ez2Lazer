// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaHoldTailJudgementTest
    {
        [TestCase(EzEnumJudgeMode.O2Jam)]
        [TestCase(EzEnumJudgeMode.IIDX_HD)]
        [TestCase(EzEnumJudgeMode.EZ2AC)]
        public void TestPerfectHoldSessionHasZeroComboBreak(EzEnumJudgeMode judgeMode)
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateHoldPerfectForJudgeMode(judgeMode);
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is TailNote), Is.True);
        }

        [Test]
        public void TestMalodyPerfectHoldSessionHasZeroComboBreak()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateMalodyHoldPerfect();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is HeadNote), Is.True);
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is TailNote), Is.False);
        }

        [Test]
        public void TestEarlyReleaseSessionProducesComboBreak()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateO2HoldEarlyRelease();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.GreaterThan(0));
        }

        [Test]
        public void TestO2TailUsesPressTimeBpmOnVariableBpm()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateO2HoldVariableBpmPressTimeWindow();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            // 按下在 60BPM（Cool 窗 125ms）、尾在 120BPM，松手晚 100ms：只有用按下时刻的 BPM 才是 Cool。
            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.TimeOffset, Is.EqualTo(100).Within(0.01));
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Perfect));
        }

        [Test]
        public void TestO2ReleaseInsideTailWindowFollowsLiveSemantics()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateO2HoldReleaseInsideTailWindow();
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
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateO2HoldEarlyRelease();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            // 尾窗外松手：局内尾判 Miss 后 Body 补 ComboBreak。
            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.GreaterThan(0));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Miss));
        }

        /// <summary>
        /// 头命中后中途松手（远早于尾窗）：局内 <c>Column.OnReleased</c> → <c>EzTriggerBodyAfterTailRelease</c>
        /// （尾未判、<c>isEarlyHoldRelease()</c>）→ Body ComboBreak；尾保持未判，交由 auto-miss 收束。
        /// Lazer 与 Classic 共用 <see cref="CommonHoldJudgementStrategy"/>，行为必须一致。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer)]
        [TestCase(EzEnumJudgeMode.Classic)]
        public void TestEarlyHoldReleaseBreaksComboWithoutJudgingTail(EzEnumJudgeMode judgeMode)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldReleaseAt(releaseTime: 2500);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.GreaterThan(0));

            // 松手瞬间不得把尾判成命中（局内窗口外不判尾），尾留给 auto-miss。
            Assert.That(result.ScoreInfo.HitEvents.Where(e => e.HitObject is TailNote).Any(e => e.Result.IsHit()), Is.False);
        }

        /// <summary>
        /// 尾窗内松手：局内先判尾（命中原生结果，头已命中且未断连故不降级）后写 Body IgnoreHit。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer)]
        [TestCase(EzEnumJudgeMode.Classic)]
        public void TestReleaseInsideTailWindowJudgesTailWithoutComboBreak(EzEnumJudgeMode judgeMode)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldReleaseAt(releaseTime: 3950);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Where(e => e.HitObject is TailNote).Any(e => e.Result.IsHit()), Is.True);
        }

        /// <summary>
        /// 断连后重按、再于尾部松手：局内 <c>DrawableHoldNote.OnPressed</c>（列路由未选中目标时仍执行）
        /// 会重新 <c>beginHoldAt</c> 重臂 holding，故松手时 <c>Tail.UpdateResult()</c> 判尾；
        /// 此时 <c>Body.HasHoldBreak</c> 已为真 → 尾判由 Perfect 降级为 <b>Meh</b>（不断尾判、只降级）。
        /// 与 <c>TestSceneHoldNoteInput.TestPressAtStartThenBreakThenRepressAndReleaseAtTail</c> 同口径。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer)]
        [TestCase(EzEnumJudgeMode.Classic)]
        public void TestBreakThenRepressCapsTailToMeh(EzEnumJudgeMode judgeMode)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs(
                (1500, true), (2500, false), (3900, true), (4000, false));
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            // 断连只记一次：重臂后尾判命中，不得再补 Body ComboBreak。
            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(1));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Meh));
        }

        /// <summary>
        /// 单条 LN 中途多次「按下—松开」：stable 语义下每次提前松手都各计一次 ComboBreak。
        /// 覆盖 Lazer / Classic / BMS（<see cref="IManiaHoldJudgementStrategy.AllowsRepeatedHoldBreak"/> 为真）。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer)]
        [TestCase(EzEnumJudgeMode.Classic)]
        [TestCase(EzEnumJudgeMode.IIDX_HD)]
        public void TestRepeatedHoldBreakCountsEachRelease(EzEnumJudgeMode judgeMode)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs(RepeatedBreakInputs);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(RepeatedBreakCount));
        }

        /// <summary>
        /// O2 / EZ2AC / Malody 维持单次（或零次）断连语义，不因公共修复被放宽。
        /// </summary>
        [TestCase(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal, 1)]
        [TestCase(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac, 0)]
        [TestCase(EzEnumJudgeMode.Malody_E, EzEnumHealthMode.Lazer, 0)]
        public void TestRepeatedHoldBreakKeepsSingleForExcludedJudgeModes(EzEnumJudgeMode judgeMode, EzEnumHealthMode healthMode, int expected)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs(RepeatedBreakInputs);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, healthMode);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(expected));
        }

        // 4 次提前松手（均在尾 4000 之前），末尾重新按住过尾收束。
        private static readonly (double time, bool press)[] RepeatedBreakInputs =
        {
            (1500, true), (1650, false),
            (1800, true), (1950, false),
            (2100, true), (2250, false),
            (2400, true), (2550, false),
            (3800, true), (4100, false),
        };

        private const int RepeatedBreakCount = 4;

        /// <summary>
        /// 松手落点越过下一条 LN 的尾开始时间时，前一条尾仍须被判定，且不受 JudgePrecedence 影响
        /// （局内 OnReleased 不经过 note-lock；Earliest 的阻挡规则只用于按下选目标）。
        /// </summary>
        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestReleasePastLaterTailIsJudged(EzEnumJudgePrecedence precedence)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateChainedHoldsReleasePastNextTail();
            var environment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, precedence);

            var tail = ManiaReplaySession.RunHitEvents(score, beatmap, environment)
                                        .Single(e => e.HitObject is TailNote && Math.Abs(e.HitObject.StartTime - 1950) < 1);

            Assert.That(tail.Result, Is.Not.EqualTo(HitResult.Miss));
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
            var classicEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Classic, EzEnumHealthMode.Lazer);

            var classicTail = ManiaReplaySession.RunHitEvents(score, beatmap, classicEnvironment).Single(e => e.HitObject is TailNote);

            // 与 Lazer 共用同一套 tail 时序/offset 规则；仅窗口区间不同，故 30ms late 在 Classic 下仍判 Great。
            Assert.That(classicTail.TimeOffset, Is.EqualTo(30).Within(0.01));
            Assert.That(classicTail.Result, Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void TestMalodyTailJudgementAfterEnvironmentApply()
        {
            var (_, beatmap, environment) = JudgeModeReplayFixtures.CreateMalodyHoldPerfect();
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.JudgeMode);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            Assert.That(tail.Judgement, Is.TypeOf<MalodyTailJudgement>());
            Assert.That(tail.Judgement.MaxResult, Is.EqualTo(HitResult.IgnoreHit));
        }

        [Test]
        public void TestO2TailJudgementAfterEnvironmentApply()
        {
            var (_, beatmap, environment) = JudgeModeReplayFixtures.CreateO2HoldPerfect();
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.JudgeMode);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            Assert.That(tail.Judgement.MaxResult, Is.EqualTo(HitResult.Perfect));
            Assert.That(tail.Judgement.MinResult, Is.EqualTo(HitResult.Miss));
        }
    }
}
