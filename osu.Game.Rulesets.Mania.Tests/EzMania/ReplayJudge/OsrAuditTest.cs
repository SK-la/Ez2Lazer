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
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 问题成绩对账：读内嵌的 .osr → 取原成绩 Statistics → 在真实谱面上跑 <see cref="ManiaReplaySession"/>
    /// → 逐类判定 delta，并对尾判做假 Miss 归因。
    /// </summary>
    /// <remarks>
    /// 资源来自 <c>Resources/Testing</c>（由 <c>Directory.Build.props</c> 的 <c>Resources\**</c> 通配嵌入），
    /// 不依赖本机 osu 数据目录，也不打开 Realm。
    /// </remarks>
    [TestFixture]
    public class OsrAuditTest
    {
        private const string report_file_name = "osr_audit.txt";

        private static readonly DllResourceStore resources = new DllResourceStore(typeof(OsrAuditTest).Assembly);

        /// <summary>本机复现「无法结算 → 强制结算」那一局：53 miss、Lazer、offset 0、6K LN 图。</summary>
        private const string osr_resource = "Resources/Testing/Replays/GramNibelungen23-53miss.osr";

        /// <summary>配套谱面；.osr 里带的是它的 MD5。</summary>
        private const string beatmap_resource = "Resources/Testing/Beatmaps/GramNibelungen23.osu";

        [Test]
        [Explicit("问题成绩人工对账：需要 Resources/Testing 下的 osr+谱面。CI 默认不跑，改动 Session 判定后手动执行。")]
        public void AuditProblemScore()
        {
            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osr_resource))
                score = decoder.Parse(stream);

            var sb = new StringBuilder();
            sb.AppendLine($"osr: {osr_resource}");
            sb.AppendLine($"beatmap: {score.ScoreInfo.BeatmapInfo}");
            sb.AppendLine($"embedded ManiaHitMode={score.ScoreInfo.ManiaHitMode} ManiaHealthMode={score.ScoreInfo.ManiaHealthMode}");
            sb.AppendLine($"mods: {string.Join(",", score.ScoreInfo.Mods.Select(m => m.Acronym))}");
            sb.AppendLine($"imported version={score.ScoreInfo.TotalScoreVersion} legacy={score.ScoreInfo.IsLegacyScore}");
            sb.AppendLine($"original stats: {describeCounts(score.ScoreInfo.Statistics)}");
            sb.AppendLine($"original max stats: {describeCounts(score.ScoreInfo.MaximumStatistics)}");

            var original = new Dictionary<HitResult, int>(score.ScoreInfo.Statistics);

            var allFrames = score.Replay.Frames.ToList();
            var maniaFrames = allFrames.OfType<ManiaReplayFrame>().ToList();

            sb.AppendLine();
            sb.AppendLine($"frames: total={allFrames.Count} mania={maniaFrames.Count} other={allFrames.Count - maniaFrames.Count}");
            sb.AppendLine($"  first={allFrames.FirstOrDefault()?.Time:F0} last={allFrames.LastOrDefault()?.Time:F0}");

            if (allFrames.Count > 0)
            {
                var sample = maniaFrames.Take(5).Select(f => $"{f.Time:F0}:[{string.Join("|", f.Actions)}]");
                sb.AppendLine($"  sample: {string.Join(" ", sample)}");
            }

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);

            sb.AppendLine();
            sb.AppendLine($"objects: taps={countObjects<Note>(playable, exact: true)} heads={countObjects<HeadNote>(playable)} "
                          + $"tails={countObjects<TailNote>(playable)} ticks={countObjects<HoldNoteTick>(playable)} "
                          + $"holds={countObjects<HoldNote>(playable, exact: true)}");

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

            ManiaReplaySession.Run(score, playable, environment);

            var sessionCounts = new Dictionary<HitResult, int>(score.ScoreInfo.Statistics);
            sb.AppendLine();
            sb.AppendLine($"session stats: {describeCounts(sessionCounts)}");
            sb.AppendLine($"delta:         {describeDelta(original, sessionCounts)}");

            // 跨 JudgePrecedence 对比：定位 200+ 是否只出在某一策略路径。
            sb.AppendLine();
            sb.AppendLine("JudgePrecedence 对比（同一 replay / 同一谱面）:");

            foreach (var precedence in Enum.GetValues<EzEnumJudgePrecedence>())
            {
                var rerun = new HarnessScoreDecoder();
                Score copy;

                using (var stream = resources.GetStream(osr_resource))
                    copy = rerun.Parse(stream);

                var copyBeatmap = rerun.LastWorkingBeatmap!.GetPlayableBeatmap(copy.ScoreInfo.Ruleset, copy.ScoreInfo.Mods);
                var env = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer, precedence);

                ManiaReplaySession.Run(copy, copyBeatmap, env);

                var counts = new Dictionary<HitResult, int>(copy.ScoreInfo.Statistics);
                int tailMisses = copy.ScoreInfo.HitEvents.Count(e => e.Result == HitResult.Miss && e.HitObject is TailNote);

                sb.AppendLine($"  {precedence,-8} Miss={counts.GetValueOrDefault(HitResult.Miss),-4} Meh={counts.GetValueOrDefault(HitResult.Meh),-5} "
                              + $"Perfect={counts.GetValueOrDefault(HitResult.Perfect),-5} tailMiss={tailMisses}");
            }

            var edges = collectEdges(maniaFrames);

            sb.AppendLine();
            sb.AppendLine($"edges: {string.Join(", ", edges.releases.OrderBy(kv => kv.Key).Select(kv => $"col{kv.Key}: press={edges.presses.GetValueOrDefault(kv.Key, Array.Empty<double>()).Length} release={kv.Value.Length}"))}");

            var misses = score.ScoreInfo.HitEvents.Where(e => e.Result == HitResult.Miss).ToList();

            var tailEvents = score.ScoreInfo.HitEvents.Where(e => e.HitObject is TailNote).ToList();
            sb.AppendLine();
            sb.AppendLine($"tail events: {tailEvents.Count} (总尾数 {countObjects<TailNote>(playable)})");
            foreach (var g in tailEvents.GroupBy(e => e.Result).OrderBy(g => g.Key))
                sb.AppendLine($"  {g.Key}: {g.Count()}");

            // 判定：(a) rawOffset 大 → 窗口本身就是 Meh；(b) rawOffset 小却 Meh → 被 headHit/holdBreak 封顶。
            sb.AppendLine("  Meh 尾 |offset| 分布: " + describeOffsetBuckets(tailEvents.Where(e => e.Result == HitResult.Meh)));
            sb.AppendLine("  命中尾 |offset| 分布: " + describeOffsetBuckets(tailEvents.Where(e => e.Result.IsHit())));
            sb.AppendLine("  Miss 尾 |offset| 分布: " + describeOffsetBuckets(tailEvents.Where(e => e.Result == HitResult.Miss)));

            // 判别 headHit 封顶分支是否被误触发：Meh 尾（小 offset）对应的头在 Session 里是否命中。
            var headResult = new Dictionary<HeadNote, HitResult>();
            var tailResult = new Dictionary<TailNote, HitResult>();
            var tailOffset = new Dictionary<TailNote, double>();

            foreach (var e in score.ScoreInfo.HitEvents)
            {
                if (e.HitObject is HeadNote h)
                    headResult[h] = e.Result;

                if (e.HitObject is TailNote t)
                {
                    tailResult[t] = e.Result;
                    tailOffset[t] = e.TimeOffset;
                }
            }

            var holdList = enumerate(playable).OfType<HoldNote>().ToList();
            int headHitButMehTail = 0, headMissAndMehTail = 0, headUnknownMehTail = 0;

            foreach (var e in tailEvents.Where(e => e.Result == HitResult.Meh && Math.Abs(e.TimeOffset) < 50))
            {
                var tail = (TailNote)e.HitObject;
                var hold = holdList.FirstOrDefault(h => ReferenceEquals(h.Tail, tail));

                if (hold == null)
                    headUnknownMehTail++;
                else if (headResult.TryGetValue(hold.Head, out var hr) && hr.IsHit())
                    headHitButMehTail++;
                else
                    headMissAndMehTail++;
            }

            sb.AppendLine();
            sb.AppendLine($"小 offset(<50) 的 Meh 尾归因: 头命中却被压 Meh={headHitButMehTail}, 头确实 Miss={headMissAndMehTail}, 找不到头={headUnknownMehTail}");
            sb.AppendLine($"头判定分布: {string.Join(", ", headResult.GroupBy(kv => kv.Value).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"))}");

            sb.AppendLine();
            sb.AppendLine($"session misses: {misses.Count} (original miss={original.GetValueOrDefault(HitResult.Miss)})");
            sb.AppendLine($"  by type: {string.Join(", ", misses.GroupBy(e => e.HitObject.GetType().Name).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"))}");

            // 假设：同一帧内「松手 + 重按」净状态不变 → ManiaReplayFrameEdgeParser 不产 release/press 边沿，
            // 该 LN 尾永不进入候选、最后被 applyForcedMisses 补 Miss。
            var downByTime = maniaFrames.GroupBy(f => Math.Round(f.Time, 3))
                                        .OrderBy(g => g.Key)
                                        .Select(g => (Time: g.Key, Columns: g.SelectMany(f => f.Actions).Select(a => (int)a).Where(c => c >= 0).ToHashSet()))
                                        .ToList();

            var frameDeltas = downByTime.Zip(downByTime.Skip(1), (a, b) => b.Time - a.Time).Where(d => d > 0).OrderBy(d => d).ToList();
            double frameInterval = frameDeltas.Count > 0 ? frameDeltas[frameDeltas.Count / 2] : 0;

            var lostPairs = new List<(double Time, int Column)>();

            for (int i = 1; i < downByTime.Count - 1; i++)
            {
                for (int c = 0; c < 6; c++)
                {
                    if (downByTime[i - 1].Columns.Contains(c) && !downByTime[i].Columns.Contains(c) && downByTime[i + 1].Columns.Contains(c))
                        lostPairs.Add((downByTime[i].Time, c));
                }
            }

            sb.AppendLine();
            sb.AppendLine($"frameInterval(median)={frameInterval:F1}ms  distinctTimes={downByTime.Count}");
            sb.AppendLine($"同帧「松手+重按」丢失边沿: {lostPairs.Count} 处, 按列 "
                          + string.Join(", ", Enumerable.Range(0, 6).Select(c => $"col{c}={lostPairs.Count(p => p.Column == c)}")));

            int correlated = 0, uncorrelated = 0;

            foreach (var e in misses.Where(m => m.HitObject is TailNote))
            {
                int column = ((IHasColumn)e.HitObject).Column;
                double end = e.HitObject.GetEndTime();
                bool nearby = lostPairs.Any(p => p.Column == column && Math.Abs(p.Time - end) <= frameInterval * 3);

                if (nearby) correlated++;
                else uncorrelated++;
            }

            sb.AppendLine($"假尾 Miss 归因: 同一帧丢失边沿吻合={correlated}, 不吻合={uncorrelated}");

            // 与 Session 实际吃到的边沿对账（ManiaReplayFrameEdgeParser 是 internal，测试程序集可见）。
            var inputData = ManiaReplayFrameEdgeParser.ParseAll(score.Replay, 0);

            sb.AppendLine();
            sb.AppendLine("parser 边沿 vs 本报告提取的边沿:");

            for (int c = 0; c < 6; c++)
            {
                int parserPress = inputData.PressTimesByColumn.TryGetValue(c, out var pl) ? pl.Count : 0;
                int parserRelease = inputData.SortedEvents.Count(e => !e.IsPress && e.Column == c);
                int myPress = edges.presses.TryGetValue(c, out double[]? mp) ? mp.Length : 0;
                int myRelease = edges.releases.TryGetValue(c, out double[]? mr) ? mr.Length : 0;

                sb.AppendLine($"  col{c}: parser press={parserPress} release={parserRelease} | extracted press={myPress} release={myRelease}");
            }

            int probeColumn = ((IHasColumn)misses.First(m => m.HitObject is TailNote).HitObject).Column;
            double probeEnd = misses.First(m => m.HitObject is TailNote).HitObject.StartTime;

            sb.AppendLine();
            sb.AppendLine("残余尾 Miss 逐个看原始边沿:");

            foreach (var e in misses.Where(m => m.HitObject is TailNote).OrderBy(m => m.HitObject.StartTime).Take(4))
            {
                int column = ((IHasColumn)e.HitObject).Column;
                double end = e.HitObject.GetEndTime();
                bool lostNearby = lostPairs.Any(p => p.Column == column && Math.Abs(p.Time - end) <= frameInterval);
                var probedHold = holdList.FirstOrDefault(h => Math.Abs(h.Tail.StartTime - end) < 1);

                string headInfo = probedHold == null
                    ? "找不到对应 HoldNote"
                    : $"头 t={probedHold.Head.StartTime:F0} session判定={headResult.GetValueOrDefault(probedHold.Head, HitResult.None)} "
                      + $"头窗口[{probedHold.Head.StartTime - probedHold.Head.MaximumJudgementOffset:F0}..{probedHold.Head.StartTime + probedHold.Head.MaximumJudgementOffset:F0}]";

                sb.AppendLine($"-- col{column} tail={end:F0} storedOffset={e.TimeOffset:F0} 同帧丢失边沿={lostNearby}");
                sb.AppendLine($"     {headInfo}");

                foreach (var ev in inputData.SortedEvents.Where(ev => ev.Column == column && Math.Abs(ev.Time - end) <= 300).OrderBy(ev => ev.Time))
                    sb.AppendLine($"     t={ev.Time:F0} {(ev.IsPress ? "press" : "release")}");

                sb.AppendLine("     frames: " + string.Join(" ", downByTime.Where(d => Math.Abs(d.Time - end) <= 60)
                                                                           .Select(d => $"{d.Time:F0}[{(d.Columns.Contains(column) ? "D" : "-")}]")));

                sb.AppendLine("     同列附近尾: " + string.Join(" | ", holdList.Where(h => h.Column == column && Math.Abs(h.Tail.StartTime - end) <= 400)
                                                                          .OrderBy(h => h.Tail.StartTime)
                                                                          .Select(h => $"{h.Tail.StartTime:F0}:{tailResult.GetValueOrDefault(h.Tail, HitResult.None)}/{tailOffset.GetValueOrDefault(h.Tail, double.NaN):F0}")));
            }

            var falseTailMisses = new List<string>();
            int trueTailMisses = 0;

            foreach (var e in misses.Where(m => m.HitObject is TailNote).OrderBy(m => m.HitObject.StartTime))
            {
                var tail = (TailNote)e.HitObject;
                int column = ((IHasColumn)e.HitObject).Column;
                double[] releases = edges.releases.TryGetValue(column, out double[]? r) ? r : Array.Empty<double>();

                double lenience = tail.MaximumJudgementOffset;
                double nearest = releases.Length == 0 ? double.NaN : releases.OrderBy(t => Math.Abs(t - tail.StartTime)).First();
                bool withinLenience = !double.IsNaN(nearest) && Math.Abs(nearest - tail.StartTime) <= lenience;

                if (withinLenience)
                    falseTailMisses.Add($"col{column} tail={tail.StartTime:F0} release={nearest:F0} d={(nearest - tail.StartTime):F0} lenience={lenience:F0} offset={e.TimeOffset:F1}");
                else
                    trueTailMisses++;
            }

            sb.AppendLine();
            sb.AppendLine($"tail misses: {misses.Count(m => m.HitObject is TailNote)} "
                          + $"(release-inside-lenience={falseTailMisses.Count} -> 假 Miss, 其余={trueTailMisses})");

            foreach (string line in falseTailMisses.Take(50))
                sb.AppendLine($"  FALSE {line}");

            sb.AppendLine();
            sb.AppendLine("按 offset 分桶的 miss（offset = 判定时刻 - 物件结束时刻）:");

            foreach (var bucket in misses.GroupBy(m => bucketOf(m.TimeOffset)).OrderBy(g => g.Key))
            {
                sb.AppendLine($"  {bucket.Key}: {bucket.Count()}");
            }

            sb.AppendLine();
            sb.AppendLine("first 80 misses:");

            foreach (var e in misses.OrderBy(m => m.HitObject.StartTime).Take(80))
            {
                string column = e.HitObject is IHasColumn c ? c.Column.ToString() : "-";
                sb.AppendLine($"  {e.HitObject.GetType().Name}@{e.HitObject.StartTime:F0} end={e.HitObject.GetEndTime():F0} col={column} offset={e.TimeOffset:F1}");
            }

            string reportPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, report_file_name);
            File.WriteAllText(reportPath, sb.ToString());
            TestContext.AddTestAttachment(reportPath);
            TestContext.WriteLine(sb.ToString());
        }

        private static string bucketOf(double offset)
        {
            double abs = Math.Abs(offset);

            if (abs <= 50) return "|o|<=50";
            if (abs <= 100) return "50<|o|<=100";
            if (abs <= 190) return "100<|o|<=190";
            if (abs <= 500) return "190<|o|<=500";
            if (abs <= 2000) return "500<|o|<=2000";

            return "|o|>2000";
        }

        private static string describeOffsetBuckets(IEnumerable<HitEvent> events)
        {
            var buckets = events.GroupBy(e => Math.Abs(e.TimeOffset) switch
            {
                < 10 => "<10",
                < 50 => "10-50",
                < 100 => "50-100",
                < 190 => "100-190",
                _ => ">=190",
            });

            return string.Join(", ", buckets.Select(g => $"{g.Key}={g.Count()}"));
        }

        private static string describeCounts(IReadOnlyDictionary<HitResult, int> counts)
            => string.Join(", ", counts.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        private static string describeDelta(IReadOnlyDictionary<HitResult, int> before, IReadOnlyDictionary<HitResult, int> after)
        {
            var keys = before.Keys.Concat(after.Keys).Distinct().OrderBy(k => k);
            var parts = new List<string>();

            foreach (var key in keys)
            {
                before.TryGetValue(key, out int b);
                after.TryGetValue(key, out int a);

                if (a != b)
                    parts.Add($"{key}:{b}->{a}({(a - b):+0;-0})");
            }

            return parts.Count == 0 ? "(identical)" : string.Join(", ", parts);
        }

        private static int countObjects<T>(IBeatmap beatmap, bool exact = false)
        {
            int count = 0;

            foreach (var hitObject in enumerate(beatmap))
            {
                if (exact ? hitObject.GetType() == typeof(T) : hitObject is T)
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

        private static (Dictionary<int, double[]> presses, Dictionary<int, double[]> releases) collectEdges(List<ManiaReplayFrame> frames)
        {
            var presses = new Dictionary<int, List<double>>();
            var releases = new Dictionary<int, List<double>>();
            var held = new Dictionary<int, bool>();

            int baseAction = (int)ManiaAction.Key1;
            var allActions = Enum.GetValues<ManiaAction>();

            foreach (var frame in frames.OrderBy(f => f.Time))
            {
                var active = new HashSet<ManiaAction>(frame.Actions);

                foreach (var action in allActions)
                {
                    int column = (int)action - baseAction;
                    bool isDown = active.Contains(action);
                    bool wasDown = held.GetValueOrDefault(column);

                    if (isDown && !wasDown)
                        getList(presses, column).Add(frame.Time);

                    if (!isDown && wasDown)
                        getList(releases, column).Add(frame.Time);

                    held[column] = isDown;
                }
            }

            return (presses.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
                    releases.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
        }

        private static List<double> getList(Dictionary<int, List<double>> map, int key)
        {
            if (!map.TryGetValue(key, out var list))
                map[key] = list = new List<double>();

            return list;
        }

        private sealed class HarnessScoreDecoder : LegacyScoreDecoder
        {
            public WorkingBeatmap? LastWorkingBeatmap { get; private set; }

            protected override Ruleset GetRuleset(int rulesetId) => new ManiaRuleset();

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                using var stream = resources.GetStream(beatmap_resource);
                IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));

                LastWorkingBeatmap = new TestWorkingBeatmap(decoded);
                return LastWorkingBeatmap;
            }
        }
    }
}
