// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局游玩环境快照。由 <see cref="Configuration.Ez2ConfigManager.ResolveEnvironment"/> 在 resolve 时刻从 EzConfig 读出；
    /// 下游仿真/判定须使用已解析实例，禁止再读全局 bindable。
    /// </summary>
    public interface IGameplayEnvironment
    {
        EzEnumHitMode ManiaHitMode { get; }

        EzEnumHealthMode ManiaHealthMode { get; }

        EzEnumJudgePrecedence JudgePrecedence { get; }

        double OffsetPlusMania { get; }

        /// <summary>
        /// 非 Mania（Osu/Taiko/Catch）局内 Drawable 用的输入偏移。
        /// replay Session：ForStored / ignoreOffset 解析为 0；<b>不得</b>再叠进 ResultFor/接盘判窗（帧已是有效时刻，见 REGISTRY §1.7b Offset 原则）。
        /// </summary>
        double OffsetPlusNonMania { get; }

        /// <summary>Osu 判定轨；非 Osu 规则集可忽略（默认 Lazer）。</summary>
        EzEnumOsuJudgementTrack OsuJudgementTrack { get; }

        bool BmsPoorHitResultEnable { get; }

        bool ApplyInputOffsetViaReplayFrameShift { get; }
    }
}
