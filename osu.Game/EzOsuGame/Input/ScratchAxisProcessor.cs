// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 模拟转盘轴：只认位移（含 0→1→0 环绕），不认停靠绝对值。
    /// </summary>
    /// <remarks>
    /// 对齐 beatoraja Analog Scratch V2 语义（时间停转 + 死区；无 tick 量化）：
    /// <list type="bullet">
    /// <item>每帧读当前位置；与上次不同且 |最短弧 Δ|≥有效死区 → 运动</item>
    /// <item>同向连续转动（含绕回 1→0）保持按下；仅停转超时或确认反向才松开</item>
    /// <item>激活需连续 <see cref="RequiredActivationTicks"/> 次同向有效位移；反向累计达 2×有效死区视为另一次打击</item>
    /// </list>
    /// beatoraja 原始轴多为 [-1,1]（用户侧常说 0–100 再映射）；绕回用最短弧，与 computeAnalogDiff 同类。
    /// </remarks>
    public class ScratchAxisProcessor
    {
        public BindableDouble Deadzone { get; } = new BindableDouble(0.005)
        {
            MinValue = 0,
            MaxValue = 0.05,
        };

        /// <summary>
        /// 有效死区 = <see cref="Deadzone"/> × 本系数（Catch Ez2 可设为 0.5）。
        /// </summary>
        public BindableDouble DeadzoneMultiplier { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 2,
        };

        /// <summary>
        /// 从未 pressed 到 pressed 所需的连续同向有效位移帧数（默认 2；Catch Ez2 可设为 1）。
        /// </summary>
        public BindableInt RequiredActivationTicks { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        public BindableInt StopThresholdMs { get; } = new BindableInt(30)
        {
            MinValue = 10,
            MaxValue = 300,
        };

        public BindableInt StopThreshold => StopThresholdMs;

        public BindableBool IsPressed { get; } = new BindableBool();

        public Bindable<ScratchAxisDirection> Direction { get; } = new Bindable<ScratchAxisDirection>();

        /// <summary>
        /// 最近一次有效转动的角速度（轴单位 / ms）。
        /// </summary>
        public double AngularVelocity { get; private set; }

        /// <summary>
        /// 带时间衰减的平滑角速度（轴单位/ms），与帧率无关。
        /// </summary>
        public double SmoothedAngularVelocity { get; private set; }

        /// <summary>
        /// 平滑角速度半衰期（ms）。热重载调试时可改。
        /// </summary>
        public static double SmoothedVelocityHalfLifeMs { get; set; } = 50;

        /// <summary>
        /// 最近一次有效转动的时间（wall clock ms），供多转盘 last-active 仲裁。
        /// </summary>
        public double LastMotionTime { get; private set; } = double.NegativeInfinity;

        private float lastValue;
        private bool hasSample;

        private double lastMotionTime = double.NegativeInfinity;
        private double lastUpdateTime = double.NegativeInfinity;

        private int pendingTicks;
        private ScratchAxisDirection pendingDirection = ScratchAxisDirection.None;

        private float reverseTravel;

        private double effectiveDeadzone => Deadzone.Value * DeadzoneMultiplier.Value;

        private float reverseClearThreshold => (float)(effectiveDeadzone * 2);

        public bool Update(float axisValue, double currentTime)
        {
            bool wasPressed = IsPressed.Value;

            applyTimeBasedSmoothingDecay(currentTime);
            lastUpdateTime = currentTime;

            if (!hasSample)
            {
                lastValue = axisValue;
                hasSample = true;
                LastMotionTime = lastMotionTime = double.NegativeInfinity;
                AngularVelocity = 0;
                SmoothedAngularVelocity = 0;
                clearPending();
                reverseTravel = 0;
                return false;
            }

            float delta = shortestDelta(lastValue, axisValue);
            float absDelta = Math.Abs(delta);
            double deadzone = effectiveDeadzone;

            if (absDelta >= deadzone)
            {
                double previousMotionTime = lastMotionTime;

                lastValue = axisValue;
                LastMotionTime = lastMotionTime = currentTime;

                if (previousMotionTime > double.NegativeInfinity && currentTime > previousMotionTime)
                    AngularVelocity = absDelta / Math.Max(currentTime - previousMotionTime, 1);
                else
                    AngularVelocity = 0;

                SmoothedAngularVelocity = Math.Max(SmoothedAngularVelocity, AngularVelocity);

                var dir = delta > 0 ? ScratchAxisDirection.Clockwise : ScratchAxisDirection.CounterClockwise;

                if (IsPressed.Value)
                    handlePressedMotion(dir, absDelta);
                else
                {
                    reverseTravel = 0;
                    accumulatePending(dir);
                }
            }
            else
            {
                // 亚死区：只跟随停靠点，绝不刷新 lastMotionTime。
                // 否则静止噪声会让停转永远到不了 → 轨道灯常亮。
                lastValue = axisValue;

                if (currentTime - lastMotionTime >= StopThresholdMs.Value)
                {
                    clearPending();
                    reverseTravel = 0;
                    AngularVelocity = 0;
                    SmoothedAngularVelocity = 0;

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
                AngularVelocity = 0;
                SmoothedAngularVelocity = 0;
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
            LastMotionTime = lastMotionTime = double.NegativeInfinity;
            AngularVelocity = 0;
            SmoothedAngularVelocity = 0;
            lastUpdateTime = double.NegativeInfinity;
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
                    IsPressed.Value = false;
                    Direction.Value = ScratchAxisDirection.None;
                    reverseTravel = 0;
                    pendingTicks = 1;
                    pendingDirection = dir;
                }

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

            if (pendingTicks >= RequiredActivationTicks.Value)
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

        private void applyTimeBasedSmoothingDecay(double currentTime)
        {
            if (SmoothedAngularVelocity <= 0 || lastUpdateTime <= double.NegativeInfinity)
                return;

            double elapsedMs = Math.Max(currentTime - lastUpdateTime, 0);

            if (elapsedMs <= 0)
                return;

            double halfLife = Math.Max(SmoothedVelocityHalfLifeMs, 1);
            SmoothedAngularVelocity *= Math.Pow(0.5, elapsedMs / halfLife);
        }

        public static float ShortestDelta(float from, float to) => shortestDelta(from, to);

        /// <summary>
        /// [-1,1] 环上最短弧。对应 0→100→0 单调绕回：例如 0.95→-0.95 视为小步前进而非大步后退。
        /// </summary>
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
