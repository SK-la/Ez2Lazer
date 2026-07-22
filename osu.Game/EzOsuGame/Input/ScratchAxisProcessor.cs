// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 规则集无关的模拟转盘轴处理器：只认位移，不认停靠绝对值。
    /// 轴可停在 [-1,1] 任意位置；连续同向有效位移→按下并保持；转向清空后需重新激活；停止超过阈值→松开。
    /// </summary>
    /// <remarks>
    /// 对齐 beatoraja Analog Scratch V2（死区 + 墙钟时间阈值；无 tick 量化）：
    /// 两次同向过死区才激活；转向清空视为另一次打击；顺逆在 Mania 仍注入同一列。
    /// 供 Mania scratch、未来 Catch 左右移动、选歌滚动等复用。
    /// </remarks>
    public class ScratchAxisProcessor
    {
        /// <summary>
        /// 激活死区：环绕最短弧 |Δ| 低于此值不视为开始转动（不是“离中心多远”）。
        /// </summary>
        public BindableDouble Deadzone { get; } = new BindableDouble(0.04)
        {
            MinValue = 0,
            MaxValue = 0.5,
        };

        /// <summary>
        /// 停转判定：距上次有效位移超过多少毫秒后松开。
        /// </summary>
        public BindableInt StopThresholdMs { get; } = new BindableInt(150)
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

        /// <summary>激活前连续同向有效位移次数（替代 beatoraja V2 的 2-tick，无量化步进）。</summary>
        private int pendingTicks;

        private ScratchAxisDirection pendingDirection = ScratchAxisDirection.None;

        /// <summary>
        /// 喂入当前轴绝对值与<strong>墙钟时间</strong>（勿用 gameplay / FrameStable 的 <c>Time.Current</c>，暂停或追帧时会卡住导致永不松开）。
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
                clearPending();
                return false;
            }

            float delta = shortestDelta(lastValue, axisValue);
            float absDelta = Math.Abs(delta);

            if (absDelta >= Deadzone.Value)
            {
                lastValue = axisValue;
                lastMotionTime = currentTime;

                var dir = delta > 0 ? ScratchAxisDirection.Clockwise : ScratchAxisDirection.CounterClockwise;

                if (IsPressed.Value)
                {
                    if (Direction.Value != ScratchAxisDirection.None && Direction.Value != dir)
                    {
                        // V2：转向清空，本帧反向位移只记 pending=1，不立刻再激活
                        IsPressed.Value = false;
                        Direction.Value = ScratchAxisDirection.None;
                        pendingTicks = 1;
                        pendingDirection = dir;
                    }
                    else
                    {
                        Direction.Value = dir;
                    }
                }
                else
                {
                    accumulatePending(dir);
                }
            }
            else if (IsPressed.Value && absDelta > 1e-5f)
            {
                // 已按下：亚死区同向慢移续按住（不改方向语义）
                lastValue = axisValue;
                lastMotionTime = currentTime;
            }
            else
            {
                // 微抖动跟随；不刷新 lastMotionTime
                lastValue = axisValue;

                if (currentTime - lastMotionTime >= StopThresholdMs.Value)
                {
                    clearPending();

                    if (IsPressed.Value)
                    {
                        IsPressed.Value = false;
                        Direction.Value = ScratchAxisDirection.None;
                    }
                }
            }

            return wasPressed != IsPressed.Value;
        }

        /// <summary>
        /// 本帧没有有效采样（设备未上报）时调用：仅推进停转计时，不把缺失当成回到 0。
        /// </summary>
        public bool UpdateMissing(double currentTime)
        {
            if (!IsPressed.Value && pendingTicks == 0)
                return false;

            if (currentTime - lastMotionTime >= StopThresholdMs.Value)
            {
                bool wasPressed = IsPressed.Value;
                clearPending();
                IsPressed.Value = false;
                Direction.Value = ScratchAxisDirection.None;
                return wasPressed;
            }

            return false;
        }

        public void Reset()
        {
            hasSample = false;
            lastValue = 0;
            lastMotionTime = double.NegativeInfinity;
            clearPending();
            IsPressed.Value = false;
            Direction.Value = ScratchAxisDirection.None;
        }

        private void accumulatePending(ScratchAxisDirection dir)
        {
            if (pendingDirection == dir)
                pendingTicks++;
            else
            {
                pendingTicks = 1;
                pendingDirection = dir;
            }

            if (pendingTicks >= 2)
            {
                IsPressed.Value = true;
                Direction.Value = dir;
                clearPending();
            }
        }

        private void clearPending()
        {
            pendingTicks = 0;
            pendingDirection = ScratchAxisDirection.None;
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
