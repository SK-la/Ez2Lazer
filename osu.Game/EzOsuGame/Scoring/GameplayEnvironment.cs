// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局游玩环境快照（纯数据 record）。
    /// 环境构建由 <c>Ez2ConfigManager.ResolveEnvironment</c>（Purpose + 可选 score；ForLive Session 可传 ignoreOffset）负责。
    /// </summary>
    public sealed record GameplayEnvironment : IGameplayEnvironment
    {
        public EzEnumJudgeMode JudgeMode { get; init; }

        public EzEnumHealthMode HealthMode { get; init; }

        public EzEnumJudgePrecedence JudgePrecedence { get; init; }

        public double OffsetPlusMania { get; init; }

        /// <inheritdoc cref="IGameplayEnvironment.OffsetPlusNonMania"/>
        public double OffsetPlusNonMania { get; init; }

        public bool BmsPoorHitResultEnable { get; init; }

        public bool ApplyInputOffsetViaReplayFrameShift { get; init; }

        /// <summary>
        /// 数据迁移用的规范化环境：只钉判定档，其余字段全部取固定值，不继承任何用户设置。
        /// </summary>
        /// <remarks>
        /// 只钉 <see cref="JudgeMode"/> 不够：<see cref="JudgePrecedence"/> 与 <see cref="BmsPoorHitResultEnable"/>
        /// 直接影响 mania 判定选取，继承实时配置会让同一份 replay 在不同机器上迁出不同结果。
        /// </remarks>
        internal static GameplayEnvironment ForMigration(EzEnumJudgeMode judgeMode) => new GameplayEnvironment
        {
            JudgeMode = judgeMode,
            HealthMode = EzEnumHealthMode.Lazer,
            JudgePrecedence = EzEnumJudgePrecedence.Combo,
            OffsetPlusMania = 0,
            OffsetPlusNonMania = 0,
            BmsPoorHitResultEnable = false,
            ApplyInputOffsetViaReplayFrameShift = false,
        };
    }
}
