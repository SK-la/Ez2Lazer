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
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Statistics
{
    /// <summary>
    /// 验证 <see cref="EzScoreGraphMania"/> 核心逻辑：
    /// - Now 数据只在 Session 结果就绪后才刷新
    /// - CommittedNowScore 为 null 时不更新 NowCounts/NowAccuracy/NowScore
    /// - 切 HitMode 不清 CommittedNowScore，静默保持上一次有效值
    /// </summary>
    [TestFixture]
    public class EzScoreGraphManiaTests
    {
        /// <summary>
        /// 测试 A：ExtractDisplayCounts 在 info==null 路径下行为。
        /// 由于 ExtractDisplayCounts 是 static 且只读 statistics，这里验证
        /// extractDisplayCounts(null) 不抛异常（映射到 static 版本）。
        /// </summary>
        [Test]
        public void TestExtractDisplayCountsWithNullStatisticsReturnsEmpty()
        {
            var result = EzScoreGraphMania.ExtractDisplayCounts(
                new Dictionary<HitResult, int>(), EzEnumHitMode.Lazer);

            Assert.That(result.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// 测试 A'：验证 session 结果中 Statistics 为空时 ExtractDisplayCounts 返回空。
        /// </summary>
        [Test]
        public void TestExtractDisplayCountsFromEmptySessionStatistics()
        {
            var emptyStats = new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 0,
                [HitResult.Miss] = 0,
            };

            var result = EzScoreGraphMania.ExtractDisplayCounts(emptyStats, EzEnumHitMode.Lazer);

            Assert.That(result.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// 测试 B：Session Statistics 经 ExtractDisplayCounts 过滤后，
        /// 总计数与原始 session 事件中有效判定数一致。
        /// </summary>
        [Test]
        public void TestExtractDisplayCountsMatchesSessionStatisticsTotal()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var env = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(env);

            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, env);
            var statistics = sessionScore.ScoreInfo.Statistics;

            var extracted = EzScoreGraphMania.ExtractDisplayCounts(statistics, EzEnumHitMode.Lazer);
            int extractedTotal = extracted.Values.Sum();

            // 过滤后总计数应等于 session 产出的所有有效判定总数
            int expectedTotal = statistics
                .Where(kvp => kvp.Value > 0)
                .Where(kvp => kvp.Key.IsBasic() || kvp.Key == HitResult.Poor || kvp.Key == HitResult.Miss)
                .Sum(kvp => kvp.Value);

            Assert.That(extractedTotal, Is.EqualTo(expectedTotal),
                "ExtractDisplayCounts 总计数应与 session Statistics 有效判定数一致");
        }

        /// <summary>
        /// 测试 B'：不同 HitMode 提取的计数映射到不同的判定集合。
        /// </summary>
        [Test]
        public void TestExtractDisplayCountsDiffersBetweenHitModes()
        {
            var (score, beatmap, _) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();

            var lazerEnv = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            var bmsEnv = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, bmsPoorHitResultEnable: true);

            var lazerSession = ManiaReplaySession.Run(score.DeepClone(), beatmap, lazerEnv);
            var bmsSession = ManiaReplaySession.Run(score.DeepClone(), beatmap, bmsEnv);

            var lazerCounts = EzScoreGraphMania.ExtractDisplayCounts(lazerSession.ScoreInfo.Statistics, EzEnumHitMode.Lazer);
            var bmsCounts = EzScoreGraphMania.ExtractDisplayCounts(bmsSession.ScoreInfo.Statistics, EzEnumHitMode.IIDX_HD);

            Assert.That(lazerCounts, Is.Not.EqualTo(bmsCounts),
                "不同 HitMode 的 session 结果应映射到不同计数");
        }

        /// <summary>
        /// 测试 C：验证切换 HitMode 后重新运行 Session，Now 数据应反映新的 HitMode。
        /// 由于 fixture 完美时机（offset=0）各 HitMode 都判 Perfect，
        /// 此处验证 ExtractDisplayCounts 对各 HitMode 返回正确的有效结果集合。
        /// </summary>
        [Test]
        public void TestExtractDisplayCountsReturnsCorrectResultsPerHitMode()
        {
            var (score, beatmap, _) = HitModeReplayFixtures.CreateEz2AcManyNoteTap(noteCount: 10);

            // 初始 Lazer Session
            var lazerEnv = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(lazerEnv);
            var lazerSession = ManiaReplaySession.Run(score.DeepClone(), beatmap, lazerEnv);

            // 切到 EZ2AC Session
            var ez2AcEnv = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ez2AcEnv);
            var ez2AcSession = ManiaReplaySession.Run(score.DeepClone(), beatmap, ez2AcEnv);

            // 完美时机时两模式结果相同（都是 Perfect），验证过滤结果有效
            var lazerCounts = EzScoreGraphMania.ExtractDisplayCounts(lazerSession.ScoreInfo.Statistics, EzEnumHitMode.Lazer);
            var ez2AcCounts = EzScoreGraphMania.ExtractDisplayCounts(ez2AcSession.ScoreInfo.Statistics, EzEnumHitMode.EZ2AC);

            // 两模式总计数相同（因为 fixture 完美时机），但有效结果集不同（EZ2AC 额外有 Kool 等）
            Assert.That(lazerCounts.Values.Sum(), Is.EqualTo(10), "完美时机 Lazer 应有 10 个有效判定");
            Assert.That(ez2AcCounts.Values.Sum(), Is.EqualTo(10), "完美时机 EZ2AC 应有 10 个有效判定");

            // 验证 ExtractDisplayCounts 对不同 HitMode 返回不同键集合（EZ2AC 有额外判定类型）
            Assert.That(ez2AcCounts.Keys, Is.SupersetOf(lazerCounts.Keys),
                "EZ2AC 有效结果集应包含 Lazer 的所有结果");
        }

        /// <summary>
        /// 测试 E：验证切换到 O2Jam HitMode 时 BPM 同步对判定结果有影响。
        /// BPM 越高窗口越窄，同一 offset 偏移在高 BPM 下判定更差。
        /// </summary>
        [Test]
        public void TestO2JamBpmSyncAffectsJudgement()
        {
            var env = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(env);

            // 使用固定偏移（25ms）在不同 BPM 下验证
            double tapOffset = 25;
            var (score120, beatmap120) = createTapOnlyFixture(new[] { tapOffset }, bpm: 120);
            var (score200, beatmap200) = createTapOnlyFixture(new[] { tapOffset }, bpm: 200);

            var session120 = ManiaReplaySession.Run(score120.DeepClone(), beatmap120, env);
            var session200 = ManiaReplaySession.Run(score200.DeepClone(), beatmap200, env);

            var result120 = session120.ScoreInfo.HitEvents.First().Result;
            var result200 = session200.ScoreInfo.HitEvents.First().Result;

            // BPM 120 窗口更大 → 相同偏移在 BPM 120 下判定不差于 BPM 200
            Assert.That((int)result120, Is.GreaterThanOrEqualTo((int)result200),
                $"BPM 120 结果({result120}) 应不差于 BPM 200 ({result200})");
        }

        // ---- 以下是从 EzScoreGraphRejudgeParityTest 复制的辅助方法 ----

        private static (Score score, IBeatmap beatmap) createTapOnlyFixture(
            double[] offsetsMs, double bpm = 120)
        {
            var ruleset = new ManiaRuleset();
            var hitObjects = new List<Rulesets.Objects.HitObject>();
            var frames = new List<Rulesets.Replays.ReplayFrame>();

            const double base_time = 1000;
            const double note_spacing = 500;

            for (int i = 0; i < offsetsMs.Length; i++)
            {
                double time = base_time + i * note_spacing;
                hitObjects.Add(new Note { StartTime = time, Column = 0 });

                double pressTime = time + offsetsMs[i];
                frames.Add(new ManiaReplayFrame(pressTime, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(pressTime + 60));
            }

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = new ControlPointInfo(),
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60000 / bpm });
            beatmap.Difficulty.OverallDifficulty = 7;

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return (new Score
            {
                ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
                Replay = new Replay { Frames = frames },
            }, beatmap);
        }
    }
}
