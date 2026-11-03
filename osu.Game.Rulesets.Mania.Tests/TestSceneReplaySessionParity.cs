// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    /// <summary>
    /// Drawable replay 路径与 <see cref="ManiaReplaySession"/> 的 parity（Lazer + Ez JudgeMode）。
    /// 高精度夹具用 <see cref="ManiaReplayJsonFixture"/>（JSON double ms）；勿用 osr 验局内。
    /// </summary>
    public partial class TestSceneReplaySessionParity : RateAdjustedBeatmapTestScene
    {
        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private IReadOnlyList<HitEvent> drawableHitEvents = null!;
        private IBeatmap playableBeatmap = null!;
        private Score replayScore = null!;
        private ScoreInfo recalculatedScoreInfo = null!;
        private GameplayEnvironment parityEnvironment = null!;

        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, false);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaJudgeMode, EzEnumJudgeMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, EzEnumHealthMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, EzEnumJudgePrecedence.Earliest);
        }

        [Test]
        public void TestLazerTapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(900, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(1900, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                });
        }

        /// <summary>
        /// 高精度 JSON 回放（亚毫秒）→ Drawable ≡ Session。不用 osr（整数 ms）。
        /// </summary>
        [Test]
        public void TestJsonLazerTwoNoteTapDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/Lazer-two-note-tap.json");

        [Test]
        public void TestJsonLazerHoldBreakRepressMehDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/Lazer-hold-break-repress-meh.json");

        [Test]
        public void TestJsonLazerJackAutoMissDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/Lazer-jack-auto-miss.json");

        /// <summary>
        /// 全谱：JSON 帧 + 嵌入 .osu（OD/timing）。帧源为 Lazer osr 解码 double（整数档）；
        /// 亚毫秒局内抓帧后可覆盖同路径 JSON。
        /// Visual ≈5min，用 <see cref="LongUntilStepButton"/>（10min）；CI 默认不跑。
        /// CI Session smoke：<see cref="ManiaReplayJsonFixtureTest.TestAuditChartSessionRunsFromJson"/>。
        /// </summary>
        [Test]
        [Explicit("全谱 Visual ≈5–10min；手动跑 Drawable≡Session（LongUntilStep 10min）。")]
        public void TestJsonLazerHanatachiDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/Lazer-Hanatachi.json", longWait: true);

        [Test]
        [Explicit("全谱 Visual ≈5–10min；手动跑 Drawable≡Session（LongUntilStep 10min）。")]
        public void TestJsonLazerPORTRAiTDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/Lazer-PORTRAiT.json", longWait: true);

        /// <summary>
        /// 真实全谱换 StableClassic 档：帧与 <see cref="TestJsonLazerHanatachiDrawableMatchesSession"/> 同源
        /// （osr 整数 ms），只换判定档位，验局内 ≡ Session 是否仍然同源。
        /// </summary>
        [Test]
        [Explicit("全谱 Visual ≈5–10min；手动跑 Drawable≡Session（LongUntilStep 10min）。")]
        public void TestJsonStableClassicHanatachiDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/StableClassic-Hanatachi.json", longWait: true);

        [Test]
        [Explicit("全谱 Visual ≈5–10min；手动跑 Drawable≡Session（LongUntilStep 10min）。")]
        public void TestJsonStableClassicPORTRAiTDrawableMatchesSession()
            => runDrawableParityFromJson("Resources/Testing/ReplayJson/StableClassic-PORTRAiT.json", longWait: true);

        [Test]
        public void TestLazerHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote
                    {
                        StartTime = head,
                        Duration = tail - head,
                        Column = 0,
                    },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        /// <summary>
        /// LN 尾之后明显迟松手（松手落在 release lenience 之外）：Drawable 与 Session 必须一致。
        /// </summary>
        [Test]
        public void TestLazerHoldTailReleasedOutsideWindowDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 2500;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote
                    {
                        StartTime = head,
                        Duration = tail - head,
                        Column = 0,
                    },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail + 400),
                });
        }

        /// <summary>
        /// 整局按住 LN 不松手（回放结束时仍按住）：Drawable 与 Session 必须一致。
        /// </summary>
        [Test]
        public void TestLazerHoldTailNeverReleasedDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 2500;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote
                    {
                        StartTime = head,
                        Duration = tail - head,
                        Column = 0,
                    },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail + 2000, ManiaAction.Key1),
                });
        }

        [Test]
        public void TestIidxTapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                });
        }

        [Test]
        public void TestIidxHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestO2TapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                });
        }

        [Test]
        public void TestEz2AcManyNoteTapDrawableMatchesSession()
        {
            runDrawableParityTestFromFixture(JudgeModeReplayFixtures.CreateEz2AcManyNoteTap());
        }

        [Test]
        public void TestMalodyETapDrawableMatchesSession()
        {
            runDrawableParityTestFromFixture(JudgeModeReplayFixtures.CreateMalodyManyNoteTap());
        }

        [Test]
        public void TestO2VariableBpmTapDrawableMatchesSession()
        {
            runDrawableParityTestFromFixture(JudgeModeReplayFixtures.CreateO2VariableBpmTap());
        }

        [Test]
        public void TestMalodyHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Malody_E, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestO2HoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestO2PillDrawableMatchesSession()
        {
            var (score, beatmap, environment) = JudgeModeReplayFixtures.CreateO2PillUpgradeOnBadRange();
            parityEnvironment = environment;

            runDrawableParityTest(
                beatmap.HitObjects.Cast<ManiaHitObject>().ToList(),
                score.Replay.Frames);
        }

        [Test]
        public void TestEz2AcHoldDrawableMatchesSession()
        {
            runDrawableParityTestFromFixture(JudgeModeReplayFixtures.CreateEz2AcHoldThroughEnd());
        }

        // ==================== POLICY-PARITY: JudgePrecedence × 叠键 ====================

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestLazerOverlappingJackDrawableMatchesSession(EzEnumJudgePrecedence precedence)
        {
            var fixture = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer, precedence);
            runDrawableParityTestFromFixture(fixture);
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestIidxOverlappingJackDrawableMatchesSession(EzEnumJudgePrecedence precedence)
        {
            var fixture = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, precedence);
            runDrawableParityTestFromFixture(fixture);
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestIidxOverlappingJackDrawableMatchesSession_LazerHealth(EzEnumJudgePrecedence precedence)
        {
            var fixture = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.Lazer, precedence);
            runDrawableParityTestFromFixture(fixture);
        }

        // ==================== P2-B: Parity 扩到完整 Score ====================

        [Test]
        public void TestFullScoreParity_LazerTap()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                    new Note { StartTime = 3000, Column = 1 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                    new ManiaReplayFrame(3000, ManiaAction.Key2),
                    new ManiaReplayFrame(3100),
                    new ManiaReplayFrame(4000, ManiaAction.Key2),
                    new ManiaReplayFrame(4100),
                });
        }

        [Test]
        public void TestFullScoreParity_IidxHold()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            const double head = 1500;
            const double tail = 4000;

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 5000, Column = 1 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                    new ManiaReplayFrame(5000, ManiaAction.Key2),
                    new ManiaReplayFrame(5100),
                });
        }

        /// <summary>
        /// [Ez] 中途松手 LN：EZ2AC 现为 tick IgnoreMiss（不断 combo），尾 Ignore；与 Session 全量 Statistics 对齐。
        /// </summary>
        [Test]
        public void TestFullScoreParity_Ez2AcBrokenHold()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            const double head = 1000;
            const double tail = 3000;

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(1500), // 提早松手 → tick IgnoreMiss，不断 combo
                    new ManiaReplayFrame(4000, ManiaAction.Key2),
                    new ManiaReplayFrame(4100),
                });
        }

        /// <summary>
        /// [Ez] 断连后重按：再抓后 tick 恢复涨 combo。
        /// </summary>
        [Test]
        public void TestFullScoreParity_Ez2AcBrokenHoldRepress()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            const double head = 1000;
            const double tail = 3000;

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(1500), // 中途断连
                    new ManiaReplayFrame(2000, ManiaAction.Key1), // 重按
                    new ManiaReplayFrame(tail + 200), // 持过尾再松
                    new ManiaReplayFrame(4000, ManiaAction.Key2),
                    new ManiaReplayFrame(4100),
                });
        }

        /// <summary>
        /// [Ez] 断连 LN（提早松手、不重按）：显式断言 JudgedHits 与 MaximumJudgements 一致，覆盖结算门闩。
        /// </summary>
        [Test]
        public void TestFullScoreParity_Ez2AcBrokenHoldNoRepress()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            const double head = 1000;
            const double tail = 3000;

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(1500), // 提早松手 → hold break，尾判 miss
                    new ManiaReplayFrame(4000, ManiaAction.Key2),
                    new ManiaReplayFrame(4100),
                },
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] 持满过尾再松：尾为 IgnoreHit，不应再因晚松 Fail；与 Session 对齐并能正常收束。
        /// </summary>
        [Test]
        public void TestFullScoreParity_Ez2AcLateTailRelease()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            const double head = 1000;
            const double tail = 3000;

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 5000, Column = 1 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(3500), // 持过尾再松
                    new ManiaReplayFrame(5000, ManiaAction.Key2),
                    new ManiaReplayFrame(5100),
                },
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] StableClassic 断连 LN（提早松手、不重按）：尾仍须出判决且与 Session 全量一致，
        /// 覆盖结算门闩——悬空不判会让局内 <c>HasCompleted</c> 永假，局内无法进入结算。
        /// </summary>
        /// <remarks>
        /// 帧钟按 100ms 铺满：stable classic 的尾判挂在帧钟上（<c>EndTime + HitWindow100</c>），
        /// 稀疏帧（如只在按键边沿取帧）会把这枚尾判压到下一枚帧，从而与后一个物件的落判顺序互换，
        /// 总分随之不同——那是帧密度造成的，不是判定语义的差异。
        /// </remarks>
        [Test]
        public void TestFullScoreParity_StableClassicBrokenHoldNoRepress()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            const double head = 1000;
            const double tail = 3000;

            var frames = new List<ReplayFrame>();

            for (double time = head; time <= 1500; time += 100)
                frames.Add(new ManiaReplayFrame(time, ManiaAction.Key1));

            for (double time = 1600; time <= 3900; time += 100)
                frames.Add(new ManiaReplayFrame(time));

            frames.Add(new ManiaReplayFrame(4000, ManiaAction.Key2));
            frames.Add(new ManiaReplayFrame(4100));

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                frames,
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] StableClassic 断连后重按同一条 LN：重按要刷新按下基准（尾判取档用），
        /// 局内与 Session 必须同源，不得只在 Session 侧喂这条边沿。
        /// </summary>
        /// <remarks>
        /// 帧钟 10ms：stable classic 的尾判挂在帧钟上，落判时刻迟于 live 一个帧长就会换档，
        /// 100ms 帧距会让局内/ Session 落在不同档（见 <see cref="TestFullScoreParity_StableClassicHeldPastTail"/>）。
        /// </remarks>
        [Test]
        public void TestFullScoreParity_StableClassicBrokenHoldRepress()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            const double head = 1000;
            const double tail = 3000;

            var frames = new List<ReplayFrame>();

            for (double time = head; time <= 3400; time += 10)
            {
                // 1510..1690 松手造成断连，1700 起重按并持过尾。
                bool released = time >= 1510 && time <= 1690;
                frames.Add(released ? new ManiaReplayFrame(time) : new ManiaReplayFrame(time, ManiaAction.Key1));
            }

            frames.Add(new ManiaReplayFrame(3500));
            frames.Add(new ManiaReplayFrame(4000, ManiaAction.Key2));
            frames.Add(new ManiaReplayFrame(4100));

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                frames,
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] StableClassic 单键的到点 Miss 边界是 <c>EndTime + HitWindow100</c>（不是 50 窗）：
        /// 落在边界之后、50 窗之内的按下在局内与 Session 两边都必须已被动判成 Miss。
        /// </summary>
        [Test]
        public void TestFullScoreParity_StableClassicTapPastWindow100()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var frames = new List<ReplayFrame>();

            // OD5：100 窗 112ms、50 窗 136ms。1130 的按下只可能落在「已被动 Miss」之后。
            for (double time = 900; time <= 1300; time += 10)
                frames.Add(time == 1130 ? new ManiaReplayFrame(time, ManiaAction.Key1) : new ManiaReplayFrame(time));

            runFullScoreParityTest(
                new List<ManiaHitObject> { new Note { StartTime = 1000, Column = 0 } },
                frames,
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] StableClassic 按住过尾不松手：尾落在帧钟的 <c>EndTime + HitWindow50</c> 上，
        /// 局内同样必须收束，不得因「持过尾」而永不结算；同时与 Session 全量一致。
        /// </summary>
        [Test]
        public void TestFullScoreParity_StableClassicHeldPastTail()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            const double head = 1000;
            const double tail = 3000;

            var frames = new List<ReplayFrame>();

            // 10ms 帧钟铺到 3400：stable classic 的尾判挂在帧钟上（EndTime + HitWindow50），
            // 落判时刻迟于 live 一个帧长就会换档（Good → Ok），故帧钟要与局内渲染步长同量级。
            for (double time = head; time <= 3400; time += 10)
                frames.Add(new ManiaReplayFrame(time, ManiaAction.Key1));

            frames.Add(new ManiaReplayFrame(3500));
            frames.Add(new ManiaReplayFrame(4000, ManiaAction.Key2));
            frames.Add(new ManiaReplayFrame(4100));

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                frames,
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] StableClassic 整条 LN 从未按下：头尾都不给分，但仍须各出判决（头 IgnoreMiss、尾 Miss），
        /// 否则局内结算门闩永不成立；局内与 Session 的 acc / 总分必须一致。
        /// </summary>
        /// <remarks>
        /// 头的判定被压成 <see cref="HitResult.IgnoreMiss"/>（stable 的 LN 头不进 OnHit），
        /// 故这里也在守「头绑定 IgnoreJudgement 之后，被动 Miss 不得再落 <see cref="HitResult.Miss"/>」。
        /// </remarks>
        [Test]
        public void TestFullScoreParity_StableClassicHeadNeverPressed()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            const double head = 1000;
            const double tail = 3000;

            var frames = new List<ReplayFrame>();

            for (double time = head; time <= 3900; time += 100)
                frames.Add(new ManiaReplayFrame(time));

            frames.Add(new ManiaReplayFrame(4000, ManiaAction.Key2));
            frames.Add(new ManiaReplayFrame(4100));

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 4000, Column = 1 },
                },
                frames,
                assertJudgedHitsComplete: true);
        }

        /// <summary>
        /// [Ez] StableClassic 整条 LN 从未按下：过 <c>StartTime + HitWindow50</c> 即断连，
        /// 断连早于后续单键，故那条单键的连击从 1 起算；局内与 Session 必须一致。
        /// </summary>
        [Test]
        public void TestFullScoreParity_StableClassicUnpressedHoldBreaksEarly()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            const double head = 1000;
            const double tail = 3000;

            var frames = new List<ReplayFrame>();

            for (double time = head; time <= 3900; time += 10)
            {
                bool press = time == 1050 || time == 2500;
                frames.Add(press ? new ManiaReplayFrame(time, ManiaAction.Key2) : new ManiaReplayFrame(time));
            }

            frames.Add(new ManiaReplayFrame(3920));
            frames.Add(new ManiaReplayFrame(3960));

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                    new Note { StartTime = 1050, Column = 1 },
                    new Note { StartTime = 2500, Column = 1 },
                },
                frames,
                assertJudgedHitsComplete: true,
                expectedMaxCombo: 1);
        }

        /// <summary>
        /// [Ez] StableClassic 长条持有的 100ms 连击增量必须由局内产出：stable 的 <c>Holding</c> 每 100ms
        /// 推一次 combo（不加分、不进判定计数），局内缺这一路时 MaxCombo 会少一大截。
        /// </summary>
        /// <remarks>
        /// LN 结束时间刻意落在连击网格后 90ms（1000 按住 → 1100/1200/…/3000 共 20 枚），
        /// 让最后一枚的容差远大于帧抖动；尾判在 3100 松手，两边都走镜像档位。
        /// </remarks>
        [Test]
        public void TestFullScoreParity_StableClassicHoldComboTicks()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var hitObjects = new List<ManiaHitObject>
            {
                new HoldNote { StartTime = hold_start, Duration = hold_duration, Column = 0 },
            };

            var notes = new List<Note>();

            for (int i = 0; i < 8; i++)
            {
                var note = new Note { StartTime = 4000 + i * 100, Column = i % 3 + 1 };
                notes.Add(note);
                hitObjects.Add(note);
            }

            runFullScoreParityTest(
                hitObjects,
                buildStableClassicHoldFrames(hitObjects, note => note == notes[3] ? 45 : 0, 4800),
                assertJudgedHitsComplete: true,
                overallDifficulty: 8,
                expectedMaxCombo: 8 + hold_tick_count + 1);
        }

        /// <summary>
        /// [Ez] StableClassic 持有增量把 combo 推过 384 之后，384 重置的落点必须局内与 Session 同点：
        /// 少了增量会让重置晚到 20 个判定，TotalScore 与 MaxCombo 同时分叉。
        /// </summary>
        [Test]
        public void TestFullScoreParity_StableClassicHoldComboTicksCrossComboBonus()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var hitObjects = new List<ManiaHitObject>
            {
                new HoldNote { StartTime = hold_start, Duration = hold_duration, Column = 0 },
            };

            // 每列每 240ms 一枚，远大于 OD8 的 Miss 窗（164ms），避免按键被路由到相邻物件。
            // 每 5 枚偏 45ms（Good200）把连击加成压下去，使 384 重置真正改变分数。
            for (int i = 0; i < 380; i++)
                hitObjects.Add(new Note { StartTime = 4000 + i * 60, Column = i % 4 });

            runFullScoreParityTest(
                hitObjects,
                buildStableClassicHoldFrames(hitObjects, note => note.StartTime % 300 == 0 ? 45 : 0, 4000 + 380 * 60 + 800),
                assertJudgedHitsComplete: true,
                overallDifficulty: 8,
                expectedMaxCombo: 380 + hold_tick_count + 1,
                longWait: true);
        }

        private const double hold_start = 1000;
        private const double hold_end = 3090;
        private const double hold_duration = hold_end - hold_start;
        private const int hold_tick_count = 20;

        /// <summary>
        /// 帧源：按住段 + (物件时间 + 偏移) 的按下/松开边沿。边沿时刻各出一帧，避免相邻物件
        /// 的松开与按下落进同一帧而被合并成一个持按（会吞掉后一个物件）。
        /// </summary>
        private static List<ReplayFrame> buildStableClassicHoldFrames(List<ManiaHitObject> hitObjects, Func<Note, double> offsetFor, double endTime)
        {
            var transitions = new List<(double time, ManiaAction action, bool press)>
            {
                (hold_start, ManiaAction.Key1, true),
                (hold_end + 10, ManiaAction.Key1, false),
            };

            foreach (var note in hitObjects.OfType<Note>().Where(n => n is not HeadNote && n is not TailNote))
            {
                double pressTime = note.StartTime + offsetFor(note);
                ManiaAction action = actionForColumn(note.Column);

                transitions.Add((pressTime, action, true));
                transitions.Add((pressTime + 20, action, false));
            }

            transitions.Sort((a, b) => a.time.CompareTo(b.time));

            var times = new SortedSet<double>();

            for (double time = 500; time <= endTime; time += 50)
                times.Add(time);

            foreach (var transition in transitions)
                times.Add(transition.time);

            var pressed = new HashSet<ManiaAction>();
            var frames = new List<ReplayFrame>();
            int cursor = 0;

            foreach (double time in times)
            {
                while (cursor < transitions.Count && transitions[cursor].time <= time)
                {
                    var transition = transitions[cursor++];

                    if (transition.press)
                        pressed.Add(transition.action);
                    else
                        pressed.Remove(transition.action);
                }

                frames.Add(new ManiaReplayFrame(time, pressed.ToArray()));
            }

            return frames;
        }

        private static ManiaAction actionForColumn(int column)
        {
            switch (column)
            {
                case 0: return ManiaAction.Key1;

                case 1: return ManiaAction.Key2;

                case 2: return ManiaAction.Key3;

                default: return ManiaAction.Key4;
            }
        }

        /// <summary>
        /// 一条长 LN 中途多次「按下—松开」：每次提前松手各计一次 ComboBreak（stable 语义）。
        /// 局内与 Session 必须同为 N 次且全量统计一致。
        /// </summary>
        [Test]
        public void TestFullScoreParity_LazerHoldRepeatedBreak()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 4000;
            const int breaks = 4;

            var frames = new List<ReplayFrame>();

            for (int i = 0; i < breaks; i++)
            {
                frames.Add(new ManiaReplayFrame(head + i * 300, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(head + i * 300 + 150));
            }

            // 末尾重按并持过尾，避免尾 auto-miss 干扰断连计数。
            frames.Add(new ManiaReplayFrame(tail - 200, ManiaAction.Key1));
            frames.Add(new ManiaReplayFrame(tail + 100));

            runFullScoreParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                frames);

            AddAssert("repeated hold breaks counted per release", () =>
                currentPlayer.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.ComboBreak) == breaks);
        }

        [Test]
        public void TestReplayAfterRecalcEz2AcMatchesNow()
        {
            runReplayAfterRecalcParityTest(JudgeModeReplayFixtures.CreateEz2AcManyNoteTap());
        }

        [Test]
        public void TestReplayAfterRecalcMalodyEMatchesNow()
        {
            runReplayAfterRecalcParityTest(JudgeModeReplayFixtures.CreateMalodyManyNoteTap());
        }

        [Test]
        public void TestReplayAfterRecalcO2VariableBpmMatchesNow()
        {
            runReplayAfterRecalcParityTest(JudgeModeReplayFixtures.CreateO2VariableBpmTap());
        }

        [Test]
        public void TestIidxManyNoteTapDrawableMatchesSession_LazerHealth()
        {
            runFullScoreParityTestFromFixture(JudgeModeReplayFixtures.CreateBmsManyNoteTap(EzEnumJudgeMode.IIDX_HD));
        }

        [Test]
        public void TestLr2ManyNoteTapDrawableMatchesSession_LazerHealth()
        {
            runFullScoreParityTestFromFixture(JudgeModeReplayFixtures.CreateBmsManyNoteTap(EzEnumJudgeMode.LR2_HD));
        }

        [Test]
        public void TestRajaManyNoteTapDrawableMatchesSession_LazerHealth()
        {
            runFullScoreParityTestFromFixture(JudgeModeReplayFixtures.CreateBmsManyNoteTap(EzEnumJudgeMode.Raja_NM));
        }

        private void runFullScoreParityTestFromFixture((Score score, IBeatmap beatmap, GameplayEnvironment environment) fixture)
        {
            parityEnvironment = fixture.environment;
            runFullScoreParityTest(
                fixture.beatmap.HitObjects.Cast<ManiaHitObject>().ToList(),
                fixture.score.Replay.Frames);
        }

        private void runDrawableParityTestFromFixture((Score score, IBeatmap beatmap, GameplayEnvironment environment) fixture)
        {
            parityEnvironment = fixture.environment;
            runDrawableParityTest(
                fixture.beatmap.HitObjects.Cast<ManiaHitObject>().ToList(),
                fixture.score.Replay.Frames);
        }

        private void runDrawableParityFromJson(string resourcePath, bool longWait = false)
        {
            var document = ManiaReplayJsonFixture.ReadResource(resourcePath);
            var (environment, columns, hitObjects, frames, _) = ManiaReplayJsonFixture.ToParts(document);

            parityEnvironment = environment;

            if (!string.IsNullOrWhiteSpace(document.BeatmapResource))
                runDrawableParityTestWithBeatmapResource(document.BeatmapResource!, frames, longWait);
            else
                runDrawableParityTest(hitObjects, frames, columns, longWait);
        }

        private void runDrawableParityTestWithBeatmapResource(string beatmapResource, List<ReplayFrame> frames, bool longWait = false)
        {
            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("load player", () =>
            {
                using var stream = ManiaReplayJsonAuditLoader.OpenBeatmap(beatmapResource)
                                   ?? throw new FileNotFoundException($"beatmap resource missing: {beatmapResource}");
                IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));
                decoded.BeatmapInfo.Ruleset = new ManiaRuleset().RulesetInfo;

                Beatmap.Value = CreateWorkingBeatmap(decoded);

                replayScore = new Score { Replay = new Replay { Frames = frames } };
                ReplayJudgeTestConfig.ApplyEmbeddedModes(replayScore, parityEnvironment);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            waitCaptureAndAssertParity(beatmapResource, columns: 4, longWait);
        }

        private void runDrawableParityTest(List<ManiaHitObject> hitObjects, List<ReplayFrame> frames, int columns = 4, bool longWait = false)
        {
            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("load player", () =>
            {
                var beatmap = new ManiaBeatmap(new StageDefinition(columns))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                };

                // 与 Session fixtures 一致：120 BPM，保证 EZ2AC LN 16 分 tick 网格对齐。
                beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                Beatmap.Value = CreateWorkingBeatmap(beatmap);

                replayScore = new Score { Replay = new Replay { Frames = frames } };
                ReplayJudgeTestConfig.ApplyEmbeddedModes(replayScore, parityEnvironment);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            waitCaptureAndAssertParity(beatmapResource: null, columns, longWait);
        }

        private void waitCaptureAndAssertParity(string? beatmapResource, int columns = 4, bool longWait = false)
        {
            // 玩家是异步加载的：ScoreProcessor 由 Player 的 [BackgroundDependencyLoader] load() 建出，
            // 在此之前访问它会 NRE（本夹具当进程首个测试时必现）。LoadedBeatmapSuccessfully 为 true
            // 即 load() 已跑到 ScoreProcessor 赋值之后。
            AddUntilStep("wait for player", () =>
                currentPlayer is { LoadedBeatmapSuccessfully: true, ScoreProcessor: not null });

            if (longWait)
            {
                AddStep(new LongUntilStepButton
                {
                    Text = "wait for completion",
                    IsSetupStep = false,
                    CallStack = new StackTrace(1, true),
                    TimeoutMilliseconds = 600_000,
                    Assertion = () => currentPlayer.ScoreProcessor.HasCompleted.Value,
                });
            }
            else
                AddUntilStep("wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);

            AddStep("capture drawable hit events", () =>
            {
                drawableHitEvents = currentPlayer.ScoreProcessor.HitEvents.ToList();
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo);
            });

            // 按需 dump：环境变量 EZ_DUMP_REPLAY_JSON=1
            if (ManiaReplayJsonFixture.DumpEnabled)
            {
                AddStep("dump ReplayJson", () =>
                {
                    int dumpColumns = playableBeatmap is ManiaBeatmap mania
                        ? mania.TotalColumns
                        : columns;

                    string path = Path.Combine(
                        TestContext.CurrentContext.WorkDirectory,
                        "ReplayJsonDump",
                        $"{TestContext.CurrentContext.Test.Name}.json");

                    var hitObjects = string.IsNullOrWhiteSpace(beatmapResource)
                        ? playableBeatmap.HitObjects.OfType<ManiaHitObject>().Where(h => h is Note or HoldNote).ToList()
                        : new List<ManiaHitObject>();

                    ManiaReplayJsonFixture.Write(
                        path,
                        ManiaReplayJsonFixture.FromParts(
                            parityEnvironment,
                            dumpColumns,
                            hitObjects,
                            replayScore.Replay.Frames,
                            beatmapResource));

                    TestContext.WriteLine($"ReplayJson dumped to {path}");
                });
            }

            AddAssert("session hit events match drawable replay path", () =>
            {
                var sessionEvents = ManiaReplaySession.RunHitEvents(replayScore, playableBeatmap, parityEnvironment);

                if (ManiaReplayParityHelper.AreHitEventsEquivalent(drawableHitEvents, sessionEvents))
                    return true;

                throw new AssertionException(
                    $"drawable=[{ManiaReplayParityHelper.DescribeHitEvents(drawableHitEvents)}] session=[{ManiaReplayParityHelper.DescribeHitEvents(sessionEvents)}]");
            });

            AddAssert("results score (p) matches session (n)", () =>
            {
                var sessionResult = ManiaReplaySession.Run(replayScore, playableBeatmap, parityEnvironment);
                return assertPopulatedScoreMatches(sessionResult.ScoreInfo);
            });
        }

        // P2-B: 完整 Score parity 测试（accuracy/score/statistics）
        private void runFullScoreParityTest(
            List<ManiaHitObject> hitObjects,
            List<ReplayFrame> frames,
            bool assertJudgedHitsComplete = false,
            bool assertSessionParity = true,
            double? overallDifficulty = null,
            int? expectedMaxCombo = null,
            bool longWait = false)
        {
            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("load player", () =>
            {
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                };

                if (overallDifficulty is double od)
                {
                    beatmap.BeatmapInfo.Difficulty = new BeatmapDifficulty { OverallDifficulty = (float)od };
                    beatmap.Difficulty = beatmap.BeatmapInfo.Difficulty;
                }

                // 与 Session fixtures 一致：120 BPM，保证 EZ2AC LN 16 分 tick 网格对齐。
                beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                Beatmap.Value = CreateWorkingBeatmap(beatmap);

                replayScore = new Score { Replay = new Replay { Frames = frames } };
                ReplayJudgeTestConfig.ApplyEmbeddedModes(replayScore, parityEnvironment);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            // 玩家是异步加载的：ScoreProcessor 由 Player 的 [BackgroundDependencyLoader] load() 建出，
            // 在此之前访问它会 NRE。LoadedBeatmapSuccessfully 先于 ScoreProcessor 赋值变 true（实测约半数复现），
            // 故两者都要等。
            AddUntilStep("wait for player", () => currentPlayer is { LoadedBeatmapSuccessfully: true, ScoreProcessor: not null });

            if (longWait)
            {
                AddStep(new LongUntilStepButton
                {
                    Text = "wait for completion",
                    IsSetupStep = false,
                    CallStack = new StackTrace(1, true),
                    TimeoutMilliseconds = 600_000,
                    Assertion = () => currentPlayer.ScoreProcessor.HasCompleted.Value,
                });
            }
            else
                AddUntilStep("wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);

            if (assertJudgedHitsComplete)
            {
                AddAssert("all judgements applied", () =>
                {
                    var processor = currentPlayer.ScoreProcessor;
                    return processor.JudgedHits == processor.MaximumJudgements;
                });
            }

            AddStep("capture drawable results", () =>
            {
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo);
            });

            if (!assertSessionParity)
                return;

            if (expectedMaxCombo is int maxCombo)
            {
                AddAssert("drawable max combo matches expected", () =>
                {
                    int actual = currentPlayer.ScoreProcessor.HighestCombo.Value;

                    if (actual == maxCombo)
                        return true;

                    throw new AssertionException($"drawable max combo={actual} expected={maxCombo}");
                });
            }

            AddAssert("results score (p) matches session (n)", () =>
            {
                var sessionResult = ManiaReplaySession.Run(replayScore, playableBeatmap, parityEnvironment);
                return assertPopulatedScoreMatches(sessionResult.ScoreInfo);
            });
        }

        /// <summary>
        /// 结算页的 p（成绩卡）来自局内 <see cref="ScoreProcessor.PopulateScore"/>，n（Score Graph Now）来自 Session 的
        /// <see cref="ScoreInfo"/>。两者必须逐字段相等，否则结算页会出现 p ≠ n。
        /// </summary>
        /// <remarks>
        /// 不能拿 <c>ScoreProcessor.Accuracy.Value</c> 比：StableClassic 的 <c>ManiaScoreProcessor.PopulateScore</c>
        /// 会把 <see cref="ScoreInfo.Accuracy"/> 覆盖成 <c>AccuracyClassic</c>、把总分换成 combo 状态机结果，
        /// 而 <c>Accuracy.Value</c> 仍是 lazer 口径，两边量的不是同一个数。
        /// </remarks>
        private bool assertPopulatedScoreMatches(ScoreInfo sessionInfo)
        {
            var populated = new ScoreInfo { Ruleset = new ManiaRuleset().RulesetInfo };
            currentPlayer.ScoreProcessor.PopulateScore(populated);

            const double tolerance = 1e-6;

            if (Math.Abs(populated.Accuracy - sessionInfo.Accuracy) >= tolerance)
            {
                throw new AssertionException(
                    $"p accuracy={populated.Accuracy:F8} n accuracy={sessionInfo.Accuracy:F8}");
            }

            if (populated.TotalScore != sessionInfo.TotalScore)
            {
                throw new AssertionException(
                    $"p total score={populated.TotalScore} n total score={sessionInfo.TotalScore}");
            }

            if (populated.Combo != sessionInfo.Combo)
            {
                throw new AssertionException(
                    $"p combo={populated.Combo} n combo={sessionInfo.Combo}");
            }

            if (populated.MaxCombo != sessionInfo.MaxCombo)
            {
                throw new AssertionException(
                    $"p max combo={populated.MaxCombo} n max combo={sessionInfo.MaxCombo}");
            }

            if (populated.Rank != sessionInfo.Rank)
            {
                throw new AssertionException(
                    $"p rank={populated.Rank} n rank={sessionInfo.Rank}");
            }

            foreach (var result in Enum.GetValues<HitResult>())
            {
                // Session 路径不记录 IgnoreHit/IgnoreMiss（与既有 hit event 比对口径一致）。
                if (result is HitResult.IgnoreHit or HitResult.IgnoreMiss)
                    continue;

                int p = populated.Statistics.GetValueOrDefault(result);
                int n = sessionInfo.Statistics.GetValueOrDefault(result);

                if (p != n)
                {
                    throw new AssertionException(
                        $"p {result}={p} n {result}={n}");
                }
            }

            return true;
        }

        private void runReplayAfterRecalcParityTest((Score score, IBeatmap beatmap, GameplayEnvironment environment) fixture)
        {
            parityEnvironment = fixture.environment;

            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("recalculate then load replay", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(fixture.beatmap);
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo);

                replayScore = fixture.score.DeepClone();
                ReplayJudgeTestConfig.ApplyEmbeddedModes(replayScore, parityEnvironment);

                var nowScore = ManiaReplaySession.Run(replayScore.DeepClone(), playableBeatmap, parityEnvironment);
                ScoreManager.ApplyEzSessionRecalculationToDetachedScoreInfo(
                    replayScore.ScoreInfo,
                    nowScore.ScoreInfo,
                    ReplayRunPurpose.ForLive,
                    parityEnvironment);
                recalculatedScoreInfo = replayScore.ScoreInfo.DeepClone();

                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            // 玩家是异步加载的：ScoreProcessor 由 Player 的 [BackgroundDependencyLoader] load() 建出，
            // 在此之前访问它会 NRE。LoadedBeatmapSuccessfully 先于 ScoreProcessor 赋值变 true（实测约半数复现），
            // 故两者都要等。
            AddUntilStep("wait for player", () => currentPlayer is { LoadedBeatmapSuccessfully: true, ScoreProcessor: not null });
            AddUntilStep("wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);

            AddAssert("replay result matches recalculated Now", () =>
            {
                const double tolerance = 1e-6;

                if (Math.Abs(currentPlayer.ScoreProcessor.Accuracy.Value - recalculatedScoreInfo.Accuracy) >= tolerance)
                {
                    throw new AssertionException(
                        $"accuracy mismatch: drawable={currentPlayer.ScoreProcessor.Accuracy.Value}, session={recalculatedScoreInfo.Accuracy}");
                }

                if (currentPlayer.ScoreProcessor.TotalScore.Value != recalculatedScoreInfo.TotalScore)
                {
                    throw new AssertionException(
                        $"total score mismatch: drawable={currentPlayer.ScoreProcessor.TotalScore.Value}, session={recalculatedScoreInfo.TotalScore}");
                }

                foreach (var result in Enum.GetValues<HitResult>())
                {
                    if (result is HitResult.IgnoreHit or HitResult.IgnoreMiss)
                        continue;

                    if (currentPlayer.ScoreProcessor.Statistics.GetValueOrDefault(result) != recalculatedScoreInfo.Statistics.GetValueOrDefault(result))
                    {
                        throw new AssertionException(
                            $"{result} mismatch: drawable={currentPlayer.ScoreProcessor.Statistics.GetValueOrDefault(result)}, "
                            + $"session={recalculatedScoreInfo.Statistics.GetValueOrDefault(result)}");
                    }
                }

                return true;
            });
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleReplayPlayer(Score score)
                : base(score, new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                })
            {
            }
        }
    }
}
