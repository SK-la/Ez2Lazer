// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Catch
{
    /// <summary>
    /// 将转盘角速度（轴单位/ms，与帧率无关）映射为 Catch 移动速度倍率。
    /// </summary>
    internal static class CatchScratchSpeedMapper
    {
        /// <summary>达到此角速度（轴单位/ms）时倍率为 <see cref="MaxMultiplier"/>。</summary>
        internal static double ReferenceVelocity { get; set; } = 0.001;

        internal static double MaxMultiplier { get; set; } = 1.5;

        internal static double Map(double angularVelocity)
        {
            double t = normalise(angularVelocity / ReferenceVelocity);
            return 1 + t * (MaxMultiplier - 1);
        }

        private static double normalise(double t) => Math.Clamp(t, 0, 1);
    }
}
