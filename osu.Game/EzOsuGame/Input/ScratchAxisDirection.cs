// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 转盘/编码器轴的转动方向。
    /// Mania：顺逆均视为按下；选歌滚动与 Catch 左右移动可据此区分。
    /// </summary>
    /// <remarks>
    /// TODO(Catch)：逆时针→左、顺时针→右，作为 Catch 移动输入时再接消费方。
    /// TODO(SongSelect)：类滚轮滚动选歌时按方向消费。
    /// </remarks>
    public enum ScratchAxisDirection
    {
        None,
        Clockwise,
        CounterClockwise,
    }
}
