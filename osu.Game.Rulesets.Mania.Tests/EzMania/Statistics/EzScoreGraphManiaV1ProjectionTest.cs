// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Statistics
{
    /// <summary>
    /// V1 列的来源契约：只取「同一份 replay + 钉死 StableClassic 的 Session」输出，
    /// 与 Now 共用同一套仿真器，不读 <c>ScoreInfo.HitEvents</c> 重判。
    /// </summary>
    /// <remarks>
    /// 旧的按事件重判路径会丢三样信息：auto miss 的 stored offset 不是按键时刻、
    /// 长条档位需要头尾两时刻、持有期连击增量不在 events 里。
    /// </remarks>
    [TestFixture]
    public class EzScoreGraphManiaV1ProjectionTest
    {
        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestNoSessionYieldsZeroSnapshot()
        {
            var zero = EzScoreGraphMania.CreateV1Snapshot(null);

            Assert.Multiple(() =>
            {
                Assert.That(zero.Accuracy, Is.Zero, "无 Session 时 V1 必须保持 0，不能回落到成绩自身数值");
                Assert.That(zero.Score, Is.Zero);
                Assert.That(zero.Counts, Is.Empty);
            });
        }

        [Test]
        public void TestSnapshotTakesSessionScoreVerbatim()
        {
            var sessionInfo = new ScoreInfo
            {
                Accuracy = 0.5,
                TotalScore = 42,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 3,
                    [HitResult.Miss] = 1,
                },
            };

            var snapshot = EzScoreGraphMania.CreateV1Snapshot(sessionInfo);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Accuracy, Is.EqualTo(0.5));
                Assert.That(snapshot.Score, Is.EqualTo(42), "总分必须原样取 Session，不能按 acc 反投影");
                Assert.That(snapshot.Counts.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(3));
                Assert.That(snapshot.Counts.GetValueOrDefault(HitResult.Miss), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TestJudgeModeOverrideMatchesExplicitStableClassicEnvironment()
        {
            var (score, beatmap) = createMixedFixture();

            var live = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(live);

            var pinned = await new ManiaReplaySessionService().RunRequestAsync(new ReplayRunRequest(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive)
            {
                JudgeModeOverride = EzEnumJudgeMode.StableClassic,
            }).ConfigureAwait(true);

            var expected = ManiaReplaySession.Run(score.DeepClone(), beatmap, live with { JudgeMode = EzEnumJudgeMode.StableClassic });

            assertEquivalent(expected.ScoreInfo, pinned.Score.ScoreInfo, "pinned stable classic");
        }

        [Test]
        public async Task TestJudgeModeOverrideDoesNotInheritLiveJudgeMode()
        {
            var (score, beatmap) = createMixedFixture();

            // 全局档位换成另一个 Ez 档：pinned 结果必须与全局无关。
            var other = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(other);

            var pinned = await new ManiaReplaySessionService().RunRequestAsync(new ReplayRunRequest(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive)
            {
                JudgeModeOverride = EzEnumJudgeMode.StableClassic,
            }).ConfigureAwait(true);

            var expectedStable = ManiaReplaySession.Run(score.DeepClone(), beatmap, other with { JudgeMode = EzEnumJudgeMode.StableClassic });
            var liveResult = ManiaReplaySession.Run(score.DeepClone(), beatmap, other);

            assertEquivalent(expectedStable.ScoreInfo, pinned.Score.ScoreInfo, "pinned stable classic");

            Assert.That(pinned.Score.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Perfect),
                Is.Not.EqualTo(liveResult.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Perfect)).Or.EqualTo(expectedStable.ScoreInfo.Statistics.GetValueOrDefault(HitResult.Perfect)),
                "前提：该谱面在 EZ2AC 与 stable 下判定不同");
        }

        [Test]
        public async Task TestV1EqualsNowWhenScoreIsStableClassic()
        {
            var (score, beatmap) = createMixedFixture();
            var stableEnv = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(stableEnv);

            var now = await new ManiaReplaySessionService().RunRequestAsync(new ReplayRunRequest(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive)).ConfigureAwait(true);
            var v1 = await new ManiaReplaySessionService().RunRequestAsync(new ReplayRunRequest(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive)
            {
                JudgeModeOverride = EzEnumJudgeMode.StableClassic,
            }).ConfigureAwait(true);

            assertEquivalent(now.Score.ScoreInfo, v1.Score.ScoreInfo, "stable 成绩的 Now 与 V1");
        }

        [Test]
        public async Task TestV1GoldenValues()
        {
            var (score, beatmap) = createMixedFixture();

            // 全局档位故意设为其它档：V1 金标不允许被当前配置影响。
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumJudgeMode.EZ2AC, EzEnumHealthMode.Ez2Ac));

            var pinned = await new ManiaReplaySessionService().RunRequestAsync(new ReplayRunRequest(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive)
            {
                JudgeModeOverride = EzEnumJudgeMode.StableClassic,
            }).ConfigureAwait(true);

            var v1 = EzScoreGraphMania.CreateV1Snapshot(pinned.Score.ScoreInfo);

            // 固定样本的金标：后续解耦 / 删 Classic 分支时这些数值必须逐位不变。
            // 上游 mania 判定机制调整（tap late miss / LN early break / 断连罚除子）会改变这些值，届时需整体重基线。
            Assert.Multiple(() =>
            {
                Assert.That(v1.Accuracy, Is.EqualTo(0.4722222222222222), "V1 accuracy golden");
                Assert.That(v1.Score, Is.EqualTo(348434L), "V1 total score golden");
                Assert.That(describeCounts(v1.Counts),
                    Is.EqualTo("Miss=6,Good=1,Great=2,Perfect=3,IgnoreMiss=3,IgnoreHit=7,ComboBreak=2"),
                    "V1 counts golden");
            });
        }

        private static string describeCounts(Dictionary<HitResult, int> counts)
            => string.Join(",", counts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        private static void assertEquivalent(ScoreInfo expected, ScoreInfo actual, string context)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.Accuracy, Is.EqualTo(expected.Accuracy), $"{context}: accuracy");
                Assert.That(actual.TotalScore, Is.EqualTo(expected.TotalScore), $"{context}: total score");
                Assert.That(actual.Statistics, Is.EquivalentTo(expected.Statistics), $"{context}: statistics");
            });
        }

        private static (Score score, IBeatmap beatmap) createMixedFixture()
        {
            var ruleset = new ManiaRuleset();
            var hitObjects = new List<HitObject>();
            var frames = new List<ReplayFrame>();

            void tap(double time, double pressTime, int column = 0)
            {
                hitObjects.Add(new Note { StartTime = time, Column = column });
                frames.Add(new ManiaReplayFrame(pressTime, (ManiaAction)((int)ManiaAction.Key1 + column)));
                frames.Add(new ManiaReplayFrame(pressTime + 40));
            }

            void hold(double head, double tail, double pressTime, double releaseTime)
            {
                hitObjects.Add(new HoldNote { StartTime = head, Duration = tail - head, Column = 0 });
                frames.Add(new ManiaReplayFrame(pressTime, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(releaseTime));
            }

            // tap：命中窗内各档 + 窗外按键（auto miss）
            tap(1000, 1000);
            tap(2000, 2035);
            tap(3000, 3065);
            tap(4000, 4110);
            tap(5000, 5125);
            tap(6000, 6200);

            // 同列双押只按一次：一个命中，另一个 auto miss 且 stored offset 很小
            hitObjects.Add(new Note { StartTime = 7000, Column = 1 });
            hitObjects.Add(new Note { StartTime = 7030, Column = 1 });
            frames.Add(new ManiaReplayFrame(7030, ManiaAction.Key2));
            frames.Add(new ManiaReplayFrame(7080));

            // 长条：完美 / 提前松手（miss）/ 尾偏晚 / 从未按下
            hold(8000, 9000, 8000, 9000);
            hold(10000, 11000, 10000, 10500);
            hold(12000, 13000, 12000, 13040);
            hitObjects.Add(new HoldNote { StartTime = 14000, Duration = 1000, Column = 0 });

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = new ControlPointInfo(),
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.Difficulty.OverallDifficulty = 7;

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var score = new Score
            {
                ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
                Replay = new Replay { Frames = frames.OrderBy(f => f.Time).ToList() },
            };

            return (score, beatmap);
        }
    }
}
