// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania 成绩 <see cref="HitEvent"/> 生成器；委托 <see cref="ManiaReplaySession"/> 作为唯一判定源。
    /// </summary>
    public sealed class ManiaScoreHitEventGenerator : IScoreHitEventGenerator
    {
        public static ManiaScoreHitEventGenerator Instance { get; } = new ManiaScoreHitEventGenerator();
        private static readonly ManiaReplaySessionService replaySession = new ManiaReplaySessionService();

        static ManiaScoreHitEventGenerator()
        {
            // TODO(P3-Rest): 删除 EzScoreReloadBridge / EzScoreTimelineBridge 的 Mania 注册。
            // Panel HitEvents → IEzReplaySession.RunAsync(ForStoredStatistics)
            // Race Timeline → IEzReplaySession.RunTimelineAsync(ForRaceTimeline)
            // 见 replayjudge_总体路线 §P3-Rest
            EzScoreReloadBridge.RegisterImplementation("mania", Instance);
            EzScoreReloadBridge.RegisterImplementation("3", Instance);

            EzScoreTimelineBridge.RegisterManiaTimelineBuilder((score, beatmap, cancellationToken) =>
            {
                var environment = ManiaRuleset.ResolveEnvironment(null, GlobalConfigStore.EzConfig, ReplayRunPurpose.ForRaceTimeline);
                return replaySession.RunTimelineAsync(score, beatmap, environment, cancellationToken).GetAwaiter().GetResult();
            });

            // 注册到全局 Registry，使 Graph/Panel/Race 的 DI 消费者可获得实例
            EzReplaySessionRegistry.Register(replaySession);
        }

        public bool Validate(Score score)
        {
            if (score.ScoreInfo.Ruleset.OnlineID != 3)
                return false;

            var replay = score.Replay;

            if (replay == null || replay.Frames.Count == 0)
                return false;

            return replay.Frames.All(f => f is ManiaReplayFrame);
        }

        public List<HitEvent> Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            var environment = ManiaRuleset.ResolveEnvironment(score.ScoreInfo, GlobalConfigStore.EzConfig, ReplayRunPurpose.ForStoredStatistics);
            return replaySession.RunAsync(score, playableBeatmap, environment, cancellationToken)
                                .GetAwaiter()
                                .GetResult()
                                .ScoreInfo.HitEvents
                                .ToList();
        }
    }
}
