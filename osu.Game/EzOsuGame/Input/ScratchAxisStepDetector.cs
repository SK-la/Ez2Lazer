// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 把转盘的连续位移量化成「格」：每转过 <see cref="StepSize"/> 的累计位移输出一次 ±1。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ScratchAxisProcessor"/> 互补：后者回答「是否正在转 / 转向哪边」（打击用语义），
    /// 本类回答「转过了几格」（步进导航语义，如选曲时的 ↑/↓）。
    /// <list type="bullet">
    /// <item>单帧最多输出 1 格：选曲链路（<c>Carousel</c>）用 <c>Scheduler.AddOnce</c> 合并同帧请求，
    /// 一帧内输出多格会被吞掉。因此 <see cref="StepSize"/> 的实际表现是「多久能出下一格」，
    /// 最快也只能到每帧一格（60 格/秒）。</item>
    /// <item>余量（欠账）最多保留一格，且停转 <see cref="IdleResetMs"/> 后丢弃，避免松手后继续跳。</item>
    /// <item>单帧位移低于 <see cref="Deadzone"/> 视为抖动、不参与累积。该门限刻意远小于转盘轴的
    /// 「最小位移阈值」，否则慢速转动（每帧位移小于打击死区）永远累积不出格。</item>
    /// <item>位移按 <see cref="ScratchAxisProcessor.ShortestDelta"/> 的最短弧计算，因此 0→1→0 的绕回不会误判为反向。</item>
    /// </list>
    /// </remarks>
    public class ScratchAxisStepDetector
    {
        /// <summary>
        /// 单帧位移低于此值视为抖动，不参与累积。
        /// </summary>
        /// <remarks>
        /// 必须远小于转盘的「最小位移阈值」（默认 0.005）：本类是累积语义，
        /// 若直接沿用打击用的单帧死区，慢速转动（每帧位移 &lt; 死区）会永远无法累积、完全不响应。
        /// </remarks>
        public BindableDouble Deadzone { get; } = new BindableDouble(0.001)
        {
            MinValue = 0,
            MaxValue = 0.05,
        };

        /// <summary>
        /// 每格所需累计位移（轴单位，1.0 ≈ 一整圈）。
        /// </summary>
        public BindableDouble StepSize { get; } = new BindableDouble(0.2)
        {
            MinValue = 0.05,
            MaxValue = 1,
        };

        /// <summary>
        /// 停转超过此时长后丢弃尚未输出的欠账（避免松手后继续跳格）。
        /// </summary>
        public BindableDouble IdleResetMs { get; } = new BindableDouble(80)
        {
            MinValue = 10,
            MaxValue = 1000,
        };

        private float lastValue;
        private bool hasSample;
        private double accumulated;
        private double lastMotionTime = double.NegativeInfinity;

        /// <summary>
        /// 送入本帧轴值与时间戳，返回本帧应执行的格数（正 = <see cref="ScratchAxisDirection.Clockwise"/>）。
        /// 无采样（首次）时返回 0。
        /// </summary>
        public int Update(float axisValue, double currentTime)
        {
            if (!hasSample)
            {
                lastValue = axisValue;
                hasSample = true;
                accumulated = 0;
                lastMotionTime = currentTime;
                return 0;
            }

            float delta = ScratchAxisProcessor.ShortestDelta(lastValue, axisValue);
            lastValue = axisValue;

            // 停转：丢弃尚未输出的余量（不影响之后的继续累积）。
            if (currentTime - lastMotionTime > IdleResetMs.Value)
                accumulated = 0;

            if (Math.Abs(delta) < Deadzone.Value)
                return 0;

            lastMotionTime = currentTime;
            accumulated += delta;

            double step = Math.Max(StepSize.Value, 0.001);

            if (Math.Abs(accumulated) < step)
                return 0;

            int consumed = Math.Sign(accumulated);
            accumulated -= consumed * step;

            // 单帧只能落实一格（Carousel 的同帧请求会被 Scheduler.AddOnce 合并），
            // 因此欠账最多保留一格，多出的位移丢弃，避免松手后长时间继续跳。
            accumulated = Math.Clamp(accumulated, -step, step);

            return consumed;
        }

        /// <summary>
        /// 丢弃采样状态（轴失联 / 功能关闭 / 离开选曲界面时调用）。
        /// </summary>
        public void Reset()
        {
            hasSample = false;
            lastValue = 0;
            accumulated = 0;
            lastMotionTime = double.NegativeInfinity;
        }
    }
}
