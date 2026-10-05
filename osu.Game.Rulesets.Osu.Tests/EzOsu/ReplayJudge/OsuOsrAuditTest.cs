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
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    /// <summary>
    /// Osu osr 完整金标门禁。Header 与 Session 共用同一套 Parse 金标。
    /// </summary>
    [TestFixture]
    public class OsuOsrAuditTest
    {
        private const string report_file_name = "osu_osr_audit.txt";
        private const string osr_resource = "Resources/Testing/Replays/OsuAudit-SaikouNoKataomoi.osr";
        private const string beatmap_resource = "Resources/Testing/Beatmaps/OsuAudit-SaikouNoKataomoi.osu";

        // Anchored from OsuAudit-SaikouNoKataomoi.osr after LegacyScoreDecoder.Parse (lazer v30000019, user=SK_la).
        private const string expected_beatmap_md5 = "a961fc95f5678f61810ca205903c51da";
        private const long expected_total_score = 329109;
        private const int expected_max_combo = 139;
        private const double expected_accuracy = 0.7372262773722628;
        private const ScoreRank expected_rank = ScoreRank.C;
        private const int expected_total_score_version = 30000019;
        private const bool expected_is_legacy = false;

        private static readonly IReadOnlyDictionary<HitResult, int> expected_statistics = new Dictionary<HitResult, int>
        {
            [HitResult.Miss] = 3,
            [HitResult.Meh] = 12,
            [HitResult.Ok] = 60,
            [HitResult.Great] = 79,
            [HitResult.LargeTickHit] = 15,
            [HitResult.SmallBonus] = 10,
            [HitResult.LargeBonus] = 3,
            [HitResult.IgnoreMiss] = 8,
            [HitResult.IgnoreHit] = 100,
            [HitResult.SliderTailHit] = 98,
        };

        private static readonly DllResourceStore resources = new DllResourceStore(typeof(OsuOsrAuditTest).Assembly);

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
        public void AuditEmbeddedScoreStatisticsMatchSession()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            assertFullBaseline(score.ScoreInfo);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);

            OsuReplaySession.Run(score, playable, environment);

            string report = buildReport(score, playable);
            archiveReport(report);
            assertFullBaseline(score.ScoreInfo, report);
        }

        [Test]
        public void AuditEmbeddedScoreTimelineDirectMatchesFullBaseline()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);

            var (_, timeline) = OsuReplaySession.RunWithTimeline(score, playable, environment);

            Assert.That(timeline.FinalTotalScore, Is.EqualTo(expected_total_score));
            assertFullBaseline(score.ScoreInfo);
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

        private static string buildReport(Score score, IBeatmap playable)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"osr: {osr_resource}");
            sb.AppendLine($"objects: circles={countObjects<HitCircle>(playable)} sliders={countObjects<Slider>(playable)} spinners={countObjects<Spinner>(playable)}");
            sb.AppendLine($"anchor: acc={expected_accuracy:R} rank={expected_rank} total={expected_total_score} combo={expected_max_combo}");
            sb.AppendLine($"anchor stats: {string.Join(", ", expected_statistics.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
            sb.AppendLine($"session: acc={score.ScoreInfo.Accuracy:R} rank={score.ScoreInfo.Rank} total={score.ScoreInfo.TotalScore} combo={score.ScoreInfo.MaxCombo}");
            sb.AppendLine($"session stats: {string.Join(", ", score.ScoreInfo.Statistics.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
            return sb.ToString();
        }

        private static void archiveReport(string report)
        {
            string reportPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, report_file_name);
            File.WriteAllText(reportPath, report);
            TestContext.AddTestAttachment(reportPath);
            TestContext.WriteLine(report);
        }

        private static int countObjects<T>(IBeatmap beatmap)
        {
            int count = 0;

            foreach (var hitObject in enumerate(beatmap))
            {
                if (hitObject is T)
                    count++;
            }

            return count;
        }

        private static IEnumerable<HitObject> enumerate(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
            {
                foreach (var nested in enumerateRecursive(hitObject))
                    yield return nested;

                yield return hitObject;
            }
        }

        private static IEnumerable<HitObject> enumerateRecursive(HitObject parent)
        {
            foreach (var nested in parent.NestedHitObjects)
            {
                foreach (var deeper in enumerateRecursive(nested))
                    yield return deeper;

                yield return nested;
            }
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
