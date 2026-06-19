// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Scoring
{
    /// <summary>
    /// Mania 专用游玩环境；Session / Drawable 判定须完整携带，禁止 ReplayJudge 路径读全局 KPoor。
    /// </summary>
    public interface IManiaGameplayEnvironment : IGameplayEnvironment
    {
        bool BmsPoorHitResultEnable { get; }
    }
}
