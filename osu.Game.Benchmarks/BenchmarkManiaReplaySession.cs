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
    /// BENCH-KPS：含高密度 jack（80ms × 4K）与三档 JudgePrecedence 场景。
    /// </summary>
    public class BenchmarkManiaReplaySession : BenchmarkTest
    {
        private ManiaReplaySessionService sessionService = null!;
        private IBeatmap beatmap = null!;
        private IBeatmap jackBeatmap = null!;
        private Score score = null!;
        private Score jackScore = null!;

        [Params(EzEnumJudgePrecedence.Earliest, EzEnumJudgePrecedence.Combo, EzEnumJudgePrecedence.Duration)]
        public EzEnumJudgePrecedence BenchPrecedence { get; set; }

        public override void SetUp()
        {
            base.SetUp();

            sessionService = new ManiaReplaySessionService();
            beatmap = createTestBeatmap(noteSpacingMs: 500, noteCount: 200);
            jackBeatmap = createTestBeatmap(noteSpacingMs: 80, noteCount: 400);
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
        /// 高密度 jack：80ms 间距 4K，约 90 KPS 量级 Session 吞吐基线。
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

        private static IBeatmap createTestBeatmap(int noteSpacingMs, int noteCount)
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
