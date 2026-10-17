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
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 临时诊断：Classic osr 头（stable 真值）与「stable 参考状态机」/「现行 StableClassic Session」三方对比。
    /// </summary>
    [TestFixture]
    public class ManiaClassicFullComparisonDiagnosticTest
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(ManiaClassicFullComparisonDiagnosticTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [TestCase("Resources/Testing/Replays/ManiaAudit-Classic-Hanatachi.osr", "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu")]
        [TestCase("Resources/Testing/Replays/ManiaAudit-Classic-PORTRAiT.osr", "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu")]
        public void DumpFullComparison(string osrResource, string beatmapResource)
        {
            var decoder = new HarnessScoreDecoder(beatmapResource);
            Score score;

            using (var stream = resources.GetStream(osrResource))
                score = decoder.Parse(stream);

            ScoreInfo header = score.ScoreInfo.DeepClone();
            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(header.Ruleset, header.Mods);

            var sb = new StringBuilder();
            sb.AppendLine($"osr: {osrResource}");

            var byType = playable.HitObjects.SelectMany(flatten).GroupBy(o => o.GetType().Name).OrderBy(g => g.Key);
            sb.AppendLine($"beatmap objects: {string.Join(", ", byType.Select(g => $"{g.Key}={g.Count()}"))}");
            sb.AppendLine($"beatmap md5={playable.BeatmapInfo.MD5Hash} od={playable.Difficulty.OverallDifficulty}");

            var frames = score.Replay!.Frames.OfType<ManiaReplayFrame>().OrderBy(f => f.Time).ToArray();
            sb.AppendLine($"replay frames: n={frames.Length} first={frames.FirstOrDefault()?.Time} last={frames.LastOrDefault()?.Time}");

            if (frames.Length > 1)
            {
                var intervals = new List<double>(frames.Length - 1);
                int changed = 0;

                for (int i = 1; i < frames.Length; i++)
                {
                    intervals.Add(frames[i].Time - frames[i - 1].Time);

                    if (!frames[i].Actions.SequenceEqual(frames[i - 1].Actions))
                        changed++;
                }

                sb.AppendLine($"frame interval: min={intervals.Min():R} max={intervals.Max():R} mean={intervals.Average():R} ms; distinct-times={frames.Select(f => f.Time).Distinct().Count()}; actions-changed={changed}");
            }

            double od = playable.Difficulty.OverallDifficulty;
            var windows = StableManiaWindows.Create(od, StableManiaScoring.WindowMultiplier(header.Mods), false);
            sb.AppendLine($"stable windows (od={od}, mult={StableManiaScoring.WindowMultiplier(header.Mods)}): 300g={windows.Perfect300g} 300={windows.Great300} 200={windows.Good200} 100={windows.Ok100} 50={windows.Meh50} early={windows.Miss}");

            sb.AppendLine();
            sb.AppendLine("── osr header (解码后原样) ──");
            sb.AppendLine($"  TotalScore={header.TotalScore} MaxCombo={header.MaxCombo} Acc={header.Accuracy:R} ver={header.TotalScoreVersion}");
            dumpStats(sb, "header", header.Statistics);
            sb.AppendLine($"  MaximumStatistics: {string.Join(", ", header.MaximumStatistics.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");

            var environment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.StableClassic, EzEnumHealthMode.Lazer);
            sb.AppendLine($"  env: offsetPlusMania={environment.OffsetPlusMania} frameShift={environment.ApplyInputOffsetViaReplayFrameShift} judgePrecedence={environment.JudgePrecedence}");

            score.ScoreInfo.Statistics = new Dictionary<HitResult, int>();
            score.ScoreInfo.MaximumStatistics = new Dictionary<HitResult, int>();
            score.ScoreInfo.TotalScore = 0;
            score.ScoreInfo.MaxCombo = 0;
            score.ScoreInfo.Accuracy = 0;
            score.ScoreInfo.Rank = ScoreRank.D;
            score.ScoreInfo.JudgeMode = (int)EzEnumJudgeMode.StableClassic;
            score.ScoreInfo.IsLegacyScore = true;

            ManiaReplaySession.Run(score, playable, environment);

            ScoreInfo session = score.ScoreInfo;
            sb.AppendLine();
            sb.AppendLine("── 现行 StableClassic Session ──");
            sb.AppendLine($"  TotalScore={session.TotalScore} MaxCombo={session.MaxCombo} Acc={session.Accuracy:R}");
            dumpStats(sb, "session", session.Statistics);
            dumpByComponent(sb, session.HitEvents);

            // stable 参考状态机。
            var reference = new StableReferenceSimulation(playable, windows);
            reference.Run(frames, environment.OffsetPlusMania);

            sb.AppendLine();
            sb.AppendLine("── stable 参考状态机（classic 列状态机 + Holding） ──");
            dumpStats(sb, "reference(all)", reference.AllStatistics);
            dumpStats(sb, "reference(tap)", reference.TapStatistics);
            dumpStats(sb, "reference(LN)", reference.LongStatistics);
            sb.AppendLine($"  reference combo: max={reference.MaxCombo} (hits={reference.MaxComboAtHits} increments={reference.MaxComboAtIncrements}) totalIncrements={reference.HoldIncrements} breaks={reference.Breaks}");
            sb.AppendLine($"  reference classic score double={reference.TotalScoreDouble}");

            var target = new Dictionary<HitResult, int>(header.Statistics);

            sb.AppendLine();
            sb.AppendLine("── header vs reference(all) ──");

            foreach (var key in target.Keys.Union(reference.AllStatistics.Keys).OrderByDescending(k => k))
            {
                int delta = reference.AllStatistics.GetValueOrDefault(key) - target.GetValueOrDefault(key);
                sb.AppendLine($"  {key,-10}: header={target.GetValueOrDefault(key),5} reference={reference.AllStatistics.GetValueOrDefault(key),5} delta={delta:+#;-#;0}");
            }

            sb.AppendLine();
            sb.AppendLine("── 参考判定与 Session 判定不一致的长条（前 80 条） ──");

            int mismatches = 0;
            int shown = 0;

            foreach (var ln in reference.Longs)
            {
                var tailEvent = session.HitEvents.LastOrDefault(e => ReferenceEquals(e.HitObject, ln.Hold?.Tail));
                var sessionResult = tailEvent.HitObject != null ? tailEvent.Result : HitResult.IgnoreMiss;

                if (sessionResult == ln.Result)
                    continue;

                mismatches++;

                if (shown++ >= 80)
                    continue;

                sb.AppendLine($"  col={ln.Column} start={ln.Start} end={ln.End} len={ln.End - ln.Start} | " +
                              $"press={(ln.TimePress == 0 ? "-" : (ln.TimePress - ln.Start).ToString())} release={(ln.TimeRelease == 0 ? "-" : (ln.TimeRelease - ln.End).ToString())} | " +
                              $"presses={ln.Presses} releases={ln.Releases} break={ln.HasHoldBreak} startHit={ln.StartHit} | " +
                              $"ref={ln.Result} session={sessionResult}");
            }

            sb.AppendLine($"  (共 {mismatches} 条不一致)");

            // 参考与 header 在「单键」上的分布差异（验证参考实现是否复刻 stable 单键）。
            sb.AppendLine();
            sb.AppendLine("── reference(tap) vs header−reference(LN) ──");

            var headerMinusLn = new Dictionary<HitResult, int>(header.Statistics);

            foreach (var (result, count) in reference.LongStatistics)
                headerMinusLn[result] = headerMinusLn.GetValueOrDefault(result) - count;

            var taps = reference.TapStatistics;

            foreach (var key in headerMinusLn.Keys.Union(taps.Keys).OrderByDescending(k => k))
                sb.AppendLine($"  {key,-10}: header−refLN={headerMinusLn.GetValueOrDefault(key),5} refTap={taps.GetValueOrDefault(key),5} delta={taps.GetValueOrDefault(key) - headerMinusLn.GetValueOrDefault(key):+#;-#;0}");

            TestContext.WriteLine(sb.ToString());

            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"classic_diag_{Path.GetFileNameWithoutExtension(osrResource)}.txt");
            File.WriteAllText(path, sb.ToString());
            TestContext.AddTestAttachment(path);
        }

        private static void dumpStats(StringBuilder sb, string label, IReadOnlyDictionary<HitResult, int> stats)
            => sb.AppendLine($"  {label} stats: {string.Join(", ", stats.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))} (judged={stats.Where(kv => kv.Key.AffectsAccuracy()).Sum(kv => kv.Value)})");

        private static void dumpByComponent(StringBuilder sb, IReadOnlyList<HitEvent> hitEvents)
        {
            foreach (var group in hitEvents.GroupBy(e => componentOf(e.HitObject)).OrderBy(g => g.Key))
            {
                var dist = group.GroupBy(e => e.Result).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}");
                sb.AppendLine($"  {group.Key} (n={group.Count()}): {string.Join(", ", dist)}");
            }
        }

        private static string componentOf(HitObject? o) => o switch
        {
            HeadNote => "HeadNote",
            TailNote => "TailNote",
            HoldNoteBody => "HoldNoteBody",
            HoldNoteTick => "HoldNoteTick",
            HoldNote => "HoldNote(parent)",
            Note => "Note(tap)",
            _ => o?.GetType().Name ?? "?",
        };

        private static IEnumerable<HitObject> flatten(HitObject o)
        {
            yield return o;

            foreach (var nested in o.NestedHitObjects)
            {
                foreach (var deeper in flatten(nested))
                    yield return deeper;
            }
        }

        /// <summary>
        /// 按列跑物件的参考状态机：单键与长条共用列路由、到点结算与持有增量。
        /// </summary>
        private sealed class StableReferenceSimulation
        {
            private readonly List<Obj> objects = new List<Obj>();
            private readonly StableManiaWindows windows;

            public StableReferenceSimulation(IBeatmap beatmap, StableManiaWindows windows)
            {
                this.windows = windows;

                foreach (var hitObject in beatmap.HitObjects)
                {
                    if (hitObject is HoldNote hold)
                    {
                        objects.Add(new Obj
                        {
                            Source = hold,
                            Hold = hold,
                            IsLong = true,
                            Column = hold.Column,
                            Start = (int)hold.Head.StartTime,
                            End = (int)hold.Tail.StartTime,
                        });
                    }
                    else if (hitObject is Note note)
                    {
                        objects.Add(new Obj
                        {
                            Source = note,
                            Note = note,
                            IsLong = false,
                            Column = note.Column,
                            Start = (int)note.StartTime,
                            End = (int)note.StartTime,
                        });
                    }
                }

                foreach (var group in objects.GroupBy(o => o.Column))
                {
                    var ordered = group.OrderBy(o => o.Start).ToList();

                    for (int i = 0; i < ordered.Count; i++)
                        ordered[i].Order = i;
                }
            }

            public IReadOnlyList<Obj> Longs => objects.Where(o => o.IsLong).ToList();
            public int MaxCombo { get; private set; }
            public int HoldIncrements { get; private set; }
            public int Breaks { get; private set; }
            public double TotalScoreDouble { get; private set; }

            public Dictionary<HitResult, int> AllStatistics => statistics(objects);
            public Dictionary<HitResult, int> TapStatistics => statistics(objects.Where(o => !o.IsLong));
            public Dictionary<HitResult, int> LongStatistics => statistics(objects.Where(o => o.IsLong));

            private static Dictionary<HitResult, int> statistics(IEnumerable<Obj> source)
            {
                var stats = new Dictionary<HitResult, int>();

                foreach (var o in source)
                    stats[o.Result] = stats.GetValueOrDefault(o.Result) + 1;

                return stats;
            }

            public void Run(IReadOnlyList<ManiaReplayFrame> frames, double frameTimeOffset)
            {
                var clock = new List<(int time, List<ManiaAction> actions)>(frames.Count);

                foreach (var frame in frames)
                    clock.Add(((int)Math.Round(frame.Time + frameTimeOffset), frame.Actions.ToList()));

                // stable 的列状态机以帧为外层：同一帧内逐列推进，
                // 共享同一个 Combo/断连计数。列循环不能外提，否则断连在列间重排，MaxCombo 会偏小。
                int[] columns = objects.Select(o => o.Column).Distinct().OrderBy(c => c).ToArray();
                var columnObjects = columns.Select(c => objects.Where(o => o.Column == c).OrderBy(o => o.Order).ToList()).ToArray();
                int[] hi = new int[columns.Length];

                for (int i = 0; i < hi.Length; i++)
                    hi[i] = -1;

                var previousActions = new List<ManiaAction>();

                foreach (var (time, actions) in clock)
                {
                    for (int i = 0; i < columns.Length; i++)
                        stepColumn(columnObjects[i], time, actions.Contains((ManiaAction)columns[i]), previousActions.Contains((ManiaAction)columns[i]), ref hi[i]);

                    previousActions = actions;
                }
            }

            private void stepColumn(List<Obj> columnObjects, int time, bool keyDown, bool keyStateLast, ref int hi)
            {
                // stable 的 hitObjectsMinimal 上界：StartTime <= now + 300。
                while (hi + 1 < columnObjects.Count && columnObjects[hi + 1].Start <= time + 300)
                    hi++;

                bool routed = false;

                for (int i = 0; i <= hi; i++)
                {
                    var o = columnObjects[i];

                    if (o.IsHit)
                    {
                        if (o.Start > time)
                            routed = true;

                        continue;
                    }

                    if (routed || time < o.Start - windows.Miss)
                        continue;

                    // EndTime + HitWindow100 到点结算。
                    if (time >= o.End + (int)windows.Ok100)
                    {
                        evaluate(o, time);
                        continue;
                    }

                    if (keyDown && !keyStateLast)
                    {
                        if (o.IsLong)
                        {
                            if (!o.Pressed)
                            {
                                o.Presses++;
                                o.Pressed = true;
                                o.TimePress = time;
                                o.StartHit = true;
                                o.HoldTickBaseline = time;
                                routed = true;
                            }
                        }
                        else
                        {
                            o.Presses++;
                            o.Pressed = true;
                            o.TimePress = time;
                            routed = true;
                            evaluate(o, time);
                        }
                    }
                    else if (keyDown && keyStateLast)
                    {
                        if (o.IsLong && o.Pressed)
                            holding(o, time);
                    }
                    else
                    {
                        if (o.IsLong && o.Pressed)
                        {
                            o.Releases++;
                            o.Pressed = false;
                            o.TimeRelease = time;
                            evaluate(o, time);
                        }
                        else if (o.IsLong && !o.IsMissing)
                            evaluate(o, time);
                    }
                }
            }

            private void holding(Obj o, int now)
            {
                if (o.HoldTickBaseline == -1 || now > o.End || now < o.Start)
                    return;

                if (now - o.HoldTickBaseline >= 100)
                {
                    o.HoldTickBaseline += 100;
                    HoldIncrements++;
                    applyValue(7);
                }
            }

            /// <summary>连击处理：正值 +1（7 为持有增量），其余清零。</summary>
            private void applyValue(int value)
            {
                if (value > 0)
                {
                    Combo++;
                    MaxCombo = Math.Max(MaxCombo, Combo);
                    MaxComboAtHits = HitsInRun;
                    MaxComboAtIncrements = IncrementsInRun;

                    if (value == 7)
                        IncrementsInRun++;
                    else
                        HitsInRun++;
                }
                else
                {
                    Combo = 0;
                    HitsInRun = 0;
                    IncrementsInRun = 0;
                    Breaks++;
                }
            }

            public int Combo { get; private set; }
            public int MaxComboAtHits { get; private set; }
            public int MaxComboAtIncrements { get; private set; }
            public int HitsInRun { get; private set; }
            public int IncrementsInRun { get; private set; }

            private void evaluate(Obj o, int now)
            {
                if (o.IsHit)
                    return;

                if (o.IsLong)
                    evaluateLong(o, now);
                else
                    evaluateTap(o);
            }

            private void evaluateTap(Obj o)
            {
                if (o.IsHit)
                    return;

                o.IsHit = true;
                o.Judgement = StableManiaJudgement.Miss;

                if (o.Pressed)
                {
                    int error = Math.Abs(o.TimePress - o.Start);

                    if (error <= windows.Perfect300g)
                        o.Judgement = StableManiaJudgement.Perfect300g;
                    else if (error <= windows.Great300)
                        o.Judgement = StableManiaJudgement.Great300;
                    else if (error <= windows.Good200)
                        o.Judgement = StableManiaJudgement.Good200;
                    else if (error <= windows.Ok100)
                        o.Judgement = StableManiaJudgement.Ok100;
                    else if (error <= windows.Meh50)
                        o.Judgement = StableManiaJudgement.Meh50;
                }

                settle(o);
            }

            private void evaluateLong(Obj o, int now)
            {
                if (o.IsHit)
                    return;

                if (now < o.Start + (int)windows.Meh50 && o.TimePress == 0)
                    return;

                if (now < o.End - (int)windows.Meh50)
                {
                    if (o.Pressed)
                        return;

                    o.HasHoldBreak = true;
                    applyValue(-1048576);
                    return;
                }

                if (now < o.End + (int)windows.Meh50 && o.Pressed)
                    return;

                o.IsHit = true;

                int pressBaseline = o.TimePress < o.Start - (int)windows.Meh50
                    ? o.End - 1
                    : (o.TimePress >= o.Start ? o.TimePress : o.Start + (o.Start - o.TimePress));
                int releaseBaseline = o.Pressed
                    ? now
                    : (o.TimeRelease <= o.End ? o.TimeRelease : o.End - (o.TimeRelease - o.End));

                if (releaseBaseline < o.End - (int)windows.Meh50)
                    o.Judgement = StableManiaJudgement.Miss;
                else
                {
                    int pressError = Math.Abs(pressBaseline - o.Start);
                    int combinedError = pressError + Math.Abs(releaseBaseline - o.End);

                    if (pressError <= windows.Perfect300g * 1.2 && combinedError <= windows.Perfect300g * 2.4)
                        o.Judgement = StableManiaJudgement.Perfect300g;
                    else if (pressError <= windows.Great300 * 1.1 && combinedError <= windows.Great300 * 2.2)
                        o.Judgement = StableManiaJudgement.Great300;
                    else if (pressError <= windows.Good200 && combinedError <= windows.Good200 * 2)
                        o.Judgement = StableManiaJudgement.Good200;
                    else if (pressError <= windows.Ok100 && combinedError <= windows.Ok100 * 2)
                        o.Judgement = StableManiaJudgement.Ok100;
                    else
                        o.Judgement = StableManiaJudgement.Meh50;
                }

                if (o.HasHoldBreak && o.Judgement > StableManiaJudgement.Good200)
                    o.Judgement = StableManiaJudgement.Good200;

                settle(o);
            }

            private void settle(Obj o)
            {
                o.Result = StableClassicJudgeModeJudgement.MapTo(o.Judgement);

                applyValue(o.Judgement switch
                {
                    StableManiaJudgement.Perfect300g => 268435456,
                    StableManiaJudgement.Great300 => 134217728,
                    StableManiaJudgement.Good200 => 67108864,
                    StableManiaJudgement.Ok100 => 33554432,
                    StableManiaJudgement.Meh50 => 16777216,
                    _ => -65536,
                });
            }

            internal sealed class Obj
            {
                public HitObject Source = null!;
                public HoldNote? Hold;
                public Note? Note;
                public bool IsLong;
                public int Column;
                public int Start;
                public int End;
                public int Order;
                public int TimePress;
                public int TimeRelease;
                public int Presses;
                public int Releases;
                public int HoldTickBaseline = -1;
                public bool Pressed;
                public bool StartHit;
                public bool IsMissing;
                public bool IsHit;
                public bool HasHoldBreak;
                public StableManiaJudgement Judgement = StableManiaJudgement.Miss;
                public HitResult Result = HitResult.IgnoreMiss;
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
