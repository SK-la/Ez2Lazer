// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
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

        static ManiaScoreHitEventGenerator()
        {
            EzScoreReloadBridge.RegisterImplementation("mania", Instance);
            EzScoreReloadBridge.RegisterImplementation("3", Instance);

            // Race Timeline: direct synchronous call (not via ManiaReplaySessionService)
            // to avoid any async/caching issues for ghost score HUDs.
            EzScoreTimelineBridge.RegisterManiaTimelineBuilder((score, beatmap, cancellationToken) =>
            {
                var environment = ManiaRuleset.ResolveEnvironment(null, ReplayRunPurpose.ForLive);
                return ManiaReplaySession.RunTimeline(score, beatmap, environment, cancellationToken);
            });
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
            var environment = ManiaRuleset.ResolveEnvironment(score.ScoreInfo, ReplayRunPurpose.ForStored);
            return ManiaReplaySession.Run(score, playableBeatmap, environment, cancellationToken).ScoreInfo.HitEvents.ToList();
        }
    }
}
