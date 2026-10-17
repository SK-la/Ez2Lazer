// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaScoreProcessorTest
    {
        [TestCase(ScoreRank.X, 1, HitResult.Perfect)]
        [TestCase(ScoreRank.X, 0.99, HitResult.Great)]
        [TestCase(ScoreRank.D, 0.1, HitResult.Great)]
        [TestCase(ScoreRank.X, 0.99, HitResult.Perfect, HitResult.Great)]
        [TestCase(ScoreRank.X, 0.99, HitResult.Great, HitResult.Great)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Perfect, HitResult.Good)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Perfect, HitResult.Ok)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Perfect, HitResult.Meh)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Perfect, HitResult.Miss)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Great, HitResult.Good)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Great, HitResult.Ok)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Great, HitResult.Meh)]
        [TestCase(ScoreRank.S, 0.99, HitResult.Great, HitResult.Miss)]
        public void TestRanks(ScoreRank expected, double accuracy, params HitResult[] results)
        {
            var scoreProcessor = new ManiaScoreProcessor();

            Dictionary<HitResult, int> resultsDict = new Dictionary<HitResult, int>();
            foreach (var result in results)
                resultsDict[result] = resultsDict.GetValueOrDefault(result) + 1;

            Assert.That(scoreProcessor.RankFromScore(accuracy, resultsDict), Is.EqualTo(expected));
        }

        /// <summary>
        /// [Ez] 无头（非 gameplay）处理器必须保持 ppy 上游 Lazer 语义，不得回读玩家全局 JudgeMode。
        /// 否则官方 legacy 转换 / 后台升级 / 重算会被当前 JudgeMode 污染（曾导致 stable 导入成绩被错转为 D + 超低分）。
        /// </summary>
        [Test]
        public void TestHeadlessProcessorUsesOfficialLazerWeights()
        {
            var scoreProcessor = new ManiaScoreProcessor();

            // 与 ppy 上游 ManiaScoreProcessor.GetBaseScoreForResult 一致。
            Assert.That(scoreProcessor.GetBaseScoreForResult(HitResult.Perfect), Is.EqualTo(305));
            Assert.That(scoreProcessor.GetBaseScoreForResult(HitResult.Great), Is.EqualTo(300));
            Assert.That(scoreProcessor.GetBaseScoreForResult(HitResult.Good), Is.EqualTo(200));
            Assert.That(scoreProcessor.GetBaseScoreForResult(HitResult.Ok), Is.EqualTo(100));
            Assert.That(scoreProcessor.GetBaseScoreForResult(HitResult.Meh), Is.EqualTo(50));
        }

        /// <summary>
        /// [Ez] 显式注入 JudgeMode 后才允许使用 Ez 权重表（gameplay 冻结 / Session 注入路径）。
        /// </summary>
        [Test]
        public void TestJudgeModeOverrideSwitchesToEzWeights()
        {
            var scoreProcessor = new ManiaScoreProcessor { JudgeModeOverride = EzEnumJudgeMode.EZ2AC };

            foreach (var result in new[] { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Meh, HitResult.Miss })
            {
                Assert.That(scoreProcessor.GetBaseScoreForResult(result),
                    Is.EqualTo(JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.EZ2AC, result)),
                    () => $"{result} 应使用 EZ2AC 权重表");
            }
        }

        /// <summary>
        /// [Ez] StableClassic 的基础分表：300g 与 300 都按 300 计。
        /// 该表缺分支时每个判定基分都是 0，局内 <c>ScoreProcessor.Accuracy</c> 的分子分母同为 0，会恒判 100%。
        /// </summary>
        [Test]
        public void TestStableClassicUsesStableWeights()
        {
            var expected = new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 300,
                [HitResult.Great] = 300,
                [HitResult.Good] = 200,
                [HitResult.Ok] = 100,
                [HitResult.Meh] = 50,
                [HitResult.Miss] = 0,
            };

            foreach (var (result, value) in expected)
            {
                Assert.That(JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, result),
                    Is.EqualTo(value),
                    () => $"{result} 应使用 stable classic 档位权重");
            }
        }
    }
}
