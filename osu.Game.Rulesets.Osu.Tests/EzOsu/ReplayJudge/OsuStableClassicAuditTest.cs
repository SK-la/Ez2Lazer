// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    /// <summary>
    /// std StableClassic 的金标门禁：拿 stable 原生 OSR 的头部快照（不是 Ez 自己算出来的数）
    /// 对照 Session 产物，锁住「正常游玩机制」的对齐。
    /// </summary>
    /// <remarks>
    /// 锚点全部来自 osr 头部，属于冻结值：
    /// 主判定（300 / 100 / 50 / miss）、最大连击、准确率、总分。
    /// 准确率按 0.005% 容差（两位小数不可见），总分按 0.05% 容差（stable 的分数累加在尾部 tick 与 combo 乘区的
    /// 取整上留有历史口径差异）。
    /// </remarks>
    [TestFixture]
    public class OsuStableClassicAuditTest
    {
        private const string osr_resource = "Resources/Testing/Replays/OsuAudit-Stable-Midorigo.osr";
        private const string beatmap_resource = "Resources/Testing/Beatmaps/OsuAudit-Midorigo.osu";
        private const string report_file_name = "osu_stable_classic_audit.txt";

        private const int expected_great = 1172;
        private const int expected_ok = 48;
        private const int expected_meh = 1;
        private const int expected_miss = 11;
        private const int expected_max_combo = 438;
        private const double expected_accuracy = 0.9644209956709957;
        private const long expected_total_score = 9820018;

        private const double accuracy_tolerance = 5e-5;
        private const double total_score_tolerance = 5e-4;

        private static readonly DllResourceStore resources = new DllResourceStore(typeof(OsuStableClassicAuditTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void TestSessionMatchesStableHeader()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            ScoreInfo original = score.ScoreInfo.DeepClone();

            Assert.Multiple(() =>
            {
                Assert.That(original.Statistics.GetValueOrDefault(HitResult.Great), Is.EqualTo(expected_great), "锚点自检：great");
                Assert.That(original.Statistics.GetValueOrDefault(HitResult.Ok), Is.EqualTo(expected_ok), "锚点自检：ok");
                Assert.That(original.Statistics.GetValueOrDefault(HitResult.Meh), Is.EqualTo(expected_meh), "锚点自检：meh");
                Assert.That(original.Statistics.GetValueOrDefault(HitResult.Miss), Is.EqualTo(expected_miss), "锚点自检：miss");
                Assert.That(original.MaxCombo, Is.EqualTo(expected_max_combo), "锚点自检：combo");
            });

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo) with
            {
                JudgeMode = EzEnumJudgeMode.StableClassic,
            };

            // 静态 Run 会 PopulateScore 写回入参；喂克隆，保留 original。
            Score session = OsuReplaySession.Run(score.DeepClone(), playable, environment);
            ScoreInfo actual = session.ScoreInfo;

            string report = buildReport(original, actual);
            archiveReport(report);

            var actualMainStats = actual.Statistics
                                        .Where(kv => kv.Key is HitResult.Great or HitResult.Ok or HitResult.Meh or HitResult.Miss)
                                        .Where(kv => kv.Value != 0)
                                        .OrderBy(kv => kv.Key)
                                        .ToArray();

            var expectedMainStats = new[]
            {
                (HitResult.Great, expected_great),
                (HitResult.Ok, expected_ok),
                (HitResult.Meh, expected_meh),
                (HitResult.Miss, expected_miss),
            }.OrderBy(kv => kv.Item1).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(
                    actualMainStats.Select(kv => $"{kv.Key}={kv.Value}"),
                    Is.EqualTo(expectedMainStats.Select(kv => $"{kv.Item1}={kv.Item2}")),
                    $"主判定统计\n{report}");

                Assert.That(actual.MaxCombo, Is.EqualTo(expected_max_combo), $"最大连击\n{report}");

                Assert.That(
                    Math.Abs(actual.Accuracy - expected_accuracy), Is.LessThan(accuracy_tolerance),
                    $"准确率\n{report}");

                Assert.That(
                    Math.Abs(actual.TotalScore - expected_total_score) / (double)expected_total_score, Is.LessThan(total_score_tolerance),
                    $"总分\n{report}");
            });
        }

        [Test]
        public void TestSessionWritesOnlyToItsOwnClone()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            ScoreInfo original = score.ScoreInfo.DeepClone();
            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo) with
            {
                JudgeMode = EzEnumJudgeMode.StableClassic,
            };

            OsuReplaySession.Run(score.DeepClone(), playable, environment);

            Assert.Multiple(() =>
            {
                Assert.That(score.ScoreInfo.TotalScore, Is.EqualTo(original.TotalScore));
                Assert.That(score.ScoreInfo.Accuracy, Is.EqualTo(original.Accuracy));
                Assert.That(score.ScoreInfo.MaxCombo, Is.EqualTo(original.MaxCombo));
            });
        }

        private static void assumeResourcesPresent()
        {
            if (resources.GetStream(osr_resource) == null || resources.GetStream(beatmap_resource) == null)
                Assert.Ignore($"缺少内嵌资源：{osr_resource} / {beatmap_resource}");
        }

        private static string buildReport(ScoreInfo original, ScoreInfo session)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"osr: {osr_resource}");
            sb.AppendLine($"original: total={original.TotalScore} combo={original.MaxCombo} acc={original.Accuracy:R} rank={original.Rank}");
            sb.AppendLine($"session:  total={session.TotalScore} combo={session.MaxCombo} acc={session.Accuracy:R} rank={session.Rank}");
            sb.AppendLine($"original stats: {formatStats(original.Statistics)}");
            sb.AppendLine($"session stats:  {formatStats(session.Statistics)}");
            return sb.ToString();
        }

        private static string formatStats(IReadOnlyDictionary<HitResult, int> stats)
            => string.Join(", ", stats.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        private static void archiveReport(string report)
        {
            string reportPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, report_file_name);
            File.WriteAllText(reportPath, report);
            TestContext.AddTestAttachment(reportPath);
            TestContext.WriteLine(report);
        }

        private sealed class HarnessScoreDecoder : LegacyScoreDecoder
        {
            public WorkingBeatmap? LastWorkingBeatmap { get; private set; }

            protected override Ruleset GetRuleset(int rulesetId) => new OsuRuleset();

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                using var stream = resources.GetStream(beatmap_resource);
                IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));
                decoded.BeatmapInfo.MD5Hash = md5Hash;
                LastWorkingBeatmap = new TestWorkingBeatmap(decoded);
                return LastWorkingBeatmap;
            }
        }
    }
}
