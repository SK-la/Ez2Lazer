// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 转盘/编码器轴的转动方向。
    /// Mania：顺逆均视为按下；Catch 逆时针→左、顺时针→右。
    /// </summary>
    public enum ScratchAxisDirection
    {
        None,
        Clockwise,
        CounterClockwise,
    }
}
