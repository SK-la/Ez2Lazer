// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.Statistics
{
    /// <summary>
    /// Osu 成绩 <see cref="HitEvent"/> 生成器；委托 <see cref="OsuReplaySessionService"/> 作为唯一判定源。
    /// </summary>
    /// <remarks>
    /// 精度随 Session 判定路径（当前为 OSL-010 Shadow 桥；毕业见 TODO(EZ-SR-OSL-011)）。
    /// </remarks>
    public sealed class OsuScoreHitEventGenerator
    {
        public static OsuScoreHitEventGenerator Instance { get; } = new OsuScoreHitEventGenerator();

        private static readonly OsuReplaySessionService session_service = new OsuReplaySessionService();

        public bool Validate(Score score)
        {
            if (score.ScoreInfo.Ruleset.OnlineID != 0)
                return false;

            var replay = score.Replay;

            if (replay == null || replay.Frames.Count == 0)
                return false;

            return replay.Frames.OfType<OsuReplayFrame>().Any();
        }

        public List<HitEvent> Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            return session_service.RunHitEventsAsync(score, playableBeatmap, cancellationToken: cancellationToken).GetAwaiter().GetResult();
        }
    }
}
