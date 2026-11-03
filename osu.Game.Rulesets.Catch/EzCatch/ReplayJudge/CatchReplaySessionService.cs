// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Catch.EzCatch.ReplayJudge
{
    public sealed class CatchReplaySessionService : EzReplaySession
    {
        protected override (Score Score, EzScoreTimeline Timeline) RunWithTimeline(
            Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
            => CatchReplaySession.RunWithTimeline(score, beatmap, environment, cancellationToken);
    }
}
