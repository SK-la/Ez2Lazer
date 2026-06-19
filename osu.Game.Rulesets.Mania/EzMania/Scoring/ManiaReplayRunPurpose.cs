// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Scoring
{
    /// <summary>
    /// Mania replay Session 环境解析用途。
    /// </summary>
    public enum ManiaReplayRunPurpose
    {
        /// <summary>StatisticsPanel HitEvents：嵌入 HitMode/HealthMode + 全局 JudgePrecedence/Offset/KPoor。</summary>
        ForStoredStatistics,

        /// <summary>Graph Now / 拓展 what-if：全 FromLive。</summary>
        ForLiveAnalysis,

        /// <summary>角逐 ghost timeline：全 FromLive。</summary>
        ForRaceTimeline,
    }
}
