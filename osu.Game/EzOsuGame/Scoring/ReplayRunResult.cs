// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行结果：包含回填后的 Score、时间线及缓存命中状态。
    /// </summary>
    public sealed class ReplayRunResult
    {
        public Score Score { get; }
        public EzScoreTimeline? Timeline { get; }
        public bool HitCache { get; }
        public bool IsValidReplay { get; }
        public bool WasCancelled { get; }

        public ReplayRunResult(Score score, EzScoreTimeline? timeline = null, bool hitCache = false, bool isValidReplay = true, bool wasCancelled = false)
        {
            Score = score;
            Timeline = timeline;
            HitCache = hitCache;
            IsValidReplay = isValidReplay;
            WasCancelled = wasCancelled;
        }

        public static ReplayRunResult Cancelled() => new ReplayRunResult(null!, wasCancelled: true, isValidReplay: false);
        public static ReplayRunResult InvalidReplay(Score score) => new ReplayRunResult(score, isValidReplay: false);
    }
}
