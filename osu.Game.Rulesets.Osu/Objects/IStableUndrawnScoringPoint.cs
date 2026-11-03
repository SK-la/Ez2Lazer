// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Objects
{
    /// <summary>
    /// osu!stable 不绘制的滑条评分点。
    /// </summary>
    /// <remarks>
    /// stable 仅在评分点距头尾至少一个物件半径时才把 tick/折返精灵加入绘制，但仍照常判定与计分
    /// （<c>SliderOsu.cs:1024-1029</c>）。Aspire 滑条把数千折返叠进不足一单位，几乎每个都与两端重叠。
    /// </remarks>
    public interface IStableUndrawnScoringPoint
    {
        /// <summary>
        /// stable 是否不绘制该点。
        /// </summary>
        bool UndrawnUnderStableRules { get; set; }
    }
}
