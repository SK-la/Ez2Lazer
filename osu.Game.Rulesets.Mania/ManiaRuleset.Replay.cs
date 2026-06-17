// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania
{
    public partial class ManiaRuleset
    {
        /// <summary>
        /// 无绘制 replay 判定：返回与真 <see cref="ScoreProcessor"/> 同源的 <see cref="HitEvent"/> 列表。
        /// 成绩统计页经 <see cref="EzMania.Statistics.ManiaScoreHitEventGenerator"/> 调用同一 Session 路径。
        /// </summary>
        public Task<IReadOnlyList<HitEvent>> RunReplayAsync(Score score, IBeatmap playable, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ManiaReplaySession.Run(score, playable, environment, cancellationToken), cancellationToken);
        }
    }
}
