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
    /// 见 docs/scoring/session.md · docs/scoring/mania.md。
    /// OffsetPlusNonMania：REGISTRY §1.7b — Session 不叠进判窗；轨字段见 OSL-013。
    /// </remarks>
    internal static class OsuReplaySessionSimulator
    {
        // TODO(Ez live stable 影子)：与 mania 侧同步把 Session 改成可推进引擎（改造面与验收口径见
        // ManiaReplaySessionSimulator.Simulate 头部的 TODO）。
        //
        // std 侧额外约束：stable 语义需要独立仿真谱面副本（EzScoreRacePlayableResolver.GetSessionReady），
        // 且 OsuStableClassic.BindSemantics 只能作用于副本，不能碰 live 实例；改造后
        // OsuScoreProcessor 的 IStableScoreSource 转发目标换成引擎输出，StableAvailable 恒为 true。
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
