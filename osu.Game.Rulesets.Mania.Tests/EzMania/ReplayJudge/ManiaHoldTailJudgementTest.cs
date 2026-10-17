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
        /// Lazer 走 <see cref="CommonHoldJudgementStrategy"/>。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer)]
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
        /// 会重新 <c>beginHoldAt</c> 重臂 holding，故松手时 <c>Tail.UpdateResult()</c> 判尾。
        /// stable 的三条档都落 <b>Meh</b>：按下基准在每次按下边沿被覆盖成重按时刻，
        /// 尾判的按下误差是「重按 − 头」（此例 2400ms），先掉出全部窗口；断连封顶只降不升，
        /// 接不住已经低于下限的结果。
        /// 与 <c>TestSceneHoldNoteInput.TestPressAtStartThenBreakThenRepressAndReleaseAtTail</c> 同口径。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer, HitResult.Meh)]
        [TestCase(EzEnumJudgeMode.StableClassic, HitResult.Meh)]
        public void TestBreakThenRepressCapsTailToMeh(EzEnumJudgeMode judgeMode, HitResult expected)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs(
                (1500, true), (2500, false), (3900, true), (4000, false));
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            // 断连只记一次：重臂后尾判命中，不得再补 Body ComboBreak。
            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(1));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.EqualTo(expected));
        }

        /// <summary>
        /// 单条 LN 中途多次「按下—松开」：stable 语义下每次提前松手都各计一次 ComboBreak。
        /// 覆盖 Lazer / BMS（<see cref="IManiaHoldJudgementStrategy.AllowsRepeatedHoldBreak"/> 为真）。
        /// </summary>
        [TestCase(EzEnumJudgeMode.Lazer)]
        [TestCase(EzEnumJudgeMode.IIDX_HD)]
        public void TestRepeatedHoldBreakCountsEachRelease(EzEnumJudgeMode judgeMode)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs(repeated_break_inputs);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(repeated_break_count));
        }

        /// <summary>
        /// O2 / EZ2AC / Malody 维持单次（或零次）断连语义，不因公共修复被放宽。
        /// </summary>
        [TestCase(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal, 1)]
        [TestCase(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac, 0)]
        [TestCase(EzEnumJudgeMode.Malody_E, EzEnumHealthMode.Lazer, 0)]
        public void TestRepeatedHoldBreakKeepsSingleForExcludedJudgeModes(EzEnumJudgeMode judgeMode, EzEnumHealthMode healthMode, int expected)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs(repeated_break_inputs);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, healthMode);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(expected));
        }

        // 4 次提前松手（均在尾 4000 之前），末尾重新按住过尾收束。
        private static readonly (double time, bool press)[] repeated_break_inputs =
        {
            (1500, true), (1650, false),
            (1800, true), (1950, false),
            (2100, true), (2250, false),
            (2400, true), (2550, false),
            (3800, true), (4100, false),
        };

        private const int repeated_break_count = 4;

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

        [TestCase(EzEnumJudgeMode.StableClassic, HitResult.Perfect)]
        public void TestStableClassicTailLateReleaseUsesStableWindows(EzEnumJudgeMode judgeMode, HitResult expected)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldLateTailRelease(lateMs: 30);
            var environment = ReplayJudgeTestConfig.Create(judgeMode, EzEnumHealthMode.Lazer);

            var tail = ManiaReplaySession.RunHitEvents(score, beatmap, environment).Single(e => e.HitObject is TailNote);

            // stable classic 长条按「按下误差 + 合计误差」联合取档，30ms late 落在 300g*2.4 内，判 300g（Perfect）。
            Assert.That(tail.TimeOffset, Is.EqualTo(30).Within(0.01));
            Assert.That(tail.Result, Is.EqualTo(expected));
        }

        /// <summary>
        /// 从未按下：过 <c>StartTime + HitWindow50</c> 立即断连，只落 Body ComboBreak，尾仍留到
        /// <c>EndTime + HitWindow100</c> 判 Miss。断连早于后续单键，故那条单键的连击从 1 起算。
        /// </summary>
        [Test]
        public void TestStableClassicNeverPressedBreaksAtStartWindow()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateUnpressedHoldWithNotes(
                holdStart: 1500, holdEnd: 4000, firstNote: 1550, secondNote: 2500, frameUntil: 4300);

            var environment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            var bodyEvents = result.ScoreInfo.HitEvents.Where(e => e.HitObject is HoldNoteBody).ToList();
            Assert.That(bodyEvents, Has.Count.EqualTo(1), "LN 只补一次 Body 断连");
            Assert.That(bodyEvents[0].Result, Is.EqualTo(HitResult.ComboBreak));

            // 断连落在 1550 与 2500 之间：后一枚单键重新从 1 起算，中间没有跨过断连的连击。
            Assert.That(result.ScoreInfo.MaxCombo, Is.EqualTo(1));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Miss));
        }

        /// <summary>
        /// 晚于 <c>StartTime + HitWindow50</c> 才按下：断连已在到点帧发生，重抓不撤销断连，
        /// 尾档由按下误差（重抓 − 头）掉档并由断连标志封顶。
        /// </summary>
        [Test]
        public void TestStableClassicLatePressStillBreaksAtStartWindow()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldWithInputs((1650, false), (1700, true), (4000, false));
            var environment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(1));

            var tailEvent = result.ScoreInfo.HitEvents.Single(e => e.HitObject is TailNote);
            Assert.That(tailEvent.Result, Is.EqualTo(HitResult.Meh));
        }

        /// <summary>
        /// 按住过尾不松手时，到点判尾被推迟到 <c>EndTime + HitWindow50</c>
        /// （本谱 OD6 → 133ms），并取帧钟首个不早于阈值的帧落判。
        /// </summary>
        [TestCase(0, 140)]
        [TestCase(5, 135)]
        public void TestStableClassicHeldPastTailSettlesOnFrameClock(double framePhase, double expectedOffset)
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldHeldPastTail(holdUntil: 6000, framePhase: framePhase);
            var stableClassicEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            var stableClassicTail = ManiaReplaySession.RunHitEvents(score, beatmap, stableClassicEnvironment).Single(e => e.HitObject is TailNote);

            // 按住档不落在「已松手」档的 EndTime + HitWindow100（OD6 → 109ms）上。
            Assert.That(stableClassicTail.TimeOffset, Is.GreaterThan(tail.HitWindows!.WindowFor(HitResult.Ok)));

            // 按下基准落在段首、松开基准 = 落判帧钟 → 合计误差 = 偏差；
            // 140/135 越出 200 窗（79*2）但仍在 100 窗（109*2）内 → 100。
            Assert.That(stableClassicTail.TimeOffset, Is.EqualTo(expectedOffset).Within(0.01));
            Assert.That(stableClassicTail.Result, Is.EqualTo(HitResult.Good));
        }

        /// <summary>
        /// stable classic 长条持有期每 100ms 涨一格连击，节拍锚在按下时刻而不是帧时刻。
        /// </summary>
        [Test]
        public void TestStableClassicHeldPastTailRaisesHoldComboPerInterval()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldHeldPastTail(holdUntil: 6000);
            var stableClassicEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var result = ManiaReplaySession.Run(score, beatmap, stableClassicEnvironment);

            // 帧钟 1500 起每 10ms 一帧、段末 4000：1600/1700/…/4000 共 25 格，
            // 再加段末判尾命中 1 格；头（IgnoreHit）与 Body 不进连击。
            Assert.That(result.ScoreInfo.MaxCombo, Is.EqualTo(26));
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
