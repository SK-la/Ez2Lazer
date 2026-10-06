// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge
{
    /// <summary>
    /// Osu Session 仿真入口；委托 <see cref="OsuReplaySessionEngine"/>（OSL-011 Mapping）。
    /// </summary>
    /// <remarks>
    /// 见 docs/scoring/REPLAY_JUDGE_SHADOW.md · MERGE-Osu。
    /// OffsetPlusNonMania：REGISTRY §1.7b — Session 不叠进判窗；轨字段见 OSL-013。
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

            OsuReplaySessionEngine.Run(score, beatmap, scoreProcessor, gameplayRate, environment, timelineRecorder, cancellationToken);
        }
    }
}
