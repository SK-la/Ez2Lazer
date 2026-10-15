// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
    [TestFixture]
    public class DumpMidorigoTest
    {
        private const string beatmap_resource = "Resources/Testing/Beatmaps/OsuAudit-Midorigo.osu";

        private static readonly DllResourceStore resources = new DllResourceStore(typeof(DumpMidorigoTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [TestCase("Resources/Testing/Replays/OsuAudit-Lazer-Midorigo.osr")]
        [TestCase("Resources/Testing/Replays/OsuAudit-Stable-Midorigo.osr")]
        public void Dump(string osrResource)
        {
            var decoder = new HarnessScoreDecoder();
            Score score;

            using (var stream = resources.GetStream(osrResource))
                score = decoder.Parse(stream);

            ScoreInfo info = score.ScoreInfo;

            var sb = new StringBuilder();
            sb.AppendLine($"=== {osrResource} ===");
            sb.AppendLine($"beatmapMd5={info.BeatmapInfo?.MD5Hash}");
            sb.AppendLine($"total={info.TotalScore} legacyTotal={info.LegacyTotalScore} combo={info.MaxCombo} acc={info.Accuracy:R} rank={info.Rank}");
            sb.AppendLine($"isLegacy={info.IsLegacyScore} scoreVersion={info.TotalScoreVersion}");
            sb.AppendLine($"mods=[{string.Join(",", info.Mods.Select(m => m.Acronym))}]");
            sb.AppendLine($"frames={score.Replay?.Frames.Count}");
            sb.AppendLine($"stats: {formatStats(info.Statistics)}");

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(info.Ruleset, info.Mods);

            foreach (var mode in new[] { EzEnumJudgeMode.Lazer, EzEnumJudgeMode.StableClassic })
            {
                var env = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, info) with { JudgeMode = mode };
                Score session = OsuReplaySession.Run(score.DeepClone(), playable, env);

                sb.AppendLine($"-- session [{mode}] total={session.ScoreInfo.TotalScore} combo={session.ScoreInfo.MaxCombo} "
                              + $"acc={session.ScoreInfo.Accuracy:R} rank={session.ScoreInfo.Rank} legacyTotal={session.ScoreInfo.LegacyTotalScore}");
                sb.AppendLine($"   stats: {formatStats(session.ScoreInfo.Statistics)}");
                sb.AppendLine($"   byType: {formatByType(session)}");
                sb.AppendLine($"   meh: {formatEvents(session, HitResult.Meh)}");
                sb.AppendLine($"   miss: {formatEvents(session, HitResult.Miss)}");
                sb.AppendLine($"   ok: {formatEvents(session, HitResult.Ok)}");
                sb.AppendLine($"   unresolved: {formatUnresolved(session, playable)}");
            }

            TestContext.WriteLine(sb.ToString());
            System.IO.File.AppendAllText(System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "midorigo_dump.txt"), sb.ToString(), Encoding.UTF8);
            Assert.Pass(sb.ToString());
        }

        private static string formatStats(IReadOnlyDictionary<HitResult, int> stats)
            => string.Join(", ", stats.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        private static string formatByType(Score session)
            => string.Join("; ", session.ScoreInfo.HitEvents
                                      .GroupBy(e => e.HitObject.GetType().Name)
                                      .OrderBy(g => g.Key)
                                      .Select(g => $"{g.Key}[{string.Join(",", g.GroupBy(e => e.Result).OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Count()}"))}]"));

        private static string formatEvents(Score session, HitResult result)
        {
            var ordered = session.ScoreInfo.HitEvents
                                 .Where(e => e.HitObject is Objects.HitCircle
                                             || e.HitObject is Objects.Slider
                                             || e.HitObject is Objects.Spinner)
                                 .ToList();

            var list = new List<string>();

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Result != result)
                    continue;

                string type = ordered[i].HitObject.GetType().Name.Replace("SliderHeadCircle", "SHead");
                list.Add($"{i}:{type}@{ordered[i].TimeOffset:+0.0;-0.0}");
            }

            return string.Join(" ", list);
        }

        private static string formatUnresolved(Score session, IBeatmap playable)
        {
            var judged = new HashSet<osu.Game.Rulesets.Objects.HitObject>(session.ScoreInfo.HitEvents.Select(e => e.HitObject));            var list = new List<string>();
            int index = 0;

            foreach (var top in playable.HitObjects)
            {
                var missing = new List<string>();

                if (top.Judgement.MaxResult != HitResult.IgnoreHit && !judged.Contains(top))
                    missing.Add("self");

                foreach (var nested in top.NestedHitObjects)
                {
                    if (nested.Judgement.MaxResult == HitResult.IgnoreHit)
                        continue;

                    if (!judged.Contains(nested))
                        missing.Add($"{nested.GetType().Name}@{nested.StartTime:F0}");
                }

                if (missing.Count > 0)
                    list.Add($"{index}:{top.GetType().Name}@{top.StartTime:F0}[{string.Join(",", missing)}]");

                index++;
            }

            return $"{list.Count} -> {string.Join(" ", list.Take(20))}";
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
