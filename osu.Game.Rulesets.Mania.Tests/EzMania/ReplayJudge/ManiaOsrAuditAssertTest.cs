// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania osr 完整金标门禁。CI 断言解码后全部基线字段；Session 对齐用同一套金标（Explicit：纯净 master 已知小缺口）。
    /// </summary>
    [TestFixture]
    public class ManiaOsrAuditAssertTest
    {
        private const string report_file_name = "mania_osr_audit_assert.txt";
        private const string osr_resource = "Resources/Testing/Replays/GramNibelungen23-53miss.osr";
        private const string beatmap_resource = "Resources/Testing/Beatmaps/GramNibelungen23.osu";

        private const string expected_beatmap_md5 = "3229df33df91aa3e83b4db14ee4cb4cb";
        private const long expected_total_score = 764114;
        private const int expected_max_combo = 715;
        private const double expected_accuracy = 0.9340671270971999;
        private const ScoreRank expected_rank = ScoreRank.A;
        private const int expected_total_score_version = 30000019;
        private const bool expected_is_legacy = false;

        private static readonly IReadOnlyDictionary<HitResult, int> expected_statistics = new Dictionary<HitResult, int>
        {
            [HitResult.Miss] = 53,
            [HitResult.Meh] = 54,
            [HitResult.Ok] = 99,
            [HitResult.Good] = 555,
            [HitResult.Great] = 1984,
            [HitResult.Perfect] = 3144,
            [HitResult.IgnoreMiss] = 18,
            [HitResult.IgnoreHit] = 4902,
            [HitResult.ComboBreak] = 20,
        };

        private static readonly DllResourceStore resources = new DllResourceStore(typeof(ManiaOsrAuditAssertTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void AuditEmbeddedScoreHeaderMatchesAnchor()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            assertFullBaseline(score.ScoreInfo);
        }

        [Test]
        [Explicit("master 纯净环境实测 Session≠锚点。修判定后去掉 Explicit。")]
        public void AuditEmbeddedScoreStatisticsMatchSession()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            assertFullBaseline(score.ScoreInfo);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

            ManiaReplaySession.Run(score, playable, environment);

            string report = buildReport(score);
            archiveReport(report);
            assertFullBaseline(score.ScoreInfo, report);
        }

        private static void assertFullBaseline(ScoreInfo info, string? report = null)
        {
            Assert.Multiple(() =>
            {
                Assert.That(info.BeatmapInfo?.MD5Hash, Is.EqualTo(expected_beatmap_md5), report);
                Assert.That(info.TotalScore, Is.EqualTo(expected_total_score), report);
                Assert.That(info.MaxCombo, Is.EqualTo(expected_max_combo), report);
                Assert.That(info.Accuracy, Is.EqualTo(expected_accuracy).Within(1e-12), report);
                Assert.That(info.Rank, Is.EqualTo(expected_rank), report);
                Assert.That(info.TotalScoreVersion, Is.EqualTo(expected_total_score_version), report);
                Assert.That(info.IsLegacyScore, Is.EqualTo(expected_is_legacy), report);
                Assert.That(info.Mods.Select(m => m.Acronym).ToArray(), Is.Empty, report);
                assertStatisticsEqual(info.Statistics, expected_statistics, report);
            });
        }

        private static void assertStatisticsEqual(
            IReadOnlyDictionary<HitResult, int> actual,
            IReadOnlyDictionary<HitResult, int> expected,
            string? report)
        {
            var actualNonZero = actual.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).ToArray();
            var expectedNonZero = expected.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).ToArray();

            Assert.That(
                actualNonZero.Select(kv => $"{kv.Key}={kv.Value}"),
                Is.EqualTo(expectedNonZero.Select(kv => $"{kv.Key}={kv.Value}")),
                report);
        }

        private static void assumeResourcesPresent()
        {
            if (resources.GetStream(osr_resource) == null || resources.GetStream(beatmap_resource) == null)
                Assert.Ignore($"缺少内嵌资源：{osr_resource} / {beatmap_resource}");
        }

        private static string buildReport(Score score)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"osr: {osr_resource}");
            sb.AppendLine($"anchor: acc={expected_accuracy:R} rank={expected_rank} total={expected_total_score} combo={expected_max_combo}");
            sb.AppendLine($"session: acc={score.ScoreInfo.Accuracy:R} rank={score.ScoreInfo.Rank} total={score.ScoreInfo.TotalScore} combo={score.ScoreInfo.MaxCombo}");
            sb.AppendLine($"session stats: {string.Join(", ", score.ScoreInfo.Statistics.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
            return sb.ToString();
        }

        private static void archiveReport(string report)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, report_file_name);
            File.WriteAllText(path, report);
            TestContext.AddTestAttachment(path);
            TestContext.WriteLine(report);
        }

        private sealed class HarnessScoreDecoder : LegacyScoreDecoder
        {
            public WorkingBeatmap? LastWorkingBeatmap { get; private set; }

            protected override Ruleset GetRuleset(int rulesetId) => new ManiaRuleset();

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
