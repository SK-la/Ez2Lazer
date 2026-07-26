// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 规则集无关的模拟转盘轴处理器：只认位移，不认停靠绝对值。
    /// </summary>
    /// <remarks>
    /// 对齐 beatoraja Analog Scratch V2（死区 + 墙钟时间；无 tick 量化）：
    /// 两次同向过死区才激活；同向转动中保持按住；反向需累计位移达阈值才清空（另一次打击）；
    /// 停转超时松开。顺逆在 Mania 仍注入同一列。
    /// </remarks>
    public class ScratchAxisProcessor
    {
        public BindableDouble Deadzone { get; } = new BindableDouble(0.04)
        {
            MinValue = 0,
            MaxValue = 0.5,
        };

        public BindableInt StopThresholdMs { get; } = new BindableInt(150)
        {
            MinValue = 10,
            MaxValue = 1000,
        };

        public BindableInt StopThreshold => StopThresholdMs;

        public BindableBool IsPressed { get; } = new BindableBool();

        public Bindable<ScratchAxisDirection> Direction { get; } = new Bindable<ScratchAxisDirection>();

        private float lastValue;
        private bool hasSample;
        private double lastMotionTime = double.NegativeInfinity;

        private int pendingTicks;
        private ScratchAxisDirection pendingDirection = ScratchAxisDirection.None;

        /// <summary>已按下后反向累计弧长；达到 <see cref="reverseClearThreshold"/> 才清空。</summary>
        private float reverseTravel;

        private float reverseClearThreshold => (float)(Deadzone.Value * 2);

        public bool Update(float axisValue, double currentTime)
        {
            bool wasPressed = IsPressed.Value;

            if (!hasSample)
            {
                lastValue = axisValue;
                hasSample = true;
                lastMotionTime = double.NegativeInfinity;
                clearPending();
                reverseTravel = 0;
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
                    handlePressedMotion(dir, absDelta);
                else
                {
                    reverseTravel = 0;
                    accumulatePending(dir);
                }
            }
            else if (IsPressed.Value && absDelta > 1e-5f)
            {
                lastValue = axisValue;
                lastMotionTime = currentTime;
                // 亚死区位移不累计反向，避免噪声抬高 reverseTravel
            }
            else
            {
                lastValue = axisValue;

                if (currentTime - lastMotionTime >= StopThresholdMs.Value)
                {
                    clearPending();
                    reverseTravel = 0;

                    if (IsPressed.Value)
                    {
                        IsPressed.Value = false;
                        Direction.Value = ScratchAxisDirection.None;
                    }
                }
            }

            return wasPressed != IsPressed.Value;
        }

        public bool UpdateMissing(double currentTime)
        {
            if (!IsPressed.Value && pendingTicks == 0)
                return false;

            if (currentTime - lastMotionTime >= StopThresholdMs.Value)
            {
                bool wasPressed = IsPressed.Value;
                clearPending();
                reverseTravel = 0;
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
            reverseTravel = 0;
            IsPressed.Value = false;
            Direction.Value = ScratchAxisDirection.None;
        }

        private void handlePressedMotion(ScratchAxisDirection dir, float absDelta)
        {
            if (Direction.Value != ScratchAxisDirection.None && Direction.Value != dir)
            {
                reverseTravel += absDelta;

                if (reverseTravel >= reverseClearThreshold)
                {
                    // V2：确认转向（累计反向足够）→ 清空；本帧记 pending=1
                    IsPressed.Value = false;
                    Direction.Value = ScratchAxisDirection.None;
                    reverseTravel = 0;
                    pendingTicks = 1;
                    pendingDirection = dir;
                }

                // 反向尚未达标：保持按住，不改 Direction
                return;
            }

            reverseTravel = 0;
            Direction.Value = dir;
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
                reverseTravel = 0;
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
