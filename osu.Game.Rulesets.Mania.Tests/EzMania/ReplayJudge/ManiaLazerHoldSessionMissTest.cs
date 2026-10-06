// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// Lazer hitmode 下 LN 尾在 Session 里的结算：窗口内松手 → 命中；无窗口内松手边沿 → 尾 Miss（对齐上游被动 miss）。
    /// </summary>
    /// <remarks>
    /// 这些是绝对期望，不是 Drawable/Session parity——parity 只保证两边一致，两边同错时依然会绿。
    /// 局内（Drawable）侧的对应缺陷见 <c>TestSceneReplaySessionParity</c> 的 LN 尾用例。
    /// </remarks>
    [TestFixture]
    public class ManiaLazerHoldSessionMissTest
    {
        [TearDown]
        public void TearDown()
        {
            ReplayJudgeTestConfig.ResetGlobalConfig();
        }

        /// <summary>
        /// 单条 LN：head 按下、tail 准时松手 —— 不得有 Miss。
        /// </summary>
        [Test]
        public void TestSingleCleanHoldHasNoMisses()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1400),
                new ManiaReplayFrame(3000),
            };

            assertMisses(hitObjects, frames, 0);
        }

        /// <summary>
        /// 同列多段 LN 依次按下/松手 —— 不得有 Miss。
        /// </summary>
        [Test]
        public void TestSequentialHoldsHaveNoMisses()
        {
            var hitObjects = new List<HitObject>();

            for (int i = 0; i < 8; i++)
            {
                double start = 1000 + i * 600;
                hitObjects.Add(new HoldNote { StartTime = start, Duration = 400, Column = 0 });
            }

            var frames = new List<ReplayFrame>();

            for (int i = 0; i < 8; i++)
            {
                double start = 1000 + i * 600;
                frames.Add(new ManiaReplayFrame(start, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(start + 400));
            }

            frames.Add(new ManiaReplayFrame(8000));

            assertMisses(hitObjects, frames, 0);
        }

        /// <summary>
        /// LN 与同列 tap 交替 —— 不得有 Miss。
        /// </summary>
        [Test]
        public void TestHoldFollowedByTapHasNoMisses()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
                new Note { StartTime = 1800, Column = 0 },
                new Note { StartTime = 2200, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1400),
                new ManiaReplayFrame(1800, ManiaAction.Key1),
                new ManiaReplayFrame(1900),
                new ManiaReplayFrame(2200, ManiaAction.Key1),
                new ManiaReplayFrame(2300),
                new ManiaReplayFrame(4000),
            };

            assertMisses(hitObjects, frames, 0);
        }

        /// <summary>
        /// 锚定尾判的「有效阈值」：上游把 release lenience 体现在 offset 上
        /// （<see cref="TailNote.RELEASE_WINDOW_LENIENCE"/>，Lazer 阈值 ≈ WindowFor(Meh) × 1.5）。
        /// 落在阈值内必须命中，落在阈值外必须 Miss；N 若少乘这个 lenience，就是「局内命中、重算 Miss」的系统性多算。
        /// </summary>
        [TestCase(100, 0)]
        [TestCase(150, 0)]
        [TestCase(180, 0)]
        [TestCase(210, 1)]
        public void TestTailReleaseLenienceBoundary(double lateBy, int expectedMisses)
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1400 + lateBy),
                new ManiaReplayFrame(3000),
            };

            assertMisses(hitObjects, frames, expectedMisses);
        }

        /// <summary>
        /// tail 稍晚松手（仍在 release lenience 内）—— 不得有 Miss。
        /// </summary>
        [Test]
        public void TestLateTailReleaseWithinLenienceHasNoMisses()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1500),
                new ManiaReplayFrame(3000),
            };

            assertMisses(hitObjects, frames, 0);
        }

        /// <summary>
        /// tail 之后迟于 release lenience 才松手：尾按上游被动 miss 收束为 Miss。
        /// </summary>
        [Test]
        public void TestVeryLateTailReleaseMarksTailMiss()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1800),
                new ManiaReplayFrame(3000),
            };

            assertMisses(hitObjects, frames, 1, expectTailMiss: true);
        }

        /// <summary>
        /// 整局按住不松手：尾同样按被动 miss 收束为 Miss（不得悬空不判）。
        /// </summary>
        [Test]
        public void TestHoldThroughNeverReleasedMarksTailMiss()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(3000, ManiaAction.Key1),
            };

            assertMisses(hitObjects, frames, 1, expectTailMiss: true);
        }

        /// <summary>
        /// 同列两段 LN 一直按住：两个尾各 Miss；第二个 head 无按下、Miss 属正确。
        /// </summary>
        [Test]
        public void TestChainedHoldsSingleContinuousHoldMarksTailMiss()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
                new HoldNote { StartTime = 1600, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(3000, ManiaAction.Key1),
            };

            assertMisses(hitObjects, frames, 3, expectTailMiss: true);
        }

        /// <summary>
        /// 同列前一个物件的松手不得用来判后面那条 LN 的尾：尾的提前侧候选窗约为
        /// <c>WindowFor(Miss) × RELEASE_WINDOW_LENIENCE</c>，局内松手只判「此刻正按住的那条 LN」
        /// （Column.OnReleased → LaneController.ActiveHold），不会按时间邻近去够未来的尾。
        /// </summary>
        [Test]
        public void TestEarlierNoteReleaseDoesNotJudgeFutureTail()
        {
            var hitObjects = new List<HitObject>
            {
                new Note { StartTime = 1000, Column = 0 },
                new HoldNote { StartTime = 1120, Duration = 100, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1100),
                new ManiaReplayFrame(1120, ManiaAction.Key1),
                new ManiaReplayFrame(1220),
                new ManiaReplayFrame(3000),
            };

            var events = runEvents(hitObjects, frames);
            var tail = events.Single(e => e.HitObject is TailNote);

            Assert.That(events.Count(e => e.Result == HitResult.Miss), Is.EqualTo(0),
                () => $"release of the earlier note must not miss the future tail: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
            Assert.That(tail.Result, Is.EqualTo(HitResult.Perfect),
                () => $"tail must be judged by its own release: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
        }

        /// <summary>
        /// 提前松手但落在 release lenience 内：局内 OnReleased 先判尾、后写 Body，这一投看到的 Body 断连仍是 false，
        /// 因此不得被压成 Meh（<c>HoldNoteBody.HasHoldBreak</c> 只在真的断连后成立）。
        /// </summary>
        [Test]
        public void TestEarlyReleaseWithinLenienceIsNotCappedToMeh()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1340),
                new ManiaReplayFrame(3000),
            };

            var events = runEvents(hitObjects, frames);
            var tail = events.Single(e => e.HitObject is TailNote);

            Assert.That(tail.Result.IsHit(), Is.True,
                () => $"early release inside lenience must be a hit: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
            Assert.That(tail.Result, Is.Not.EqualTo(HitResult.Meh),
                () => $"early release inside lenience must not be capped to Meh: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
        }

        /// <summary>
        /// 断连后同列按下若已路由到 tap，局内 <c>ShouldSkipColumnRoutedPress</c> 会跳过 Hold 重臂；
        /// 随后在尾点松手不得把尾救成 Meh，尾应走被动 Miss。
        /// </summary>
        [Test]
        public void TestPressHittingNoteAfterBreakDoesNotRearmHold()
        {
            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
                new Note { StartTime = 1300, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1001),
                new ManiaReplayFrame(1300, ManiaAction.Key1),
                new ManiaReplayFrame(1400),
                new ManiaReplayFrame(3000),
            };

            var events = runEvents(hitObjects, frames);
            var note = events.Single(e => e.HitObject is Note and not HeadNote and not TailNote);
            var tail = events.Single(e => e.HitObject is TailNote);

            Assert.That(note.Result.IsHit(), Is.True,
                () => $"tap after break must still hit: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
            Assert.That(tail.Result, Is.EqualTo(HitResult.Miss),
                () => $"tail must not be rearmed by tap press: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
        }

        /// <summary>
        /// 局内 ForceMiss 条目不含 Tail（TryCreateEntry 拒绝 Head/Tail）。同列后一条 LN 尾命中时，
        /// 不得把前一条尚未 auto-miss、且已落在 raw CanBeHit 外（但仍在 release-lenience / 被动窗内）的尾 ForceMiss 掉。
        /// </summary>
        [Test]
        public void TestLaterTailHitDoesNotForceMissEarlierUnjudgedTail()
        {
            var probe = new HoldNote { StartTime = 1000, Duration = 400, Column = 0 };
            probe.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            double meh = probe.Tail.HitWindows.WindowFor(HitResult.Meh);
            double gapStart = probe.EndTime + meh + 1;
            double gapEnd = probe.EndTime + meh * TailNote.RELEASE_WINDOW_LENIENCE - 1;

            Assume.That(gapEnd > gapStart, "need a non-empty CanBeHit-fail / pre-auto-miss gap for Tail");

            double laterTailTime = (gapStart + gapEnd) / 2;
            double laterHeadTime = laterTailTime - 80;
            Assume.That(laterHeadTime > probe.EndTime, "later hold must start after earlier tail");

            var hitObjects = new List<HitObject>
            {
                new HoldNote { StartTime = 1000, Duration = 400, Column = 0 },
                new HoldNote { StartTime = laterHeadTime, Duration = laterTailTime - laterHeadTime, Column = 0 },
            };

            // 前一条：按下后立刻远早松手 → Body 断连，尾留给被动窗；后一条干净松手命中尾。
            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1000 + 1),
                new ManiaReplayFrame(laterHeadTime, ManiaAction.Key1),
                new ManiaReplayFrame(laterTailTime),
                new ManiaReplayFrame(laterTailTime + 2000),
            };

            var events = runEvents(hitObjects, frames);
            var tails = events.Where(e => e.HitObject is TailNote).OrderBy(e => e.HitObject.StartTime).ToList();

            Assert.That(tails, Has.Count.EqualTo(2),
                () => $"expected two tail judgements: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
            Assert.That(tails[1].Result.IsHit(), Is.True,
                () => $"later tail must hit: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");

            // ForceMissEarlier 会用后尾命中时刻写 stored offset；auto-miss 用自身 deadline / 按键史，两者可分。
            double forceMissOffset = laterTailTime - tails[0].HitObject.StartTime;
            Assert.That(tails[0].TimeOffset, Is.Not.EqualTo(forceMissOffset).Within(0.5),
                () => $"earlier tail must not be force-missed at later-tail hit ({laterTailTime}): [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
            Assert.That(tails[0].Result, Is.EqualTo(HitResult.Miss),
                () => $"earlier tail must still settle as Miss via auto-miss: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
        }

        private static void assertMisses(List<HitObject> hitObjects, List<ReplayFrame> frames, int expectedMisses,
                                         bool expectTailMiss = false)
        {
            var events = runEvents(hitObjects, frames);
            var missEvents = events.Where(e => e.Result == HitResult.Miss).ToList();

            Assert.That(missEvents.Count, Is.EqualTo(expectedMisses),
                () => $"expected {expectedMisses} miss(es): [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");

            if (expectTailMiss)
            {
                // 尾必须被结算（有判决），而不是悬空不判——悬空会让局内 HasCompleted 永假。
                Assert.That(missEvents.Any(e => e.HitObject is TailNote), Is.True,
                    () => $"tail must be settled as miss: [{ManiaReplayParityHelper.DescribeHitEvents(events)}]");
            }
        }

        private static List<HitEvent> runEvents(List<HitObject> hitObjects, List<ReplayFrame> frames)
        {
            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var ruleset = new ManiaRuleset();

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = new ControlPointInfo(),
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay { Frames = frames };

            var score = new Score
            {
                ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
                Replay = replay,
            };

            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            return ManiaReplaySession.RunHitEvents(score, beatmap, environment).ToList();
        }
    }
}
