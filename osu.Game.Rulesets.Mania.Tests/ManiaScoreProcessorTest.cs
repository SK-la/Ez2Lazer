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
        /// [Ez] 无头（非 gameplay）处理器必须保持 ppy 上游 Lazer 语义，不得回读玩家全局 HitMode。
        /// 否则官方 legacy 转换 / 后台升级 / 重算会被当前 HitMode 污染（曾导致 stable 导入成绩被错转为 D + 超低分）。
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
        /// [Ez] 显式注入 HitMode 后才允许使用 Ez 权重表（gameplay 冻结 / Session 注入路径）。
        /// </summary>
        [Test]
        public void TestHitModeOverrideSwitchesToEzWeights()
        {
            var scoreProcessor = new ManiaScoreProcessor { HitModeOverride = EzEnumHitMode.EZ2AC };

            foreach (var result in new[] { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Meh, HitResult.Miss })
            {
                Assert.That(scoreProcessor.GetBaseScoreForResult(result),
                    Is.EqualTo(HitModeHelper.GetBaseScoreForResult(EzEnumHitMode.EZ2AC, result)),
                    () => $"{result} 应使用 EZ2AC 权重表");
            }
        }
    }
}
