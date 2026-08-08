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
    /// Osu Session 仿真入口；委托 <see cref="OsuReplayShadowEngine"/>（OSL-010 影子判定）。
    /// </summary>
    /// <remarks>
    /// 设计见 <c>docs/REPLAY_JUDGE_SHADOW.md</c> · <c>docs/REPLAY_JUDGE_MERGE-Osu.md</c> · <c>TODO(EZ-SR-OSL-010)</c>。
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

            // environment 保留签名；Osu 无 Mania HitMode，Shadow 路径暂不读取。
            _ = environment;

            OsuReplayShadowEngine.Run(score, beatmap, scoreProcessor, gameplayRate, timelineRecorder, cancellationToken);
        }
    }
}
