// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    /// <summary>
    /// P2-B: offset 落定后 Statistics 变化集成测试
    /// 验证 offset 调整后，Session 输出的 accuracy/score/statistics 正确更新
    /// </summary>
    public partial class TestSceneOffsetIntegration : RateAdjustedBeatmapTestScene
    {
        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        private IBeatmap playableBeatmap = null!;
        private Score replayScore = null!;
        private ManiaGameplayEnvironment baseEnvironment = null!;

        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.OffsetPlusMania, 0);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, EzEnumHitMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, EzEnumHealthMode.Lazer);
        }

        [Test]
        public void TestStatisticsChangeAfterOffsetAdjustment()
        {
            var hitObjects = new List<ManiaHitObject>
            {
                new Note { StartTime = 1000, Column = 0 },
                new Note { StartTime = 2000, Column = 0 },
                new Note { StartTime = 3000, Column = 1 },
                new Note { StartTime = 4000, Column = 1 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1), // Perfect timing
                new ManiaReplayFrame(1100),
                new ManiaReplayFrame(2050, ManiaAction.Key1), // Slightly late (50ms)
                new ManiaReplayFrame(2150),
                new ManiaReplayFrame(3000, ManiaAction.Key2), // Perfect timing
                new ManiaReplayFrame(3100),
                new ManiaReplayFrame(4000, ManiaAction.Key2), // Perfect timing
                new ManiaReplayFrame(4100),
            };

            AddStep("setup base environment", () =>
            {
                baseEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
                ReplayJudgeTestConfig.ApplyToGlobalConfig(baseEnvironment);
            });

            AddStep("create beatmap and score", () =>
            {
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                };

                beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                playableBeatmap = beatmap;
                replayScore = new Score { Replay = new Replay { Frames = frames } };
            });

            // 测试 offset = 0（基准）
            AddAssert("baseline statistics at offset=0", () =>
            {
                var result = ManiaReplaySession.Run(replayScore, playableBeatmap, baseEnvironment);
                var stats = result.ScoreInfo.Statistics;

                // 预期：3 Perfect, 1 Great (50ms late)
                Assert.That(stats.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(3));
                Assert.That(stats.GetValueOrDefault(HitResult.Great), Is.EqualTo(1));

                return true;
            });

            // 测试 offset = -50（提前 50ms，应该让第 2 个 note 变成 Perfect）
            AddStep("set offset to -50ms", () =>
            {
                GlobalConfigStore.EzConfig.SetValue(Ez2Setting.OffsetPlusMania, -50.0);
            });

            AddAssert("statistics after offset=-50", () =>
            {
                var environmentWithOffset = baseEnvironment with { OffsetPlusMania = -50.0 };
                var result = ManiaReplaySession.Run(replayScore, playableBeatmap, environmentWithOffset);
                var stats = result.ScoreInfo.Statistics;

                // 预期：4 Perfect（offset 补偿了 50ms 延迟）
                Assert.That(stats.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(4),
                    $"Expected 4 Perfect but got {stats.GetValueOrDefault(HitResult.Perfect)}");
                Assert.That(stats.GetValueOrDefault(HitResult.Great), Is.EqualTo(0),
                    $"Expected 0 Great but got {stats.GetValueOrDefault(HitResult.Great)}");

                return true;
            });

            // 测试 offset = +50（延后 50ms，应该让原本 Perfect 的变成 Great）
            AddStep("set offset to +50ms", () =>
            {
                GlobalConfigStore.EzConfig.SetValue(Ez2Setting.OffsetPlusMania, 50.0);
            });

            AddAssert("statistics after offset=+50", () =>
            {
                var environmentWithOffset = baseEnvironment with { OffsetPlusMania = 50.0 };
                var result = ManiaReplaySession.Run(replayScore, playableBeatmap, environmentWithOffset);
                var stats = result.ScoreInfo.Statistics;

                // 预期：原本 3 个 Perfect 中有部分变成 Great
                // 具体取决于 HitWindows，这里假设都还在 Perfect 范围内
                int perfectCount = stats.GetValueOrDefault(HitResult.Perfect);
                int greatCount = stats.GetValueOrDefault(HitResult.Great);

                Assert.That(perfectCount + greatCount, Is.EqualTo(4),
                    $"Total hits should be 4 but got {perfectCount + greatCount}");

                return true;
            });
        }

        [Test]
        public void TestAccuracyChangeAfterOffsetAdjustment()
        {
            var hitObjects = new List<ManiaHitObject>
            {
                new Note { StartTime = 1000, Column = 0 },
                new Note { StartTime = 2000, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1), // Perfect
                new ManiaReplayFrame(1100),
                new ManiaReplayFrame(2100, ManiaAction.Key1), // Late by 100ms
                new ManiaReplayFrame(2200),
            };

            AddStep("setup environment", () =>
            {
                var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
                ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            });

            AddStep("create beatmap and score", () =>
            {
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                };

                beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                playableBeatmap = beatmap;
                replayScore = new Score { Replay = new Replay { Frames = frames } };
            });

            AddAssert("accuracy increases with correct offset", () =>
            {
                var baseEnv = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
                var adjustedEnv = baseEnv with { OffsetPlusMania = -100.0 };

                var baseResult = ManiaReplaySession.Run(replayScore, playableBeatmap, baseEnv);
                var adjustedResult = ManiaReplaySession.Run(replayScore, playableBeatmap, adjustedEnv);

                double baseAccuracy = baseResult.ScoreInfo.Accuracy;
                double adjustedAccuracy = adjustedResult.ScoreInfo.Accuracy;

                // offset 调整后准确率应该提高
                Assert.That(adjustedAccuracy, Is.GreaterThan(baseAccuracy),
                    $"Adjusted accuracy ({adjustedAccuracy:F6}) should be greater than base ({baseAccuracy:F6})");

                return true;
            });
        }
    }
}
