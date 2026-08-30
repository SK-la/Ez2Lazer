// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
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
    /// Drawable replay 路径与 <see cref="ManiaReplaySession"/> 的 parity（Lazer + Ez HitMode）。
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
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, EzEnumHitMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, EzEnumHealthMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, EzEnumJudgePrecedence.Earliest);
        }

        [Test]
        public void TestLazerTapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

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

        [Test]
        public void TestLazerHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

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

        [Test]
        public void TestIidxTapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

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
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

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
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

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
            runDrawableParityTestFromFixture(HitModeReplayFixtures.CreateEz2AcManyNoteTap());
        }

        [Test]
        public void TestMalodyETapDrawableMatchesSession()
        {
            runDrawableParityTestFromFixture(HitModeReplayFixtures.CreateMalodyManyNoteTap());
        }

        [Test]
        public void TestO2VariableBpmTapDrawableMatchesSession()
        {
            runDrawableParityTestFromFixture(HitModeReplayFixtures.CreateO2VariableBpmTap());
        }

        [Test]
        public void TestMalodyHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Malody_E, EzEnumHealthMode.Lazer);

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
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

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
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2PillUpgradeOnBadRange();
            parityEnvironment = environment;

            runDrawableParityTest(
                beatmap.HitObjects.Cast<ManiaHitObject>().ToList(),
                score.Replay.Frames);
        }

        [Test]
        public void TestEz2AcHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            const double head = 1000;
            const double tail = 2000;

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

        // ==================== POLICY-PARITY: JudgePrecedence × 叠键 ====================

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestLazerOverlappingJackDrawableMatchesSession(EzEnumJudgePrecedence precedence)
        {
            var fixture = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer, precedence);
            runDrawableParityTestFromFixture(fixture);
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestIidxOverlappingJackDrawableMatchesSession(EzEnumJudgePrecedence precedence)
        {
            var fixture = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, precedence);
            runDrawableParityTestFromFixture(fixture);
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestIidxOverlappingJackDrawableMatchesSession_LazerHealth(EzEnumJudgePrecedence precedence)
        {
            var fixture = JudgePrecedenceReplayFixtures.CreateOverlappingJack(
                EzEnumHitMode.IIDX_HD, EzEnumHealthMode.Lazer, precedence);
            runDrawableParityTestFromFixture(fixture);
        }

        // ==================== P2-B: Parity 扩到完整 Score ====================

        [Test]
        public void TestFullScoreParity_LazerTap()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

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
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

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
        /// [Ez] 断连 LN（提早松手、尾判 miss）：用户报告 EZ2AC 下游戏结束统计比 Session 重算少若干判定，
        /// 缺失数量与 ComboBreak 数一致。全量比较 Statistics（含 ComboBreak / Miss）。
        /// </summary>
        [Test]
        public void TestFullScoreParity_Ez2AcBrokenHold()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

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
                });
        }

        /// <summary>
        /// [Ez] 断连后重按到尾判：EZ2AC 断连 LN 的另一常见形态。
        /// </summary>
        [Test]
        public void TestFullScoreParity_Ez2AcBrokenHoldRepress()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

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
                    new ManiaReplayFrame(tail), // 到尾判才松手
                    new ManiaReplayFrame(4000, ManiaAction.Key2),
                    new ManiaReplayFrame(4100),
                });
        }

        [Test]
        public void TestReplayAfterRecalcEz2AcMatchesNow()
        {
            runReplayAfterRecalcParityTest(HitModeReplayFixtures.CreateEz2AcManyNoteTap());
        }

        [Test]
        public void TestReplayAfterRecalcMalodyEMatchesNow()
        {
            runReplayAfterRecalcParityTest(HitModeReplayFixtures.CreateMalodyManyNoteTap());
        }

        [Test]
        public void TestReplayAfterRecalcO2VariableBpmMatchesNow()
        {
            runReplayAfterRecalcParityTest(HitModeReplayFixtures.CreateO2VariableBpmTap());
        }

        [Test]
        public void TestIidxManyNoteTapDrawableMatchesSession_LazerHealth()
        {
            runFullScoreParityTestFromFixture(HitModeReplayFixtures.CreateBmsManyNoteTap(EzEnumHitMode.IIDX_HD));
        }

        [Test]
        public void TestLr2ManyNoteTapDrawableMatchesSession_LazerHealth()
        {
            runFullScoreParityTestFromFixture(HitModeReplayFixtures.CreateBmsManyNoteTap(EzEnumHitMode.LR2_HD));
        }

        [Test]
        public void TestRajaManyNoteTapDrawableMatchesSession_LazerHealth()
        {
            runFullScoreParityTestFromFixture(HitModeReplayFixtures.CreateBmsManyNoteTap(EzEnumHitMode.Raja_NM));
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

        private void runDrawableParityTest(List<ManiaHitObject> hitObjects, List<ReplayFrame> frames)
        {
            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("load player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new ManiaBeatmap(new StageDefinition(4))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                });

                Beatmap.Value.Beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                replayScore = new Score { Replay = new Replay { Frames = frames } };
                ReplayJudgeTestConfig.ApplyEmbeddedModes(replayScore, parityEnvironment);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            AddUntilStep("wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);

            AddStep("capture drawable hit events", () =>
            {
                drawableHitEvents = currentPlayer.ScoreProcessor.HitEvents.ToList();
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo);
            });

            AddAssert("session hit events match drawable replay path", () =>
            {
                var sessionEvents = ManiaReplaySession.RunHitEvents(replayScore, playableBeatmap, parityEnvironment);

                if (ManiaReplayParityHelper.AreHitEventsEquivalent(drawableHitEvents, sessionEvents))
                    return true;

                throw new AssertionException(
                    $"drawable=[{ManiaReplayParityHelper.DescribeHitEvents(drawableHitEvents)}] session=[{ManiaReplayParityHelper.DescribeHitEvents(sessionEvents)}]");
            });
        }

        // P2-B: 完整 Score parity 测试（accuracy/score/statistics）
        private void runFullScoreParityTest(List<ManiaHitObject> hitObjects, List<ReplayFrame> frames)
        {
            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("load player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new ManiaBeatmap(new StageDefinition(4))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                });

                Beatmap.Value.Beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                replayScore = new Score { Replay = new Replay { Frames = frames } };
                ReplayJudgeTestConfig.ApplyEmbeddedModes(replayScore, parityEnvironment);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            AddUntilStep("wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);

            AddStep("capture drawable results", () =>
            {
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo);
            });

            AddAssert("session accuracy matches drawable", () =>
            {
                var sessionResult = ManiaReplaySession.Run(replayScore, playableBeatmap, parityEnvironment);
                double drawableAccuracy = currentPlayer.ScoreProcessor.Accuracy.Value;
                double sessionAccuracy = sessionResult.ScoreInfo.Accuracy;

                const double tolerance = 1e-6;
                if (Math.Abs(drawableAccuracy - sessionAccuracy) < tolerance)
                    return true;

                throw new AssertionException(
                    $"drawable accuracy={drawableAccuracy:F8} session accuracy={sessionAccuracy:F8}");
            });

            AddAssert("session total score matches drawable", () =>
            {
                var sessionResult = ManiaReplaySession.Run(replayScore, playableBeatmap, parityEnvironment);
                long drawableScore = currentPlayer.ScoreProcessor.TotalScore.Value;
                long sessionScore = sessionResult.ScoreInfo.TotalScore;

                if (drawableScore == sessionScore)
                    return true;

                throw new AssertionException(
                    $"drawable score={drawableScore} session score={sessionScore}");
            });

            AddAssert("session statistics match drawable", () =>
            {
                var sessionResult = ManiaReplaySession.Run(replayScore, playableBeatmap, parityEnvironment);
                var drawableStats = currentPlayer.ScoreProcessor.Statistics;
                var sessionStats = sessionResult.ScoreInfo.Statistics;

                // 比较所有 HitResult 的计数（排除 IgnoreHit，因为 Session 不产生此结果）
                foreach (var kvp in drawableStats)
                {
                    if (kvp.Key == HitResult.IgnoreHit || kvp.Key == HitResult.IgnoreMiss)
                        continue; // Session 路径不记录 IgnoreHit/IgnoreMiss

                    if (!sessionStats.TryGetValue(kvp.Key, out int sessionCount) || sessionCount != kvp.Value)
                    {
                        throw new AssertionException(
                            $"statistics mismatch for {kvp.Key}: drawable={kvp.Value} session={sessionStats.GetValueOrDefault(kvp.Key, 0)}");
                    }
                }

                // 检查 session 是否有额外的统计项
                foreach (var kvp in sessionStats)
                {
                    if (!drawableStats.ContainsKey(kvp.Key) && kvp.Value != 0)
                    {
                        throw new AssertionException(
                            $"session has extra statistic {kvp.Key}={kvp.Value} not in drawable");
                    }
                }

                return true;
            });
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
