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
    /// 精度由 Shadow 引擎决定（<c>TODO(EZ-SR-OSL-010)</c>）；见 REPLAY_JUDGE_SHADOW.md。
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

        // TODO(EZ-SR-OSL-010): Generate 不独立算判；精度 = OsuReplayShadowEngine（S2 Slider / S3 Spinner 待补齐）。
        public List<HitEvent> Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            return session_service.RunHitEventsAsync(score, playableBeatmap, cancellationToken: cancellationToken).GetAwaiter().GetResult();
        }
    }
}
