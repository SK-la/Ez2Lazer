// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Objects
{
    /// <summary>
    /// 允许 hitobject 选择不延长其谱面的可玩区域。
    /// </summary>
    /// <remarks>
    /// 当 <see cref="ExcludeEndTimeFromPlayableLength"/> 为真时，可玩边界只考虑物件开始时间。
    /// 用于时长来自解析/转换伪影而非谱面实际占用时间的物件，否则结束时间会报出谱面并不具备的长度。
    /// </remarks>
    public interface IExcludeEndTimeFromPlayableLength
    {
        /// <summary>
        /// 计算可玩区域时是否排除本物件的结束时间。
        /// </summary>
        bool ExcludeEndTimeFromPlayableLength { get; }
    }
}
