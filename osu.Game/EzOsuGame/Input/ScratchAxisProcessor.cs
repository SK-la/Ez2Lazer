// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 规则集无关的模拟转盘轴处理器（beatoraja AnalogScratch Ver1 + 位移死区）。
    /// 轴范围假定为 [-1, 1]；有有效位移→按下，连续无位移达阈值→松开。
    /// </summary>
    /// <remarks>
    /// 供 Mania scratch、未来 Catch 左右移动、选歌滚动等复用；不要绑到具体 <c>ManiaAction</c>。
    /// </remarks>
    public class ScratchAxisProcessor
    {
        /// <summary>
        /// 位移死区：|Δ|（环绕最短弧）低于此值视为抖动。
        /// </summary>
        public BindableDouble Deadzone { get; } = new BindableDouble(0.02)
        {
            MinValue = 0,
            MaxValue = 0.5,
        };

        /// <summary>
        /// 停转帧阈值：连续多少次 <see cref="Update"/> 无有效位移后松开。
        /// </summary>
        public BindableInt StopThreshold { get; } = new BindableInt(100)
        {
            MinValue = 1,
            MaxValue = 1000,
        };

        public BindableBool IsPressed { get; } = new BindableBool();

        public Bindable<ScratchAxisDirection> Direction { get; } = new Bindable<ScratchAxisDirection>();

        private float lastValue;
        private bool hasSample;
        private int idleFrames;

        /// <summary>
        /// 喂入当前轴绝对值（通常每帧一次）。返回是否发生按下/松开边沿。
        /// </summary>
        public bool Update(float axisValue)
        {
            bool wasPressed = IsPressed.Value;

            if (!hasSample)
            {
                lastValue = axisValue;
                hasSample = true;
                return false;
            }

            float delta = shortestDelta(lastValue, axisValue);
            float absDelta = Math.Abs(delta);

            if (absDelta >= Deadzone.Value)
            {
                lastValue = axisValue;
                idleFrames = 0;

                var dir = delta > 0 ? ScratchAxisDirection.Clockwise : ScratchAxisDirection.CounterClockwise;
                Direction.Value = dir;
                IsPressed.Value = true;
            }
            else
            {
                // 跟随微抖动，避免慢漂移累积后一次越过死区
                lastValue = axisValue;
                idleFrames++;

                if (IsPressed.Value && idleFrames > StopThreshold.Value)
                {
                    IsPressed.Value = false;
                    Direction.Value = ScratchAxisDirection.None;
                    idleFrames = 0;
                }
            }

            return wasPressed != IsPressed.Value;
        }

        public void Reset()
        {
            hasSample = false;
            lastValue = 0;
            idleFrames = 0;
            IsPressed.Value = false;
            Direction.Value = ScratchAxisDirection.None;
        }

        /// <summary>
        /// [-1,1] 环上的最短有符号位移（全周长 2）。
        /// </summary>
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
