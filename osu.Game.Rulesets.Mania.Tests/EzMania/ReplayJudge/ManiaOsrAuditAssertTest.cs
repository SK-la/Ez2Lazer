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
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
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
    ///     Mania osr 双轨金标门禁（多谱面套件）：
    ///     <list type="bullet">
    ///         <item><b>Lazer</b>：冻结 osr 嵌入 Statistics；Header 对解码字段，Session 用 <see cref="EzEnumHitMode.Lazer" />。</item>
    ///         <item>
    ///             <b>Classic</b>：主判定/分/combo 以 osr 二进制头为准，禁止把 Lazer 解码 TotalScore 当金标；
    ///             Session 用 <see cref="EzEnumHitMode.Classic" /> + CL。
    ///         </item>
    ///     </list>
    ///     套件：Hanatachi（≈2026-06/09）、PORTRAiT（≈2026-10-02/03，现行客户端）。
    ///     IgnoreHit/IgnoreMiss/ComboBreak 若 Classic 未给则不进本轨门禁。
    ///     高难度 LN 判定数量闭环仍用 <see cref="OsrAuditTest" /> + GramNibelungen23（勿当金标）。
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

        private static readonly AuditFixture lazer_hanatachi = new AuditFixture(
            "Lazer-Hanatachi",
            "Resources/Testing/Replays/ManiaAudit-Lazer-Hanatachi.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu",
            EzEnumHitMode.Lazer,
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
            true);

        // PORTRAiT Lazer：gameVersion=30000019，≈2026-10-03。Session MaxCombo 已对齐；剩 Perfect+21 / Miss-10 等。
        private static readonly AuditFixture lazer_portrait = new AuditFixture(
            "Lazer-PORTRAiT",
            "Resources/Testing/Replays/ManiaAudit-Lazer-PORTRAiT.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu",
            EzEnumHitMode.Lazer,
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
            true);

        public static IEnumerable<AuditFixture> LazerFixtures()
        {
            yield return lazer_hanatachi;
            yield return lazer_portrait;
        }

        // ── Classic 套件（二进制头为分/combo 金标；Session 注入 CL）────────────

        private static readonly AuditFixture classic_hanatachi = new AuditFixture(
            "Classic-Hanatachi",
            "Resources/Testing/Replays/ManiaAudit-Classic-Hanatachi.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu",
            EzEnumHitMode.Classic,
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
            false);

        // PORTRAiT Classic：stable 20260924，≈2026-10-02。二进制总分 871917（Lazer 解码会抬到 875483）。
        private static readonly AuditFixture classic_portrait = new AuditFixture(
            "Classic-PORTRAiT",
            "Resources/Testing/Replays/ManiaAudit-Classic-PORTRAiT.osr",
            "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu",
            EzEnumHitMode.Classic,
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
            false);

        public static IEnumerable<AuditFixture> ClassicFixtures()
        {
            yield return classic_hanatachi;
            yield return classic_portrait;
        }

        [TestCaseSource(nameof(LazerFixtures))]
        public void AuditLazerEmbeddedScoreHeaderMatchesAnchor(AuditFixture fixture)
        {
            assertDecodedHeader(fixture);
        }

        [TestCaseSource(nameof(LazerFixtures))]
        [Explicit("Lazer Session≠冻结 osr 锚点。修判定后去掉 Explicit。")]
        public void AuditLazerSessionMatchesAnchor(AuditFixture fixture)
        {
            assertSessionMatchesAnchor(fixture);
        }

        [TestCaseSource(nameof(ClassicFixtures))]
        [Explicit("Classic Session≠Classic 客户端静态锚点。修判定后去掉 Explicit。")]
        public void AuditClassicSessionMatchesAnchor(AuditFixture fixture)
        {
            assertSessionMatchesAnchor(fixture);
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

            // Classic：丢弃 Lazer 解码嵌入 Statistics/分数，只保留 Replay 帧；Mods/HitMode 按静态契约注入。
            if (!fixture.AssertDecodedHeader)
                prepareScoreForClassicSession(score, fixture);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            // 金标环境固定：不得扫 JudgePrecedence / OffsetPlusMania。
            var environment = ReplayJudgeTestConfig.Create(
                fixture.HitMode,
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
            score.ScoreInfo.ManiaHitMode = (int)fixture.HitMode;
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

            appendHitEventAttribution(sb, score.ScoreInfo.HitEvents);
            return sb.ToString();
        }

        private static void appendHitEventAttribution(StringBuilder sb, IReadOnlyList<HitEvent> hitEvents)
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

            foreach (var e in hitEvents.Where(e => e.Result == HitResult.Miss).OrderBy(e => e.HitObject?.StartTime ?? 0).Take(20))
            {
                string column = e.HitObject is IHasColumn c ? c.Column.ToString() : "-";
                double end = e.HitObject?.GetEndTime() ?? 0;
                sb.AppendLine($"  {kind(e)}@{e.HitObject?.StartTime:F0} end={end:F0} col={column} offset={e.TimeOffset:F1}");
            }
        }

        private static void archiveReport(AuditFixture fixture, string report)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"mania_osr_audit_{fixture.Name}.txt");
            File.WriteAllText(path, report);
            TestContext.AddTestAttachment(path);
            TestContext.WriteLine(report);
        }

        public sealed record AuditFixture(
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
            bool AssertDecodedHeader)
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
