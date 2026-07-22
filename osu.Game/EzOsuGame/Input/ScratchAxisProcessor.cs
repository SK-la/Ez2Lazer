// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 规则集无关的模拟转盘轴处理器：只认位移，不认停靠绝对值。
    /// 轴可停在 [-1,1] 任意位置；有有效 |Δ|→按下，停止超过阈值→松开。
    /// </summary>
    /// <remarks>
    /// 供 Mania scratch、未来 Catch 左右移动、选歌滚动等复用。
    /// </remarks>
    public class ScratchAxisProcessor
    {
        /// <summary>
        /// 位移死区：环绕最短弧 |Δ| 低于此值视为静止/抖动（不是“离中心多远”）。
        /// </summary>
        public BindableDouble Deadzone { get; } = new BindableDouble(0.04)
        {
            MinValue = 0,
            MaxValue = 0.5,
        };

        /// <summary>
        /// 停转判定：距上次有效位移超过多少毫秒后松开。
        /// </summary>
        public BindableInt StopThresholdMs { get; } = new BindableInt(80)
        {
            MinValue = 10,
            MaxValue = 1000,
        };

        /// <summary>兼容旧绑定名，等同 <see cref="StopThresholdMs"/>。</summary>
        public BindableInt StopThreshold => StopThresholdMs;

        public BindableBool IsPressed { get; } = new BindableBool();

        public Bindable<ScratchAxisDirection> Direction { get; } = new Bindable<ScratchAxisDirection>();

        private float lastValue;
        private bool hasSample;
        private double lastMotionTime = double.NegativeInfinity;

        /// <summary>
        /// 喂入当前轴绝对值与当前时间（通常为 <c>Time.Current</c>）。
        /// 仅位移超过死区视为转动；停靠在任意绝对值都不是按下。
        /// </summary>
        public bool Update(float axisValue, double currentTime)
        {
            bool wasPressed = IsPressed.Value;

            if (!hasSample)
            {
                // 记录静止停靠点，绝不把「当前位置相对 0」当成转动
                lastValue = axisValue;
                hasSample = true;
                lastMotionTime = double.NegativeInfinity;
                return false;
            }

            float delta = shortestDelta(lastValue, axisValue);
            float absDelta = Math.Abs(delta);

            if (absDelta >= Deadzone.Value)
            {
                lastValue = axisValue;
                lastMotionTime = currentTime;
                Direction.Value = delta > 0 ? ScratchAxisDirection.Clockwise : ScratchAxisDirection.CounterClockwise;
                IsPressed.Value = true;
            }
            else
            {
                // 微抖动跟随，避免慢漂移一次越过死区；不刷新 lastMotionTime
                lastValue = axisValue;

                if (IsPressed.Value && currentTime - lastMotionTime >= StopThresholdMs.Value)
                {
                    IsPressed.Value = false;
                    Direction.Value = ScratchAxisDirection.None;
                }
            }

            return wasPressed != IsPressed.Value;
        }

        /// <summary>
        /// 本帧没有有效采样（设备未上报）时调用：仅推进停转计时，不把缺失当成回到 0。
        /// </summary>
        public bool UpdateMissing(double currentTime)
        {
            if (!IsPressed.Value)
                return false;

            bool wasPressed = true;

            if (currentTime - lastMotionTime >= StopThresholdMs.Value)
            {
                IsPressed.Value = false;
                Direction.Value = ScratchAxisDirection.None;
                return true;
            }

            return wasPressed != IsPressed.Value;
        }

        public void Reset()
        {
            hasSample = false;
            lastValue = 0;
            lastMotionTime = double.NegativeInfinity;
            IsPressed.Value = false;
            Direction.Value = ScratchAxisDirection.None;
        }

        public static float ShortestDelta(float from, float to) => shortestDelta(from, to);

        private static float shortestDelta(float from, float to)
        {
            float delta = to - from;

            if (delta > 1f)
                delta -= 2f;
            else if (delta < -1f)
                delta += 2f;

            return delta;
        }
    }
}
