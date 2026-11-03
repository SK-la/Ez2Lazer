// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 规则集自管的 stable 结果源，供 HUD 在不引用规则集程序集的前提下显示 stable 槽位。
    /// </summary>
    /// <remarks>
    /// HUD 只做 <c>ScoreProcessor as IStableScoreSource</c>，不按规则集分支；未实现的规则集该槽位自动隐藏。
    /// 是否显示必须看 <see cref="StableAvailable"/>，调用方不应假设「实现了接口就有值」。
    /// </remarks>
    public interface IStableScoreSource
    {
        /// <summary>stable 语义准确率。</summary>
        IBindable<double> StableAccuracy { get; }

        /// <summary>stable 语义总分（不含 Mod 倍率）。</summary>
        IBindable<long> StableScore { get; }

        /// <summary>stable 语义最大连击。</summary>
        IBindable<int> StableMaxCombo { get; }

        /// <summary>本次游玩能否给出 stable 结果。false 时 HUD 不显示该槽位。</summary>
        IBindable<bool> StableAvailable { get; }
    }
}
