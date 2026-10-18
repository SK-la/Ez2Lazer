// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Benchmarks
{
    /// <summary>
    /// P2-B: ManiaReplaySessionService 性能基准测试
    /// BENCH-KPS：按标定的每列 10 / 15 / 20 KPS 生成 4K 谱面（全局间隔 = 1000 / (每列 KPS × 列数)），
    /// 另保留 80 ms 间距（每列约 3.1 KPS）作为低密度参考组。三档 JudgePrecedence 都跑。
    /// </summary>
    public class BenchmarkManiaReplaySession : BenchmarkTest
    {
        private const int column_count = 4;
        private const int note_count = 400;

        private ManiaReplaySessionService sessionService = null!;
        private IBeatmap beatmap = null!;
        private IBeatmap jackBeatmap = null!;
        private Score score = null!;
        private Score jackScore = null!;

        [Params(10, 15, 20)]
        public int PerColumnKps { get; set; }

        [Params(EzEnumJudgePrecedence.Earliest, EzEnumJudgePrecedence.Combo, EzEnumJudgePrecedence.Duration)]
        public EzEnumJudgePrecedence BenchPrecedence { get; set; }

        public override void SetUp()
        {
            base.SetUp();

            // 每列 KPS 是标定入口；全局 note 间隔由列数派生。
            double standardSpacingMs = 1000.0 / (PerColumnKps * column_count);

            sessionService = new ManiaReplaySessionService();
            beatmap = createTestBeatmap(noteSpacingMs: standardSpacingMs, noteCount: note_count);
            jackBeatmap = createTestBeatmap(noteSpacingMs: 80, noteCount: note_count);
            score = createTestScore(beatmap);
            jackScore = createTestScore(jackBeatmap);

            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaJudgeMode, environment.JudgeMode);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, environment.HealthMode);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, BenchPrecedence);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, environment.BmsPoorHitResultEnable);
        }

        [Benchmark]
        public async Task<Score> BenchmarkRunAsync()
        {
            return await sessionService.RunAsync(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        [Benchmark]
        public async Task<EzScoreTimeline> BenchmarkRunTimelineAsync()
        {
            return await sessionService.RunTimelineAsync(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        /// <summary>
        /// 低密度参考：80 ms 全局间距 × 4K，即每列约 3.1 KPS，约 12.5 KPS 合计。
        /// 低于标定下限（每列 10 KPS），只用于对照。
        /// </summary>
        [Benchmark]
        public async Task<Score> BenchmarkJackRunAsync()
        {
            return await sessionService.RunAsync(
                jackScore.DeepClone(),
                jackBeatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        /// <summary>
        /// HitEvents 专用出口：相对完整 RunAsync 的延迟基线（目标参考 p50/p95 &lt;10ms，真实谱视密度而定）。
        /// </summary>
        [Benchmark]
        public async Task<List<HitEvent>> BenchmarkRunHitEventsAsync()
        {
            return await sessionService.RunHitEventsAsync(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        [Benchmark]
        public async Task<List<HitEvent>> BenchmarkJackRunHitEventsAsync()
        {
            return await sessionService.RunHitEventsAsync(
                jackScore.DeepClone(),
                jackBeatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        private static IBeatmap createTestBeatmap(double noteSpacingMs, int noteCount)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = ruleset.RulesetInfo,
                    Difficulty = new BeatmapDifficulty
                    {
                        DrainRate = 5,
                        OverallDifficulty = 8,
                    }
                }
            };

            for (int i = 0; i < noteCount; i++)
            {
                beatmap.HitObjects.Add(new Note
                {
                    StartTime = i * noteSpacingMs,
                    Column = i % 4
                });
            }

            return beatmap;
        }

        private static Score createTestScore(IBeatmap beatmap)
        {
            var scoreInfo = new ScoreInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
                Accuracy = 0.95,
                TotalScore = 1000000,
            };

            var replay = new Replay();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is Note note)
                    replay.Frames.Add(new ManiaReplayFrame(note.StartTime));
            }

            return new Score { ScoreInfo = scoreInfo, Replay = replay };
        }
    }
}
