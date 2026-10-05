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
    /// Osu osr 金标门禁。契约：Session 产物对照「冻结的原始 osr 快照」——禁止用同一载体属性自洽顶替。
    /// </summary>
    [TestFixture]
    public class OsuOsrAuditTest
    {
        private const string report_file_name = "osu_osr_audit.txt";
        private const string osr_resource = "Resources/Testing/Replays/OsuAudit-SaikouNoKataomoi.osr";
        private const string beatmap_resource = "Resources/Testing/Beatmaps/OsuAudit-SaikouNoKataomoi.osu";

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

            using var stream = resources.GetStream(osr_resource);
            Score score = new HarnessScoreDecoder().Parse(stream);

            assertMatchesAnchor(score.ScoreInfo);
        }

        [Test]
        public void AuditSessionProductMatchesFrozenOsrSnapshot()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            ScoreInfo original = score.ScoreInfo.DeepClone();
            assertMatchesAnchor(original);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);

            // 静态 Run 会 PopulateScore 写回入参；必须喂克隆，保留 original / 调用方。
            Score session = OsuReplaySession.Run(score.DeepClone(), playable, environment);

            string report = buildReport(original, session, playable);
            archiveReport(report);

            assertMatchesOriginal(session.ScoreInfo, original, report);
            assertHitEventsMatchOriginalStatistics(session.ScoreInfo, original, report);
            assertMatchesOriginal(score.ScoreInfo, original, "Session 写回污染了调用方 ScoreInfo");
        }

        [Test]
        public void AuditTimelineDirectMatchesFrozenOsrSnapshot()
        {
            assumeResourcesPresent();

            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            ScoreInfo original = score.ScoreInfo.DeepClone();
            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);

            var (session, timeline) = OsuReplaySession.RunWithTimeline(score.DeepClone(), playable, environment);

            Assert.That(timeline.FinalTotalScore, Is.EqualTo(original.TotalScore));
            assertMatchesOriginal(session.ScoreInfo, original);
            assertMatchesOriginal(score.ScoreInfo, original, "RunWithTimeline 污染了调用方 ScoreInfo");
        }

        private static void assertMatchesAnchor(ScoreInfo info, string? report = null)
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

        private static void assertMatchesOriginal(ScoreInfo actual, ScoreInfo original, string? report = null)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.TotalScore, Is.EqualTo(original.TotalScore), report);
                Assert.That(actual.MaxCombo, Is.EqualTo(original.MaxCombo), report);
                Assert.That(actual.Accuracy, Is.EqualTo(original.Accuracy).Within(1e-12), report);
                Assert.That(actual.Rank, Is.EqualTo(original.Rank), report);
                assertStatisticsEqual(actual.Statistics, original.Statistics, report);
            });
        }

        private static void assertHitEventsMatchOriginalStatistics(ScoreInfo session, ScoreInfo original, string? report)
        {
            var fromEvents = session.HitEvents
                                    .GroupBy(e => e.Result)
                                    .ToDictionary(g => g.Key, g => g.Count());

            assertStatisticsEqual(fromEvents, original.Statistics, report);
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

        private static string buildReport(ScoreInfo original, Score session, IBeatmap playable)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"osr: {osr_resource}");
            sb.AppendLine($"objects: circles={countObjects<HitCircle>(playable)} sliders={countObjects<Slider>(playable)} spinners={countObjects<Spinner>(playable)}");
            sb.AppendLine($"original: acc={original.Accuracy:R} rank={original.Rank} total={original.TotalScore} combo={original.MaxCombo}");
            sb.AppendLine($"session:  acc={session.ScoreInfo.Accuracy:R} rank={session.ScoreInfo.Rank} total={session.ScoreInfo.TotalScore} combo={session.ScoreInfo.MaxCombo}");
            sb.AppendLine($"original stats: {formatStats(original.Statistics)}");
            sb.AppendLine($"session stats:  {formatStats(session.ScoreInfo.Statistics)}");
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
