// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 规则集特定的分数命中事件生成器接口。
    /// 每个规则集实现此接口来提供其特定的帧数据计算和判定逻辑。
    /// </summary>
    public interface IScoreHitEventGenerator
    {
        /// <summary>
        /// 验证该回放是否对此规则集有效。
        /// 用于快速检查回放格式、帧数据完整性等。
        /// </summary>
        /// <param name="score">要验证的分数</param>
        /// <returns>如果回放有效则返回 true，否则返回 false</returns>
        bool Validate(Score score);

        /// <summary>
        /// 为分数生成命中事件列表。
        /// 通过分析回放帧和谱面对象计算命中判定。
        /// </summary>
        /// <param name="score">要处理的分数</param>
        /// <param name="playable">与分数关联的可玩谱面</param>
        /// <param name="cancellationToken">用于停止生成的取消令牌</param>
        /// <returns>生成的命中事件列表，若无法生成则返回 null</returns>
        List<HitEvent>? Generate(Score score, IBeatmap playable, CancellationToken cancellationToken = default);
    }
}

