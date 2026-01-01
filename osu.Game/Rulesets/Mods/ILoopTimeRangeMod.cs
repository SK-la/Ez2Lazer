// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// 用于链接循环时间范围的Mod接口。
    /// 单位是ms。
    /// </summary>
    public interface ILoopTimeRangeMod
    {
        /// <summary>
        /// 更新循环时间范围。
        /// </summary>
        /// <param name="startTime">Start time in milliseconds.</param>
        /// <param name="endTime">End time in milliseconds.</param>
        void SetLoopTimeRange(double startTime, double endTime);
    }
}
