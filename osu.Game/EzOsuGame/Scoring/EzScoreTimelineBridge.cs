// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 规则集特定的时间线构建入口。Mania 由 Session 一遍判定直接采快照，不经 HitEvents 二次喂 SP。
    /// </summary>
    // TODO(EZ-SR-TL-005): 泛化为多规则集 timeline 注册（OsuReplaySession），勿仅 Mania 语义。
    public static class EzScoreTimelineBridge
    {
        private static Func<Score, IBeatmap, CancellationToken, EzScoreTimeline?>? maniaTimelineBuilder;

        public static void RegisterManiaTimelineBuilder(Func<Score, IBeatmap, CancellationToken, EzScoreTimeline?> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            maniaTimelineBuilder = builder;
        }

        public static EzScoreTimeline? TryBuildManiaTimeline(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
            => maniaTimelineBuilder?.Invoke(score, playableBeatmap, cancellationToken);
    }
}
