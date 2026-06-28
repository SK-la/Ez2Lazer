// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行用途：区分统计页、实时分析、角逐时间线等场景。
    /// </summary>
    public enum ReplayRunPurpose
    {
        /// <summary>StatisticsPanel HitEvents 回填：嵌入 HitMode/HealthMode + 全局 JudgePrecedence/Offset/KPoor。</summary>
        ForStored,

        /// <summary>Graph Now / 拓展 what-if：全 FromLive，offset=0。</summary>
        ForLive,

        /// <summary>角逐 ghost timeline：全 FromLive。</summary>
        ForRaceTimeline,
    }
}
