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
using osu.Game.IO;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    ///     Mania osr 双轨金标旁证（多谱面套件）——<b>不是</b>局内对齐门禁。
    ///     <para>
    ///     局内 Drawable ≡ Session 走高精度 JSON：
    ///     <see cref="ManiaReplayJsonFixture"/> + <c>TestSceneReplaySessionParity.TestJson_*</c>。
    ///     osr 为整数 ms Round，不能单独定责 Drawable/列管理器拆分。
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <b>Lazer Header</b>：冻结 osr 嵌入 Statistics / 分 / combo（解码 ≡ 嵌入）。
    ///         </item>
    ///         <item>
    ///             <b>Lazer Session</b>：冻结<strong>现行</strong>
    ///             <see cref="ManiaReplaySession"/> 产出（与局内对齐后的 Session），精确比对。
    ///         </item>
    ///         <item>
    ///             <b>StableClassic Session</b>：仍在向 stable 原生锚点收敛，故只钉<strong>距锚点的差距上限</strong>
    ///             （<see cref="SessionBound"/>）：更近通过、更远失败，收敛后不必重钉断言。
    ///             与嵌入/stable 客户端数字可因整数帧与历史 LN 语义不同。
    ///         </item>
    ///     </list>
    ///     套件：Hanatachi、PORTRAiT。StableClassic 主判定键门禁（不含 Ignore*）；Lazer Session 全量非零。
    ///     高难度 LN 闭环仍用 <see cref="OsrAuditTest" /> + GramNibelungen23（勿当金标）。
    /// </summary>
    [TestFixture]
    public class ManiaOsrAuditAssertTest
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(ManiaOsrAuditAssertTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            GlobalConfigStore.EnsureInitialized();
        }

        // ── Lazer 套件 ─────────────────────────────────────────────────────────

        // Header = osr 嵌入；Session* = 现行 ManiaReplaySession（局内对齐后，含整数帧量化缝）。
        private static readonly AuditFixture lazer_hanatachi = new AuditFixture(
            "Lazer-Hanatachi",
            "Resources/Testing/Replays/ManiaAudit-Lazer-Hanatachi.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu",
            EzEnumJudgeMode.Lazer,
            "29278f227275b1720e995d532130710b",
            920155,
            3451,
            0.9801182063007212,
            ScoreRank.S,
            30000019,
            false,
            Array.Empty<string>(),
            new Dictionary<HitResult, int>
            {
                [HitResult.Miss] = 15,
                [HitResult.Meh] = 12,
                [HitResult.Ok] = 22,
                [HitResult.Good] = 148,
                [HitResult.Great] = 1063,
                [HitResult.Perfect] = 4182,
                [HitResult.IgnoreMiss] = 5,
                [HitResult.IgnoreHit] = 2093,
                [HitResult.ComboBreak] = 8,
            },
            true,
            true,
            new SessionAnchor(
                919507,
                3451,
                0.9799073387917894,
                new Dictionary<HitResult, int>
                {
                    [HitResult.Miss] = 17,
                    [HitResult.Meh] = 12,
                    [HitResult.Ok] = 21,
                    [HitResult.Good] = 147,
                    [HitResult.Great] = 1073,
                    [HitResult.Perfect] = 4172,
                    [HitResult.IgnoreMiss] = 6,
                    [HitResult.IgnoreHit] = 2091,
                    [HitResult.ComboBreak] = 9,
                }),
            null);

        private static readonly AuditFixture lazer_portrait = new AuditFixture(
            "Lazer-PORTRAiT",
            "Resources/Testing/Replays/ManiaAudit-Lazer-PORTRAiT.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu",
            EzEnumJudgeMode.Lazer,
            "5737c0072e3c1319d95cf117b3f78648",
            876539,
            892,
            0.9716775696247674,
            ScoreRank.S,
            30000019,
            false,
            Array.Empty<string>(),
            new Dictionary<HitResult, int>
            {
                [HitResult.Miss] = 56,
                [HitResult.Meh] = 24,
                [HitResult.Ok] = 37,
                [HitResult.Good] = 206,
                [HitResult.Great] = 2224,
                [HitResult.Perfect] = 4808,
                [HitResult.IgnoreMiss] = 25,
                [HitResult.IgnoreHit] = 5598,
                [HitResult.ComboBreak] = 31,
            },
            true,
            true,
            new SessionAnchor(
                874701,
                892,
                0.971189444004859,
                new Dictionary<HitResult, int>
                {
                    [HitResult.Miss] = 60,
                    [HitResult.Meh] = 24,
                    [HitResult.Ok] = 36,
                    [HitResult.Good] = 208,
                    [HitResult.Great] = 2198,
                    [HitResult.Perfect] = 4829,
                    [HitResult.IgnoreMiss] = 27,
                    [HitResult.IgnoreHit] = 5594,
                    [HitResult.ComboBreak] = 33,
                }),
            null);

        public static IEnumerable<AuditFixture> LazerFixtures()
        {
            yield return lazer_hanatachi;
            yield return lazer_portrait;
        }

        // StableClassic：本档仍在向锚点收敛，故门禁只钉「距锚点的差距上限」：
        // 上限取当前实测差距，更近通过、更远失败。残差来源与后续收敛项记在
        // .cursor/plans/classic_源码差异审计_8edfa732.plan.md；收敛后把上限整体归零即可。
        // Expected* = stable 原生量（Statistics / MaxCombo / LegacyTotalScore，见
        // AuditStableClassicFixtureAnchorMatchesDecodedHeader）；ExpectedAccuracy 是解码后 lazer 迁移重算值，仅供记录，不作门禁。
        private static readonly AuditFixture stable_classic_hanatachi = new AuditFixture(
            "StableClassic-Hanatachi",
            "Resources/Testing/Replays/ManiaAudit-Classic-Hanatachi.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu",
            EzEnumJudgeMode.StableClassic,
            "29278f227275b1720e995d532130710b",
            904544,
            1728,
            0.9774174631810525,
            ScoreRank.S,
            null,
            true,
            new[] { "CL" },
            new Dictionary<HitResult, int>
            {
                [HitResult.Miss] = 36,
                [HitResult.Meh] = 10,
                [HitResult.Ok] = 9,
                [HitResult.Good] = 103,
                [HitResult.Great] = 808,
                [HitResult.Perfect] = 3423,
            },
            false,
            false,
            null,
            new SessionBound(
                369,
                new Dictionary<HitResult, int>
                {
                    [HitResult.Miss] = 2,
                    [HitResult.Meh] = 0,
                    [HitResult.Ok] = 2,
                    [HitResult.Good] = 1,
                    [HitResult.Great] = 0,
                    [HitResult.Perfect] = 1,
                }));

        // 计分改取已落定档位后，40 个 Miss 不再按同列最近按键的 stored offset 重判成命中（不再各送一次基础分），
        // 该档总分从 867600 降到 866984，距锚点 -4933；旧上限 4317 是这份偏差被高估填平后的实测值。
        private static readonly AuditFixture stable_classic_portrait = new AuditFixture(
            "StableClassic-PORTRAiT",
            "Resources/Testing/Replays/ManiaAudit-Classic-PORTRAiT.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu",
            EzEnumJudgeMode.StableClassic,
            "5737c0072e3c1319d95cf117b3f78648",
            871917,
            1340,
            0.9669379597984128,
            ScoreRank.S,
            null,
            true,
            new[] { "CL" },
            new Dictionary<HitResult, int>
            {
                [HitResult.Miss] = 38,
                [HitResult.Meh] = 8,
                [HitResult.Ok] = 15,
                [HitResult.Good] = 190,
                [HitResult.Great] = 1801,
                [HitResult.Perfect] = 2476,
            },
            false,
            false,
            null,
            new SessionBound(
                5000,
                new Dictionary<HitResult, int>
                {
                    [HitResult.Miss] = 2,
                    [HitResult.Meh] = 3,
                    [HitResult.Ok] = 0,
                    [HitResult.Good] = 1,
                    [HitResult.Great] = 4,
                    [HitResult.Perfect] = 0,
                }));

        public static IEnumerable<AuditFixture> StableClassicFixtures()
        {
            yield return stable_classic_hanatachi;
            yield return stable_classic_portrait;
        }

        [TestCaseSource(nameof(LazerFixtures))]
        public void AuditLazerEmbeddedScoreHeaderMatchesAnchor(AuditFixture fixture)
        {
            assertDecodedHeader(fixture);
        }

        [TestCaseSource(nameof(LazerFixtures))]
        public void AuditLazerSessionMatchesAnchor(AuditFixture fixture)
        {
            assertSessionMatchesAnchor(fixture);
        }

        [TestCaseSource(nameof(StableClassicFixtures))]
        public void AuditStableClassicSessionMatchesAnchor(AuditFixture fixture)
        {
            assertSessionMatchesAnchor(fixture);
        }

        [TestCaseSource(nameof(StableClassicFixtures))]
        public void AuditStableClassicFixtureAnchorMatchesDecodedHeader(AuditFixture fixture)
        {
            assertAnchorMatchesDecodedHeader(fixture);
        }

        /// <summary>
        /// 门禁口径自检：StableClassic 行的「头」锚点必须逐项等于 osr 解码头。
        /// </summary>
        /// <remarks>
        /// 解码末段会跑 <c>StandardisedScoreMigrationTools.UpdateToLatestScoring</c>，<see cref="ScoreInfo.TotalScore"/>
        /// 与 <see cref="ScoreInfo.Accuracy"/> 都是 <b>lazer 标准分迁移重算值</b>（mania 走
        /// <c>150000·combo + 850000·acc^(2+2·acc)</c>，acc 用 Perfect=305 权重），<b>不是</b> stable 原生量，
        /// 不能拿来当 stable 门禁。此处只钉 stable 原生三项：Statistics、MaxCombo、LegacyTotalScore。
        /// </remarks>
        private static void assertAnchorMatchesDecodedHeader(AuditFixture fixture)
        {
            assumeResourcesPresent(fixture);

            var decoder = new HarnessScoreDecoder(fixture.BeatmapResource);
            Score score;

            using (var stream = resources.GetStream(fixture.OsrResource))
                score = decoder.Parse(stream);

            ScoreInfo info = score.ScoreInfo;

            Assert.Multiple(() =>
            {
                Assert.That(info.IsLegacyScore, Is.True, "该夹具的头锚点取自 stable 原生量，要求 osr 走 legacy 解码路径");

                Assert.That(
                    info.LegacyTotalScore,
                    Is.EqualTo((long?)fixture.ExpectedTotalScore),
                    $"ExpectedTotalScore 应为 stable 原生分（LegacyTotalScore）；实际解码头 LegacyTotalScore={info.LegacyTotalScore}，"
                    + $"迁移后 TotalScore={info.TotalScore}");

                Assert.That(
                    info.MaxCombo,
                    Is.EqualTo(fixture.ExpectedMaxCombo),
                    $"MaxCombo 锚点={fixture.ExpectedMaxCombo}，解码头={info.MaxCombo}");

                assertStatisticsEqual(info.Statistics, fixture.ExpectedStatistics, false, null);
            });
        }

        private static void assertDecodedHeader(AuditFixture fixture)
        {
            Assume.That(fixture.AssertDecodedHeader, "本夹具不以解码 Header 为门禁");
            assumeResourcesPresent(fixture);

            var decoder = new HarnessScoreDecoder(fixture.BeatmapResource);
            Score score;

            using (var stream = resources.GetStream(fixture.OsrResource))
                score = decoder.Parse(stream);

            assertAgainstAnchor(score.ScoreInfo, fixture, null, false);
        }

        private static void assertSessionMatchesAnchor(AuditFixture fixture)
        {
            assumeResourcesPresent(fixture);

            var decoder = new HarnessScoreDecoder(fixture.BeatmapResource);
            Score score;

            using (var stream = resources.GetStream(fixture.OsrResource))
                score = decoder.Parse(stream);

            // StableClassic：丢弃 Lazer 解码嵌入 Statistics/分数，只保留 Replay 帧；Mods/JudgeMode 按静态契约注入。
            if (!fixture.AssertDecodedHeader)
                prepareScoreForClassicSession(score, fixture);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            // 金标环境固定：不得扫 JudgePrecedence / OffsetPlusMania。
            var environment = ReplayJudgeTestConfig.Create(
                fixture.JudgeMode,
                EzEnumHealthMode.Lazer);

            ManiaReplaySession.Run(score, playable, environment);

            string report = buildReport(score, fixture);
            archiveReport(fixture, report);
            assertAgainstAnchor(score.ScoreInfo, fixture, report, true);
        }

        private static void prepareScoreForClassicSession(Score score, AuditFixture fixture)
        {
            var ruleset = new ManiaRuleset();
            score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
            score.ScoreInfo.Mods = resolveMods(ruleset, fixture.ExpectedModAcronyms);

            // 不以 Lazer 解码的嵌入成绩为真；清空后由 Session PopulateScore 写回再对静态锚点。
            score.ScoreInfo.Statistics = new Dictionary<HitResult, int>();
            score.ScoreInfo.MaximumStatistics = new Dictionary<HitResult, int>();
            score.ScoreInfo.TotalScore = 0;
            score.ScoreInfo.MaxCombo = 0;
            score.ScoreInfo.Accuracy = 0;
            score.ScoreInfo.Rank = ScoreRank.D;
            score.ScoreInfo.JudgeMode = (int)fixture.JudgeMode;
            score.ScoreInfo.IsLegacyScore = fixture.ExpectedIsLegacy ?? true;
        }

        private static Mod[] resolveMods(Ruleset ruleset, IReadOnlyList<string> acronyms)
        {
            if (acronyms.Count == 0)
                return Array.Empty<Mod>();

            var all = ruleset.CreateAllMods().ToArray();
            var resolved = new List<Mod>(acronyms.Count);

            foreach (string acronym in acronyms)
            {
                var mod = all.FirstOrDefault(m => m.Acronym == acronym);
                Assert.That(mod, Is.Not.Null, $"Ruleset 无 Mod {acronym}");
                resolved.Add(mod!);
            }

            return resolved.ToArray();
        }

        private static void assertAgainstAnchor(ScoreInfo info, AuditFixture fixture, string? report, bool afterSession)
        {
            if (afterSession && fixture.SessionBound is { } bound)
            {
                assertSessionWithinBound(info, fixture, bound, report);
                return;
            }

            // afterSession 且声明了差距上限的夹具已在上面返回；此处 afterSession ⇒ SessionAnchor 非空。
            long expectedTotal = afterSession ? fixture.SessionAnchor!.TotalScore : fixture.ExpectedTotalScore;
            int expectedCombo = afterSession ? fixture.SessionAnchor!.MaxCombo : fixture.ExpectedMaxCombo;
            double expectedAcc = afterSession ? fixture.SessionAnchor!.Accuracy : fixture.ExpectedAccuracy;
            var expectedStats = afterSession ? fixture.SessionAnchor!.Statistics : fixture.ExpectedStatistics;

            Assert.Multiple(() =>
            {
                if (fixture.ExpectedBeatmapMd5 != null)
                    Assert.That(info.BeatmapInfo?.MD5Hash, Is.EqualTo(fixture.ExpectedBeatmapMd5), report);

                Assert.That(info.TotalScore, Is.EqualTo(expectedTotal), report);
                Assert.That(info.MaxCombo, Is.EqualTo(expectedCombo), report);
                // 两侧锚点各自同源：Lazer 轨 ExpectedAccuracy 取自解码头，StableClassic 轨 SessionAccuracy
                // 取自实测冻结（该轨 ExpectedAccuracy 的解码值已被 lazer 迁移改写，不作门禁）。
                double accTolerance = afterSession || fixture.AssertDecodedHeader ? 1e-12 : 5e-3;
                Assert.That(info.Accuracy, Is.EqualTo(expectedAcc).Within(accTolerance), report);

                if (fixture.ExpectedRank is ScoreRank rank)
                    Assert.That(info.Rank, Is.EqualTo(rank), report);

                if (fixture.ExpectedTotalScoreVersion is int version && !afterSession)
                    Assert.That(info.TotalScoreVersion, Is.EqualTo(version), report);

                if (fixture.ExpectedIsLegacy is bool legacy && (fixture.AssertDecodedHeader || afterSession))
                    Assert.That(info.IsLegacyScore, Is.EqualTo(legacy), report);

                if (fixture.AssertDecodedHeader || afterSession)
                {
                    Assert.That(
                        info.Mods.Select(m => m.Acronym).OrderBy(a => a).ToArray(),
                        Is.EqualTo(fixture.ExpectedModAcronyms.OrderBy(a => a).ToArray()),
                        report);
                }

                assertStatisticsEqual(info.Statistics, expectedStats, fixture.AssertFullNonZeroStatistics, report);
            });
        }

        /// <summary>
        /// 收敛档的门禁：逐项比对现行 Session 与解码头（stable 原生量）的差距，不得超过 <see cref="SessionBound"/>。
        /// </summary>
        private static void assertSessionWithinBound(ScoreInfo info, AuditFixture fixture, SessionBound bound, string? report)
        {
            Assert.Multiple(() =>
            {
                if (fixture.ExpectedBeatmapMd5 != null)
                    Assert.That(info.BeatmapInfo?.MD5Hash, Is.EqualTo(fixture.ExpectedBeatmapMd5), report);

                Assert.That(
                    Math.Abs(info.TotalScore - fixture.ExpectedTotalScore),
                    Is.LessThanOrEqualTo(bound.TotalScoreGap),
                    $"总分距解码头 {fixture.ExpectedTotalScore} 差 {info.TotalScore - fixture.ExpectedTotalScore}，超过上限 {bound.TotalScoreGap}{report}");

                Assert.That(info.MaxCombo, Is.EqualTo(fixture.ExpectedMaxCombo), report);

                foreach (var (result, gap) in bound.StatisticGaps.OrderBy(kv => kv.Key))
                {
                    int actual = info.Statistics.GetValueOrDefault(result);
                    int expected = fixture.ExpectedStatistics.GetValueOrDefault(result);

                    Assert.That(
                        Math.Abs(actual - expected),
                        Is.LessThanOrEqualTo(gap),
                        $"{result} 距解码头 {expected} 差 {actual - expected}，超过上限 {gap}{report}");
                }

                if (fixture.ExpectedRank is ScoreRank rank)
                    Assert.That(info.Rank, Is.EqualTo(rank), report);

                if (fixture.ExpectedIsLegacy is bool legacy)
                    Assert.That(info.IsLegacyScore, Is.EqualTo(legacy), report);

                Assert.That(
                    info.Mods.Select(m => m.Acronym).OrderBy(a => a).ToArray(),
                    Is.EqualTo(fixture.ExpectedModAcronyms.OrderBy(a => a).ToArray()),
                    report);
            });
        }

        private static void assertStatisticsEqual(
            IReadOnlyDictionary<HitResult, int> actual,
            IReadOnlyDictionary<HitResult, int> expected,
            bool assertFullNonZero,
            string? report)
        {
            // assertFullNonZero：Lazer 全量非零对账（含 Ignore*/ComboBreak）。
            // 否则只对 expected 键（StableClassic 用户主判定；未给的辅判不门禁）。
            var keys = assertFullNonZero
                ? expected.Keys.Union(actual.Keys).Where(k => actual.GetValueOrDefault(k) != 0 || expected.GetValueOrDefault(k) != 0).OrderBy(k => k).ToArray()
                : expected.Keys.OrderBy(k => k).ToArray();

            string[] actualVals = keys.Select(k => $"{k}={actual.GetValueOrDefault(k)}").ToArray();
            string[] expectedVals = keys.Select(k => $"{k}={expected.GetValueOrDefault(k)}").ToArray();

            Assert.That(actualVals, Is.EqualTo(expectedVals), report);
        }

        private static void assumeResourcesPresent(AuditFixture fixture)
        {
            if (resources.GetStream(fixture.OsrResource) == null || resources.GetStream(fixture.BeatmapResource) == null)
                Assert.Ignore($"缺少内嵌资源：{fixture.OsrResource} / {fixture.BeatmapResource}");
        }

        private static string buildReport(Score score, AuditFixture fixture)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"fixture: {fixture.Name}");
            sb.AppendLine($"osr: {fixture.OsrResource}");
            sb.AppendLine($"judgeMode: {fixture.JudgeMode}");
            sb.AppendLine($"env: JudgeMode={fixture.JudgeMode} HealthMode=Lazer JudgePrecedence=Earliest OffsetPlusMania=0");
            sb.AppendLine($"header-anchor: acc={fixture.ExpectedAccuracy:R} total={fixture.ExpectedTotalScore} combo={fixture.ExpectedMaxCombo}");

            if (fixture.SessionAnchor is { } anchor)
                sb.AppendLine($"session-anchor: acc={anchor.Accuracy:R} total={anchor.TotalScore} combo={anchor.MaxCombo}");

            if (fixture.SessionBound is { } bound)
                sb.AppendLine($"session-bound: totalGap<={bound.TotalScoreGap} statsGap={string.Join(",", bound.StatisticGaps.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");

            sb.AppendLine($"session: acc={score.ScoreInfo.Accuracy:R} rank={score.ScoreInfo.Rank} total={score.ScoreInfo.TotalScore} combo={score.ScoreInfo.MaxCombo}");
            sb.AppendLine($"session mods: {string.Join(",", score.ScoreInfo.Mods.Select(m => m.Acronym))}");
            sb.AppendLine($"session stats: {string.Join(", ", score.ScoreInfo.Statistics.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");

            // 比较基线：有冻结锚点的档比锚点，只钉上限的档比解码头（stable 原生量）。
            long baselineTotal = fixture.SessionAnchor?.TotalScore ?? fixture.ExpectedTotalScore;
            int baselineCombo = fixture.SessionAnchor?.MaxCombo ?? fixture.ExpectedMaxCombo;
            IReadOnlyDictionary<HitResult, int> baselineStats = fixture.SessionAnchor?.Statistics ?? fixture.ExpectedStatistics;

            sb.AppendLine($"delta (session − {(fixture.SessionAnchor != null ? "session-anchor" : "header-anchor")}):");

            foreach (var key in baselineStats.Keys.OrderBy(k => k))
            {
                int delta = score.ScoreInfo.Statistics.GetValueOrDefault(key) - baselineStats[key];
                if (delta != 0)
                    sb.AppendLine($"  {key}: {delta:+#;-#;0}");
            }

            long scoreDelta = score.ScoreInfo.TotalScore - baselineTotal;
            int comboDelta = score.ScoreInfo.MaxCombo - baselineCombo;
            if (scoreDelta != 0)
                sb.AppendLine($"  TotalScore: {scoreDelta:+#;-#;0}");
            if (comboDelta != 0)
                sb.AppendLine($"  MaxCombo: {comboDelta:+#;-#;0}");

            appendHitEventAttribution(sb, score.ScoreInfo.HitEvents, score.Replay);
            return sb.ToString();
        }

        private static void appendHitEventAttribution(StringBuilder sb, IReadOnlyList<HitEvent> hitEvents, Replay? replay = null)
        {
            static string kind(HitEvent e) => e.HitObject switch
            {
                HeadNote => "Head",
                TailNote => "Tail",
                Note => "Note",
                HoldNoteTick => "Tick",
                HoldNoteBody => "Body",
                HoldNote => "Hold",
                _ => e.HitObject?.GetType().Name ?? "?",
            };

            void dumpBucket(string title, HitResult result)
            {
                var items = hitEvents.Where(e => e.Result == result).ToArray();
                var byKind = items.GroupBy(kind).OrderBy(g => g.Key)
                                  .Select(g => $"{g.Key}={g.Count()}");
                sb.AppendLine($"{title}: total={items.Length} [{string.Join(", ", byKind)}]");

                if (result != HitResult.Perfect)
                    return;

                int overJudged = 0;
                int tailSeamRaw26 = 0; // |raw|∈(P, P*lenience] — 靠 release lenience 抬进 Perfect
                int noteHeadOver = 0;

                foreach (var e in items)
                {
                    if (e.HitObject?.HitWindows == null)
                        continue;

                    double abs = Math.Abs(e.TimeOffset);
                    double perfect = e.HitObject.HitWindows.WindowFor(HitResult.Perfect);
                    bool isTail = e.HitObject is TailNote;
                    double judgedAbs = isTail ? abs / TailNote.RELEASE_WINDOW_LENIENCE : abs;

                    if (judgedAbs > perfect)
                        overJudged++;

                    if (isTail && abs > perfect && judgedAbs <= perfect)
                        tailSeamRaw26++;
                    else if (!isTail && abs > perfect)
                        noteHeadOver++;
                }

                int tailRaw26 = items.Count(e => e.HitObject is TailNote && Math.Abs(e.TimeOffset) == 26);
                int seam172 = items.Count(e =>
                {
                    if (e.HitObject?.HitWindows == null) return false;

                    double judgedAbs = e.HitObject is TailNote
                        ? Math.Abs(e.TimeOffset) / TailNote.RELEASE_WINDOW_LENIENCE
                        : Math.Abs(e.TimeOffset);
                    return judgedAbs > 17.0 && judgedAbs <= 17.5;
                });
                sb.AppendLine($"  Perfect window: overJudged={overJudged} noteHeadOver={noteHeadOver} tailLenienceSeam={tailSeamRaw26} tailRawEq26={tailRaw26} seam(17,17.5]={seam172}");
            }

            dumpBucket("Miss by type", HitResult.Miss);
            dumpBucket("Meh by type", HitResult.Meh);
            dumpBucket("Good by type", HitResult.Good);
            dumpBucket("Great by type", HitResult.Great);
            dumpBucket("Perfect by type", HitResult.Perfect);

            // Perfect/Great 边界带：判据 offset（Tail 除 lenience）相对 Perfect 窗。
            double? perfectWindow = hitEvents.Select(e => e.HitObject?.HitWindows?.WindowFor(HitResult.Perfect)).FirstOrDefault(w => w is > 0);
            sb.AppendLine($"PerfectWindow={perfectWindow}");

            if (perfectWindow is double pWin)
            {
                int greatJustOver = 0, greatJustOverNote = 0, greatJustOverHead = 0, greatJustOverTail = 0;

                foreach (var e in hitEvents)
                {
                    if (e.Result != HitResult.Great || e.HitObject?.HitWindows == null) continue;

                    bool tail = e.HitObject is TailNote;

                    double j = Math.Abs(e.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);

                    if (j <= pWin || j > pWin + 0.5) continue;

                    greatJustOver++;

                    switch (e.HitObject)
                    {
                        case TailNote: greatJustOverTail++; break;

                        case HeadNote: greatJustOverHead++; break;

                        case Note: greatJustOverNote++; break;
                    }
                }

                sb.AppendLine($"  Great just over P (P,P+0.5]: n={greatJustOver} Note={greatJustOverNote} Head={greatJustOverHead} Tail={greatJustOverTail}");

                int exactBoundary = hitEvents.Count(ev =>
                {
                    if (ev.Result != HitResult.Great || ev.HitObject?.HitWindows == null) return false;

                    bool tail = ev.HitObject is TailNote;
                    double j = Math.Abs(ev.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);
                    return Math.Abs(j - (pWin + 0.5)) < 1e-9;
                });
                int perfectAtP = hitEvents.Count(ev =>
                {
                    if (ev.Result != HitResult.Perfect || ev.HitObject?.HitWindows == null) return false;

                    bool tail = ev.HitObject is TailNote;
                    double j = Math.Abs(ev.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);
                    return Math.Abs(j - pWin) < 1e-9;
                });
                sb.AppendLine($"  exact |j|=P+0.5 Great={exactBoundary}; exact |j|=P Perfect={perfectAtP}");

                foreach (var e in hitEvents
                                  .Where(ev => ev.Result == HitResult.Great && ev.HitObject?.HitWindows != null)
                                  .Select(ev =>
                                  {
                                      bool tail = ev.HitObject is TailNote;
                                      double j = Math.Abs(ev.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);
                                      return (ev, j, tail);
                                  })
                                  .Where(x => x.j > pWin && x.j <= pWin + 0.5)
                                  .OrderBy(x => x.ev.HitObject!.StartTime)
                                  .Take(15))
                {
                    string column = e.ev.HitObject is IHasColumn c ? c.Column.ToString() : "-";
                    sb.AppendLine($"    {kind(e.ev)}@{e.ev.HitObject!.StartTime:F3} col={column} off={e.ev.TimeOffset:R} j={e.j:R}");
                }
            }

            foreach ((double lo, double hi, string label) in new[]
                     {
                         (0, 16.5, "j(0,16.5]"),
                         (16.5, 17.0, "j(16.5,17]"),
                         (17.0, 17.5, "j(17,17.5]"),
                         (17.5, 18.5, "j(17.5,18.5]"),
                         (18.5, 22, "j(18.5,22]"),
                     })
            {
                int great = 0, perfect = 0, greatTail = 0, perfectTail = 0;

                foreach (var e in hitEvents)
                {
                    if (e.HitObject?.HitWindows == null) continue;
                    if (e.Result is not (HitResult.Great or HitResult.Perfect)) continue;

                    bool tail = e.HitObject is TailNote;
                    double j = Math.Abs(e.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);
                    if (j <= lo || j > hi) continue;

                    if (e.Result == HitResult.Great)
                    {
                        great++;
                        if (tail) greatTail++;
                    }
                    else
                    {
                        perfect++;
                        if (tail) perfectTail++;
                    }
                }

                sb.AppendLine($"  {label}: Great={great}(T={greatTail}) Perfect={perfect}(T={perfectTail})");
            }

            sb.AppendLine("first misses:");

            foreach (var e in hitEvents.Where(ev => ev.Result == HitResult.Miss).OrderBy(ev => ev.HitObject?.StartTime ?? 0).Take(20))
            {
                string column = e.HitObject is IHasColumn c ? c.Column.ToString() : "-";
                double end = e.HitObject?.GetEndTime() ?? 0;
                double missWin = e.HitObject?.HitWindows?.WindowFor(HitResult.Miss) ?? 0;
                double absForWin = e.HitObject is TailNote
                    ? Math.Abs(e.TimeOffset) / TailNote.RELEASE_WINDOW_LENIENCE
                    : Math.Abs(e.TimeOffset);
                string tag = missWin > 0 && absForWin <= missWin ? "in-miss-win" : "outside/passive";
                sb.AppendLine($"  {kind(e)}@{e.HitObject?.StartTime:F0} end={end:F0} col={column} offset={e.TimeOffset:F1} {tag}");
            }

            // Small stored offset + Body ComboBreak: usually auto-miss after early break when the next
            // press was column-routed elsewhere (no rearm). Not EvaluateTail(ResultFor=Miss).
            sb.AppendLine("Tail Miss (|off|<40) with Head/Body:");

            foreach (var e in hitEvents.Where(ev => ev.Result == HitResult.Miss && ev.HitObject is TailNote)
                                       .Where(ev => Math.Abs(ev.TimeOffset) < 40)
                                       .OrderBy(ev => ev.HitObject!.StartTime)
                                       .Take(15))
            {
                var tail = (TailNote)e.HitObject!;
                var hold = hitEvents.Select(ev => ev.HitObject).OfType<HoldNote>()
                                    .FirstOrDefault(h => ReferenceEquals(h.Tail, tail));
                string headInfo = "-";
                string bodyInfo = "-";

                if (hold != null)
                {
                    var headEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Head));
                    var bodyEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Body));
                    headInfo = headEv.HitObject != null ? $"{headEv.Result}@{headEv.TimeOffset:F1}" : "no-head-event";
                    bodyInfo = bodyEv.HitObject != null ? $"{bodyEv.Result}" : "no-body-event";
                }

                string column = tail is IHasColumn c2 ? c2.Column.ToString() : "-";
                sb.AppendLine($"  Tail@{tail.StartTime:F0} col={column} off={e.TimeOffset:F1} head={headInfo} body={bodyInfo}");
            }

            double? sampleMissWindow = hitEvents.Select(ev => ev.HitObject?.HitWindows?.WindowFor(HitResult.Miss)).FirstOrDefault(w => w is > 0);

            if (sampleMissWindow is double missWindowMs)
            {
                int mehAtMissEdge = hitEvents.Count(ev =>
                {
                    if (ev.Result != HitResult.Meh || ev.HitObject?.HitWindows == null) return false;

                    bool tail = ev.HitObject is TailNote;
                    double j = Math.Abs(ev.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);
                    return Math.Abs(j - missWindowMs) <= 1.0 || Math.Abs(j - (missWindowMs + 0.5)) < 1e-9;
                });
                int missAtMissEdge = hitEvents.Count(ev =>
                {
                    if (ev.Result != HitResult.Miss || ev.HitObject?.HitWindows == null) return false;

                    bool tail = ev.HitObject is TailNote;
                    double j = Math.Abs(ev.TimeOffset) / (tail ? TailNote.RELEASE_WINDOW_LENIENCE : 1.0);
                    return Math.Abs(j - missWindowMs) <= 1.0 || Math.Abs(j - (missWindowMs + 0.5)) < 1e-9;
                });
                sb.AppendLine($"MissWindow={missWindowMs}: Meh near miss-edge={mehAtMissEdge}; Miss near miss-edge={missAtMissEdge}");
            }

            appendHoldOutcomeBuckets(sb, hitEvents, replay);
        }

        private static void appendHoldOutcomeBuckets(StringBuilder sb, IReadOnlyList<HitEvent> hitEvents, Replay? replay)
        {
            var holds = hitEvents.Select(ev => ev.HitObject).OfType<HoldNote>().Distinct().ToArray();
            var buckets = new Dictionary<string, int>(StringComparer.Ordinal);

            int tailMiss = 0, tailMeh = 0, tailOtherHit = 0;
            int bodyCb = 0, bodyIgnoreHit = 0, bodyMissing = 0;
            int holdIgnoreMiss = 0, holdIgnoreHit = 0;

            foreach (var hold in holds)
            {
                var headEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Head));
                var tailEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Tail));
                var bodyEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Body));
                var holdEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold));

                string head = headEv.HitObject != null ? headEv.Result.ToString() : "none";
                string tail = tailEv.HitObject != null ? tailEv.Result.ToString() : "none";
                string body = bodyEv.HitObject != null ? bodyEv.Result.ToString() : "none";
                string holdAux = holdEv.HitObject != null ? holdEv.Result.ToString() : "none";
                string key = $"H={head}|T={tail}|B={body}|P={holdAux}";
                buckets[key] = buckets.GetValueOrDefault(key) + 1;

                if (tailEv.HitObject != null)
                {
                    if (tailEv.Result == HitResult.Miss) tailMiss++;
                    else if (tailEv.Result == HitResult.Meh) tailMeh++;
                    else if (tailEv.Result.IsHit()) tailOtherHit++;
                }

                if (bodyEv.HitObject == null) bodyMissing++;
                else if (bodyEv.Result == HitResult.ComboBreak) bodyCb++;
                else if (bodyEv.Result == HitResult.IgnoreHit) bodyIgnoreHit++;

                if (holdEv.Result == HitResult.IgnoreMiss) holdIgnoreMiss++;
                else if (holdEv.Result == HitResult.IgnoreHit) holdIgnoreHit++;
            }

            sb.AppendLine($"hold outcomes: n={holds.Length} TailMiss={tailMiss} TailMeh={tailMeh} TailOtherHit={tailOtherHit} BodyCB={bodyCb} BodyIH={bodyIgnoreHit} BodyNone={bodyMissing} HoldIM={holdIgnoreMiss} HoldIH={holdIgnoreHit}");

            sb.AppendLine("hold Body=ComboBreak:");
            foreach (var kv in buckets.Where(kv => kv.Key.Contains("|B=ComboBreak|", StringComparison.Ordinal)).OrderByDescending(kv => kv.Value))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");

            sb.AppendLine("hold Tail=Meh (all):");
            foreach (var kv in buckets.Where(kv => kv.Key.Contains("|T=Meh|", StringComparison.Ordinal)).OrderByDescending(kv => kv.Value))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");

            // Head 命中但尾 Miss：典型「断连后未重臂 → 尾被动 Miss」；列出时刻便于对照回放。
            // sameColHit=同列在 (head,tail) 内命中其它物件（列路由占键 → 不应重臂）；none=期间无同列命中（若仍有空按应能重臂）。
            sb.AppendLine("hold HeadHit|TailMiss detail:");

            foreach (var hold in holds.OrderBy(h => h.StartTime))
            {
                var headEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Head));
                var tailEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Tail));
                var bodyEv = hitEvents.FirstOrDefault(ev => ReferenceEquals(ev.HitObject, hold.Body));

                if (headEv.HitObject == null || tailEv.HitObject == null)
                    continue;

                if (!headEv.Result.IsHit() || tailEv.Result != HitResult.Miss)
                    continue;

                string body = bodyEv.HitObject != null ? bodyEv.Result.ToString() : "none";

                string[] sameColHits = hitEvents
                                       .Where(ev => ev.HitObject is IHasColumn hc
                                                    && hc.Column == hold.Column
                                                    && !ReferenceEquals(ev.HitObject, hold.Head)
                                                    && !ReferenceEquals(ev.HitObject, hold.Tail)
                                                    && !ReferenceEquals(ev.HitObject, hold.Body)
                                                    && !ReferenceEquals(ev.HitObject, hold)
                                                    && ev.HitObject!.StartTime > hold.StartTime - 200
                                                    && ev.HitObject.StartTime < hold.EndTime + 400)
                                       .Select(ev =>
                                       {
                                           string k = ev.HitObject switch
                                           {
                                               HeadNote => "Head",
                                               Note => "Note",
                                               _ => ev.HitObject!.GetType().Name,
                                           };
                                           double abs = ev.HitObject!.StartTime + ev.TimeOffset;
                                           return $"{k}@{ev.HitObject.StartTime:0.###}:{ev.Result}@t{abs:0.###}";
                                       })
                                       .ToArray();

                string routeTag = sameColHits.Length == 0 ? "nearCol=none" : $"nearCol={string.Join(',', sameColHits)}";
                string repressTag = describeColumnRepress(replay, hold);
                sb.AppendLine(
                    $"  Hold@{hold.StartTime:0.###} col={hold.Column} end={hold.EndTime:0.###} H={headEv.Result}@{headEv.TimeOffset:0.###} T=Miss@{tailEv.TimeOffset:0.###} B={body} {routeTag} {repressTag}");
            }

            sb.AppendLine("Note Miss (|off|<80):");

            foreach (var ev in hitEvents.Where(e => e.HitObject is Note and not HeadNote and not TailNote && e.Result == HitResult.Miss)
                                        .OrderBy(e => e.HitObject!.StartTime))
            {
                if (Math.Abs(ev.TimeOffset) >= 80)
                    continue;

                var note = (Note)ev.HitObject!;
                double abs = note.StartTime + ev.TimeOffset;
                double missWin = note.HitWindows?.WindowFor(HitResult.Miss) ?? 0;
                // ForceMiss 用命中时刻写 offset；auto-miss 用 deadline/按键史。列边沿可区分「有按下未吃到」vs「被后键钉死」。
                string edges = describeColumnEdgesNear(replay, note.Column, note.StartTime - missWin, note.StartTime + missWin + 50);
                var laterHit = hitEvents.FirstOrDefault(x =>
                    x.Result.IsHit()
                    && x.HitObject is IHasColumn hc
                    && hc.Column == note.Column
                    && x.HitObject!.StartTime > note.StartTime
                    && Math.Abs((x.HitObject.StartTime + x.TimeOffset) - abs) < 0.75);
                string forceTag = laterHit.HitObject != null
                    ? $"forceVia={laterHit.HitObject.GetType().Name}@{laterHit.HitObject.StartTime:0.###}"
                    : "forceVia=none";

                string[] atPress = hitEvents
                                   .Where(x => x.HitObject is IHasColumn hc
                                               && hc.Column == note.Column
                                               && Math.Abs((x.HitObject!.StartTime + x.TimeOffset) - abs) < 0.75)
                                   .Select(x =>
                                   {
                                       string k = x.HitObject switch
                                       {
                                           HeadNote => "Head",
                                           TailNote => "Tail",
                                           Note => "Note",
                                           _ => x.HitObject!.GetType().Name,
                                       };
                                       return $"{k}@{x.HitObject!.StartTime:0.###}:{x.Result}";
                                   })
                                   .ToArray();
                string atTag = atPress.Length == 0 ? "atPress=none" : $"atPress={string.Join(',', atPress)}";

                string[] earlierUnhit = hitEvents
                                        .Where(x => x.HitObject is IHasColumn hc
                                                    && hc.Column == note.Column
                                                    && x.HitObject is Note
                                                    && x.HitObject!.StartTime < note.StartTime
                                                    && x.HitObject.StartTime > note.StartTime - missWin - 50
                                                    && (x.Result == HitResult.Miss || !x.Result.IsHit()))
                                        .Select(x => $"{x.HitObject!.StartTime:0.###}:{x.Result}")
                                        .ToArray();
                string lockTag = earlierUnhit.Length == 0 ? "prev=none" : $"prevMiss={string.Join(',', earlierUnhit)}";

                sb.AppendLine($"  Note@{note.StartTime:0.###} col={note.Column} off={ev.TimeOffset:0.###} abs={abs:0.###} {forceTag} {atTag} {lockTag} {edges}");
            }

            sb.AppendLine("hold outcome top:");
            foreach (var kv in buckets.OrderByDescending(kv => kv.Value).Take(12))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
        }

        /// <summary>
        /// 在 (head+1ms, tail+Meh*lenience] 内该列的按下边沿：empty=可能重臂；无边沿=未重按。
        /// </summary>
        private static string describeColumnRepress(Replay? replay, HoldNote hold)
        {
            double to = hold.EndTime + (hold.Tail.HitWindows?.WindowFor(HitResult.Meh) ?? 0) * TailNote.RELEASE_WINDOW_LENIENCE;
            return describeColumnEdgesNear(replay, hold.Column, hold.StartTime + 1, to, "repress");
        }

        private static string describeColumnEdgesNear(Replay? replay, int column, double from, double to, string label = "edges")
        {
            if (replay?.Frames == null || replay.Frames.Count == 0)
                return $"{label}=?";

            var action = ManiaAction.Key1 + column;
            bool wasDown = false;
            var edges = new List<string>();

            foreach (var frame in replay.Frames.OrderBy(f => f.Time))
            {
                if (frame.Time < from)
                {
                    if (frame is ManiaReplayFrame early)
                        wasDown = early.Actions.Contains(action);
                    continue;
                }

                if (frame.Time > to)
                    break;

                if (frame is not ManiaReplayFrame mania)
                    continue;

                bool down = mania.Actions.Contains(action);

                if (down && !wasDown)
                    edges.Add($"↓{frame.Time:0.###}");
                else if (!down && wasDown)
                    edges.Add($"↑{frame.Time:0.###}");

                wasDown = down;
            }

            return edges.Count == 0 ? $"{label}=none" : $"{label}={string.Join(',', edges)}";
        }

        private static void archiveReport(AuditFixture fixture, string report)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"mania_osr_audit_{fixture.Name}.txt");
            File.WriteAllText(path, report);
            TestContext.AddTestAttachment(path);
            TestContext.WriteLine(report);
        }

        /// <summary>
        /// 现行 Session 的冻结锚点：已与解码头逐项对齐的档用它做精确门禁。
        /// </summary>
        public sealed record SessionAnchor(
            long TotalScore,
            int MaxCombo,
            double Accuracy,
            IReadOnlyDictionary<HitResult, int> Statistics);

        /// <summary>
        /// 仍在向解码头收敛的档的门禁上限：只钉现行 Session 与解码头（stable 原生量）的差距。
        /// </summary>
        /// <remarks>
        /// 逐项钉死现行输出会让每次「更接近 stable」的改动都被判红、必须重钉断言。改钉差距后
        /// 更近通过、更远失败。<see cref="StatisticGaps"/> 未列出的键按 0 处理（要求精确相等）。
        /// 解码头 <c>Accuracy</c> 已被 lazer 迁移重算，不是 stable 原生量，故不入门禁。
        /// </remarks>
        public sealed record SessionBound(
            long TotalScoreGap,
            IReadOnlyDictionary<HitResult, int> StatisticGaps);

        public sealed record AuditFixture(
            string Name,
            string OsrResource,
            string BeatmapResource,
            EzEnumJudgeMode JudgeMode,
            string? ExpectedBeatmapMd5,
            long ExpectedTotalScore,
            int ExpectedMaxCombo,
            double ExpectedAccuracy,
            ScoreRank? ExpectedRank,
            int? ExpectedTotalScoreVersion,
            bool? ExpectedIsLegacy,
            IReadOnlyList<string> ExpectedModAcronyms,
            IReadOnlyDictionary<HitResult, int> ExpectedStatistics,
            bool AssertFullNonZeroStatistics,
            bool AssertDecodedHeader,
            SessionAnchor? SessionAnchor,
            SessionBound? SessionBound)
        {
            public override string ToString()
            {
                return Name;
            }
        }

        private sealed class HarnessScoreDecoder : LegacyScoreDecoder
        {
            public WorkingBeatmap? LastWorkingBeatmap { get; private set; }
            private readonly string beatmapResource;

            public HarnessScoreDecoder(string beatmapResource)
            {
                this.beatmapResource = beatmapResource;
            }

            protected override Ruleset GetRuleset(int rulesetId)
            {
                return new ManiaRuleset();
            }

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                using var stream = resources.GetStream(beatmapResource);
                IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));
                decoded.BeatmapInfo.MD5Hash = md5Hash;
                LastWorkingBeatmap = new TestWorkingBeatmap(decoded);
                return LastWorkingBeatmap;
            }
        }
    }
}
