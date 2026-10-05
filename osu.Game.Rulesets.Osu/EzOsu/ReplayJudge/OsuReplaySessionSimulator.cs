// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Shadow;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge
{
    /// <summary>
    /// Osu Session 仿真入口；委托 <see cref="OsuReplayShadowEngine"/>（OSL-010 Shadow 桥）。
    /// </summary>
    /// <remarks>
    /// 见 docs/REPLAY_JUDGE_SHADOW.md · MERGE-Osu。env 消费见 TODO(EZ-SR-OSL-012)；判定毕业见 TODO(EZ-SR-OSL-011)。
    /// </remarks>
    internal static class OsuReplaySessionSimulator
    {
        internal static void Simulate(
            Score score,
            IBeatmap beatmap,
            IGameplayEnvironment environment,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            OsuReplayTimelineRecorder? timelineRecorder,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score.Replay);

            // TODO(EZ-SR-OSL-012): 已消费 OffsetPlusNonMania；轨字段（ClassicNative）见 OSL-013。
            OsuReplayShadowEngine.Run(score, beatmap, scoreProcessor, gameplayRate, environment, timelineRecorder, cancellationToken);
        }
    }
}
