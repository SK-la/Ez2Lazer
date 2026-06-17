// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局游玩环境快照。回放分析必须使用该上下文，而非读取全局 <see cref="GlobalConfigStore.EzConfig"/>。
    /// </summary>
    public interface IGameplayEnvironment
    {
        EzEnumHitMode ManiaHitMode { get; }

        EzEnumHealthMode ManiaHealthMode { get; }

        EzEnumJudgePrecedence JudgePrecedence { get; }

        double OffsetPlusMania { get; }
    }
}
