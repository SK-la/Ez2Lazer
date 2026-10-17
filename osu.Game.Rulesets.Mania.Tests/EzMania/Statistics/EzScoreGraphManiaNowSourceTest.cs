// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Statistics
{
    /// <summary>
    /// 锁定 Score Graph 的 Now 列取值契约：唯一来源是 Session 的 <see cref="ScoreInfo"/>，
    /// 无 Session 时为 0，不用成绩自身的数值顶替。
    /// </summary>
    [TestFixture]
    public class EzScoreGraphManiaNowSourceTest
    {
        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestNoSessionYieldsZeroSnapshot()
        {
            var zero = EzScoreGraphMania.SessionNowSnapshot.Zero();

            Assert.That(zero.Accuracy, Is.Zero, "无 Session 时 Now 必须保持 0，不能回落到成绩自身数值");
            Assert.That(zero.Score, Is.Zero);
            Assert.That(zero.Counts, Is.Empty);
        }

        [Test]
        public void TestSnapshotTakesSessionScoreVerbatim()
        {
            // stable classic 的总分由 combo 状态机累加，任何 acc × 1_000_000 的投影都不等于它。
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

            var snapshot = EzScoreGraphMania.CreateSessionNowSnapshot(sessionInfo, EzEnumJudgeMode.StableClassic);

            Assert.That(snapshot.Accuracy, Is.EqualTo(0.5));
            Assert.That(snapshot.Score, Is.EqualTo(42), "总分必须原样取 Session，不能按 acc 反投影");
            Assert.That(snapshot.Counts.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(3));
            Assert.That(snapshot.Counts.GetValueOrDefault(HitResult.Miss), Is.EqualTo(1));
        }

        [Test]
        public void TestSnapshotUsesSessionStatisticsNotHitEvents()
        {
            var sessionInfo = new ScoreInfo
            {
                Accuracy = 1,
                TotalScore = 100,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 2,
                    [HitResult.Great] = 1,
                },
            };

            var snapshot = EzScoreGraphMania.CreateSessionNowSnapshot(sessionInfo, EzEnumJudgeMode.StableClassic);

            Assert.That(sessionInfo.HitEvents, Is.Empty, "本用例不提供 HitEvents");
            Assert.That(snapshot.Counts.Values.Sum(), Is.EqualTo(3), "计数取 Session 的 Statistics，不重跑判定");
        }

        [Test]
        public void TestStableClassicSessionSnapshotMatchesSessionScoreInfo()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateSingleHoldHeldPastTail(holdUntil: 6000);
            var environment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);

            var sessionInfo = ManiaReplaySession.Run(score, beatmap, environment).ScoreInfo;
            var snapshot = EzScoreGraphMania.CreateSessionNowSnapshot(sessionInfo, EzEnumJudgeMode.StableClassic);

            Assert.That(snapshot.Accuracy, Is.EqualTo(sessionInfo.Accuracy));
            Assert.That(snapshot.Score, Is.EqualTo(sessionInfo.TotalScore));
            Assert.That(snapshot.Counts, Is.EqualTo(EzScoreGraphMania.ExtractDisplayCounts(sessionInfo.Statistics, EzEnumJudgeMode.StableClassic)));
            Assert.That(snapshot.Counts.Values.Sum(), Is.GreaterThan(0), "Now 计数不应为空");
        }
    }
}
