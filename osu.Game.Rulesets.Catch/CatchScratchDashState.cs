// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Catch
{
    /// <summary>
    /// Ez2Catch 转盘 Dash 状态机：角加速度进入、转速退出；Dash 内无 Walk→Dash 过渡，仅 Dash 倍率 1~1.5 线性。
    /// </summary>
    internal class CatchScratchDashState
    {
        /// <summary>角加速度进入 Dash（轴单位/ms²）。</summary>
        internal static double EnterAcceleration => 0.0005;

        /// <summary>平滑角速度低于此值退出 Dash（轴单位/ms，应低于进入 Dash 时的典型转速）。</summary>
        internal static double ExitVelocity => 0.00010;

        /// <summary>Dash 内角加速度映射到 <see cref="MaxDashMultiplier"/> 的上限（轴单位/ms²）。</summary>
        internal static double MaxAcceleration => 0.0002;

        internal static double MaxDashMultiplier => 1.5;

        private bool dashActive;

        internal void Reset() => dashActive = false;

        /// <returns>是否处于 Dash；Dash 速度倍率（1~MaxDashMultiplier，仅 Dash 内有效）。</returns>
        internal (bool dashActive, double dashSpeedMultiplier) Update(double angularAcceleration, double smoothedAngularVelocity)
        {
            if (!dashActive && angularAcceleration >= EnterAcceleration)
                dashActive = true;
            else if (dashActive && smoothedAngularVelocity <= ExitVelocity)
                dashActive = false;

            if (!dashActive)
                return (false, 1);

            // 已进入 Dash：默认 1× Dash；更高角加速度线性升至 MaxDashMultiplier，无 Walk 中间档。
            if (angularAcceleration <= EnterAcceleration || MaxAcceleration <= EnterAcceleration)
                return (true, 1);

            double t = Math.Clamp((angularAcceleration - EnterAcceleration) / (MaxAcceleration - EnterAcceleration), 0, 1);
            return (true, 1 + t * (MaxDashMultiplier - 1));
        }
    }
}
