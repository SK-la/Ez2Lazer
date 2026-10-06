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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania osr 双轨金标门禁：
    /// <list type="bullet">
    /// <item><b>Lazer</b>：Eviternity，Lazer 客户端成绩；Header 对解码嵌入字段，Session 用 <see cref="EzEnumHitMode.Lazer"/>。</item>
    /// <item><b>Classic</b>：Hanatachi，Classic 客户端静态锚点；禁止把 osr 经 Lazer 解码嵌入 Statistics/TotalScore 当金标
    /// （会经 Lazer 机制二次处理）。osr 仅提供 replay 帧 + beatmap MD5；Session 用 <see cref="EzEnumHitMode.Classic"/> + DT/CL。</item>
    /// </list>
    /// 用户每次给出主判定/分/combo/acc/mods 后，夹具须补齐：MD5、Rank、精确 Acc、IsLegacy 等；
    /// IgnoreHit/IgnoreMiss/ComboBreak 若用户未给则不进 Classic 门禁（待补）。
    /// 高难度 LN 判定数量闭环仍用 <see cref="OsrAuditTest"/> + GramNibelungen23（勿当金标）。
    /// </summary>
    [TestFixture]
    public class ManiaOsrAuditAssertTest
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(ManiaOsrAuditAssertTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        // ── Lazer 轨（Eviternity / Lazer 客户端冻结 osr）────────────────────────

        private static readonly AuditFixture lazer = new AuditFixture(
            Name: "Lazer-Eviternity",
            OsrResource: "Resources/Testing/Replays/ManiaAudit-Lazer-solo-replay-mania_4065226_2514660202.osr",
            BeatmapResource: "Resources/Testing/Beatmaps/ManiaAudit-Lazer-Xyris - Eviternity (Critical_Star) [Eternity].osu",
            HitMode: EzEnumHitMode.Lazer,
            ExpectedBeatmapMd5: "a3b809c1406a9c47c1af6600743511b6",
            ExpectedTotalScore: 835479,
            ExpectedMaxCombo: 673,
            ExpectedAccuracy: 0.9597103052141494,
            ExpectedRank: ScoreRank.S,
            ExpectedTotalScoreVersion: 30000019,
            ExpectedIsLegacy: false,
            ExpectedModAcronyms: System.Array.Empty<string>(),
            ExpectedStatistics: new Dictionary<HitResult, int>
            {
                [HitResult.Miss] = 40,
                [HitResult.Meh] = 10,
                [HitResult.Ok] = 24,
                [HitResult.Good] = 275,
                [HitResult.Great] = 1584,
                [HitResult.Perfect] = 2662,
                [HitResult.IgnoreMiss] = 12,
                [HitResult.IgnoreHit] = 2064,
                [HitResult.ComboBreak] = 12,
            },
            AssertFullNonZeroStatistics: true,
            AssertDecodedHeader: true);

        // ── Classic 轨（Hanatachi / Classic 客户端静态锚点；osr 只供帧）──────────
        //
        // 用户提供（Classic 客户端展示，禁止用 Lazer 解码嵌入分顶替）：
        //   TotalScore=915171 Acc≈97.70% MaxCombo=1553
        //   Perfect=3100 Great=1127 Good=110 Ok=20 Meh=6 Miss=26
        //   Mods=DoubleTime,Classic
        // 补充：
        //   MD5=osr/谱面头 29278f227275b1720e995d532130710b（元数据，非 Statistics）
        //   Acc 精确值=0.9770327457989235（305 权显示 97.70%；与 HitMode.Classic 现行 300/300 权不同，收敛时一并处理）
        //   Rank=S（acc≥0.95 且非全 Perfect/Great）
        //   IsLegacy=true（Classic 客户端）
        //   未给：IgnoreHit / IgnoreMiss / ComboBreak → 不进本轨门禁
        // 备注：osr 二进制头 TotalScore=915692、mods 位仅 DT；金标以用户静态为准。

        private static readonly AuditFixture classic = new AuditFixture(
            Name: "Classic-Hanatachi",
            OsrResource: "Resources/Testing/Replays/ManiaAudit-Classic-Hanatachi.osr",
            BeatmapResource: "Resources/Testing/Beatmaps/ManiaAudit-Classic-Hanatachi.osu",
            HitMode: EzEnumHitMode.Classic,
            ExpectedBeatmapMd5: "29278f227275b1720e995d532130710b",
            ExpectedTotalScore: 915171,
            ExpectedMaxCombo: 1553,
            ExpectedAccuracy: 0.9770327457989235,
            ExpectedRank: ScoreRank.S,
            ExpectedTotalScoreVersion: null,
            ExpectedIsLegacy: true,
            ExpectedModAcronyms: new[] { "DT", "CL" },
            ExpectedStatistics: new Dictionary<HitResult, int>
            {
                [HitResult.Miss] = 26,
                [HitResult.Meh] = 6,
                [HitResult.Ok] = 20,
                [HitResult.Good] = 110,
                [HitResult.Great] = 1127,
                [HitResult.Perfect] = 3100,
            },
            AssertFullNonZeroStatistics: false,
            AssertDecodedHeader: false);

        [Test]
        public void AuditLazerEmbeddedScoreHeaderMatchesAnchor()
            => assertDecodedHeader(lazer);

        [Test]
        [Explicit("Lazer Session≠冻结 osr 锚点。修判定后去掉 Explicit。")]
        public void AuditLazerSessionMatchesAnchor()
            => assertSessionMatchesAnchor(lazer);

        [Test]
        [Explicit("Classic Session≠Classic 客户端静态锚点。修判定后去掉 Explicit。")]
        public void AuditClassicSessionMatchesAnchor()
            => assertSessionMatchesAnchor(classic);

        private static void assertDecodedHeader(AuditFixture fixture)
        {
            Assume.That(fixture.AssertDecodedHeader, "本夹具不以解码 Header 为门禁");
            assumeResourcesPresent(fixture);

            var decoder = new HarnessScoreDecoder(fixture.BeatmapResource);
            Score score;

            using (var stream = resources.GetStream(fixture.OsrResource))
                score = decoder.Parse(stream);

            assertAgainstAnchor(score.ScoreInfo, fixture, report: null, afterSession: false);
        }

        private static void assertSessionMatchesAnchor(AuditFixture fixture)
        {
            assumeResourcesPresent(fixture);

            var decoder = new HarnessScoreDecoder(fixture.BeatmapResource);
            Score score;

            using (var stream = resources.GetStream(fixture.OsrResource))
                score = decoder.Parse(stream);

            // Classic：丢弃 Lazer 解码嵌入 Statistics/分数，只保留 Replay 帧；Mods/HitMode 按静态契约注入。
            if (!fixture.AssertDecodedHeader)
                prepareScoreForClassicSession(score, fixture);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            // 金标环境固定：不得扫 JudgePrecedence / OffsetPlusMania。
            var environment = ReplayJudgeTestConfig.Create(
                fixture.HitMode,
                EzEnumHealthMode.Lazer,
                EzEnumJudgePrecedence.Earliest,
                offsetPlusMania: 0);

            ManiaReplaySession.Run(score, playable, environment);

            string report = buildReport(score, fixture);
            archiveReport(fixture, report);
            assertAgainstAnchor(score.ScoreInfo, fixture, report, afterSession: true);
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
            score.ScoreInfo.ManiaHitMode = (int)fixture.HitMode;
            score.ScoreInfo.IsLegacyScore = fixture.ExpectedIsLegacy ?? true;
        }

        private static Mod[] resolveMods(Ruleset ruleset, IReadOnlyList<string> acronyms)
        {
            if (acronyms.Count == 0)
                return System.Array.Empty<Mod>();

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
            Assert.Multiple(() =>
            {
                if (fixture.ExpectedBeatmapMd5 != null)
                    Assert.That(info.BeatmapInfo?.MD5Hash, Is.EqualTo(fixture.ExpectedBeatmapMd5), report);

                Assert.That(info.TotalScore, Is.EqualTo(fixture.ExpectedTotalScore), report);
                Assert.That(info.MaxCombo, Is.EqualTo(fixture.ExpectedMaxCombo), report);
                // Classic 展示 Acc 与 HitMode.Classic 权值可能暂不一致；主闸是 Statistics/Score/Combo。
                double accTolerance = fixture.AssertDecodedHeader ? 1e-12 : 5e-3;
                Assert.That(info.Accuracy, Is.EqualTo(fixture.ExpectedAccuracy).Within(accTolerance), report);

                if (fixture.ExpectedRank is ScoreRank rank)
                    Assert.That(info.Rank, Is.EqualTo(rank), report);

                if (fixture.ExpectedTotalScoreVersion is int version)
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

                assertStatisticsEqual(info.Statistics, fixture.ExpectedStatistics, fixture.AssertFullNonZeroStatistics, report);
            });
        }

        private static void assertStatisticsEqual(
            IReadOnlyDictionary<HitResult, int> actual,
            IReadOnlyDictionary<HitResult, int> expected,
            bool assertFullNonZero,
            string? report)
        {
            // assertFullNonZero：Lazer 全量非零对账（含 Ignore*/ComboBreak）。
            // 否则只对 expected 键（Classic 用户主判定；未给的辅判不门禁）。
            HitResult[] keys = assertFullNonZero
                ? expected.Keys.Union(actual.Keys).Where(k => actual.GetValueOrDefault(k) != 0 || expected.GetValueOrDefault(k) != 0).OrderBy(k => k).ToArray()
                : expected.Keys.OrderBy(k => k).ToArray();

            var actualVals = keys.Select(k => $"{k}={actual.GetValueOrDefault(k)}").ToArray();
            var expectedVals = keys.Select(k => $"{k}={expected.GetValueOrDefault(k)}").ToArray();

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
            sb.AppendLine($"hitMode: {fixture.HitMode}");
            sb.AppendLine($"env: HitMode={fixture.HitMode} HealthMode=Lazer JudgePrecedence=Earliest OffsetPlusMania=0");
            sb.AppendLine($"anchor: acc={fixture.ExpectedAccuracy:R} total={fixture.ExpectedTotalScore} combo={fixture.ExpectedMaxCombo}");
            sb.AppendLine($"session: acc={score.ScoreInfo.Accuracy:R} rank={score.ScoreInfo.Rank} total={score.ScoreInfo.TotalScore} combo={score.ScoreInfo.MaxCombo}");
            sb.AppendLine($"session mods: {string.Join(",", score.ScoreInfo.Mods.Select(m => m.Acronym))}");
            sb.AppendLine($"session stats: {string.Join(", ", score.ScoreInfo.Statistics.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
            sb.AppendLine("delta (session-anchor on expected keys):");

            foreach (var key in fixture.ExpectedStatistics.Keys.OrderBy(k => k))
            {
                int delta = score.ScoreInfo.Statistics.GetValueOrDefault(key) - fixture.ExpectedStatistics[key];
                if (delta != 0)
                    sb.AppendLine($"  {key}: {delta:+#;-#;0}");
            }

            long scoreDelta = score.ScoreInfo.TotalScore - fixture.ExpectedTotalScore;
            int comboDelta = score.ScoreInfo.MaxCombo - fixture.ExpectedMaxCombo;
            if (scoreDelta != 0)
                sb.AppendLine($"  TotalScore: {scoreDelta:+#;-#;0}");
            if (comboDelta != 0)
                sb.AppendLine($"  MaxCombo: {comboDelta:+#;-#;0}");

            return sb.ToString();
        }

        private static void archiveReport(AuditFixture fixture, string report)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"mania_osr_audit_{fixture.Name}.txt");
            File.WriteAllText(path, report);
            TestContext.AddTestAttachment(path);
            TestContext.WriteLine(report);
        }

        private sealed record AuditFixture(
            string Name,
            string OsrResource,
            string BeatmapResource,
            EzEnumHitMode HitMode,
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
            bool AssertDecodedHeader);

        private sealed class HarnessScoreDecoder : LegacyScoreDecoder
        {
            private readonly string beatmapResource;

            public WorkingBeatmap? LastWorkingBeatmap { get; private set; }

            public HarnessScoreDecoder(string beatmapResource)
            {
                this.beatmapResource = beatmapResource;
            }

            protected override Ruleset GetRuleset(int rulesetId) => new ManiaRuleset();

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
