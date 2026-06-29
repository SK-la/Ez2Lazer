// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局游玩环境快照（纯数据 record）。
    /// 环境构建由 <c>Ez2ConfigManager.GetGameplayEnvironment()</c> / <c>ResolveForReplay()</c> 负责。
    /// </summary>
    public sealed record GameplayEnvironment : IGameplayEnvironment
    {
        public EzEnumHitMode ManiaHitMode { get; init; }

        public EzEnumHealthMode ManiaHealthMode { get; init; }

        public EzEnumJudgePrecedence JudgePrecedence { get; init; }

        public double OffsetPlusMania { get; init; }

        public bool BmsPoorHitResultEnable { get; init; }
    }
}
