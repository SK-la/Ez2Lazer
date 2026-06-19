// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Rulesets.Mania.EzMania.Scoring
{
    /// <summary>
    /// Mania replay 判定环境快照；仅由 <see cref="ManiaRuleset.ResolveEnvironment"/> 构造。
    /// </summary>
    public sealed record ManiaGameplayEnvironment : IManiaGameplayEnvironment
    {
        public required EzEnumHitMode ManiaHitMode { get; init; }

        public required EzEnumHealthMode ManiaHealthMode { get; init; }

        public required EzEnumJudgePrecedence JudgePrecedence { get; init; }

        public required double OffsetPlusMania { get; init; }

        public required bool BmsPoorHitResultEnable { get; init; }
    }
}
