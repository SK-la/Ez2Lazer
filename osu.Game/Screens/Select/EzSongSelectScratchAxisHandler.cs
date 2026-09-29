// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Input;
using osu.Game.Input.Bindings;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// 常规选曲界面（<see cref="SoloSongSelect"/>）用 L/R 转盘选曲：
    /// 把转动量化成格，逐格注入 <see cref="GlobalAction.SelectNext"/> /
    /// <see cref="GlobalAction.SelectPrevious"/>，与按 ↑/↓ 走完全同一条链路
    /// （<c>Carousel</c> 的 keyboard traversal）。
    /// </summary>
    /// <remarks>
    /// 复用全局缓存的 <see cref="ScratchAxisDeviceTracker"/> 与转盘轴设置里同一份 L/R 绑定，
    /// 但不沿用打击用的单帧死区（原因见 <see cref="ScratchAxisStepDetector"/>）。
    /// 不涉及 Mania/Catch 的打击注入（那是各规则集 InputManager 的职责）。
    /// </remarks>
    public partial class EzSongSelectScratchAxisHandler : Component
    {
        /// <summary>本处理器是否应工作，由所属屏幕给出（例如 <c>() =&gt; this.IsCurrentScreen()</c>）。</summary>
        private readonly Func<bool> isActive;

        private readonly ScratchAxisStepDetector leftDetector = new ScratchAxisStepDetector();
        private readonly ScratchAxisStepDetector rightDetector = new ScratchAxisStepDetector();

        private ScratchAxisDeviceTracker tracker = null!;
        private GlobalActionContainer globalBindings = null!;
        private GameHost host = null!;

        private Bindable<bool> songSelectEnabled = null!;
        private Bindable<bool> invert = null!;
        private Bindable<string> leftBinding = null!;
        private Bindable<string> rightBinding = null!;
        private Bindable<double> stepSize = null!;

        private ScratchAxisBinding leftAxis = ScratchAxisBinding.Empty;
        private ScratchAxisBinding rightAxis = ScratchAxisBinding.Empty;

        private bool wasPolling;

        public EzSongSelectScratchAxisHandler(Func<bool> isActive)
        {
            this.isActive = isActive;
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, ScratchAxisDeviceTracker scratchTracker, GlobalActionContainer globalActionContainer, GameHost gameHost)
        {
            tracker = scratchTracker;
            globalBindings = globalActionContainer;
            host = gameHost;

            songSelectEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisSongSelectEnabled);
            invert = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisSongSelectInvert);
            leftBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisL);
            rightBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisR);

            // 注意：GetBindable 返回的是弱绑定的副本，必须用字段持有强引用，
            // 否则 load 结束后副本被 GC 回收、绑定断开（表现为「设置只在启动时读一次」）。
            stepSize = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisSongSelectStep);

            // 这里刻意不共用转盘轴的「最小位移阈值」——选曲是累积语义，
            // 沿用打击用的单帧死区会让慢速转动完全累积不出格。
            leftDetector.StepSize.BindTo(stepSize);
            rightDetector.StepSize.BindTo(stepSize);

            leftBinding.BindValueChanged(_ => leftAxis = ScratchAxisBinding.Parse(leftBinding.Value), true);
            rightBinding.BindValueChanged(_ => rightAxis = ScratchAxisBinding.Parse(rightBinding.Value), true);

            // 开关／方向变化时清空累积，避免关掉再打开后补跳。
            songSelectEnabled.BindValueChanged(_ => resetDetectors());
            invert.BindValueChanged(_ => resetDetectors());
        }

        private bool shouldPoll => songSelectEnabled.Value
                                   && (!leftAxis.IsEmpty || !rightAxis.IsEmpty)
                                   && isActive();

        protected override void Update()
        {
            base.Update();

            if (!shouldPoll)
            {
                if (wasPolling)
                {
                    wasPolling = false;
                    resetDetectors();
                }

                return;
            }

            wasPolling = true;

            double wallTime = host.UpdateThread.Clock.CurrentTime;

            int steps = pollAxis(leftDetector, leftAxis, wallTime) + pollAxis(rightDetector, rightAxis, wallTime);

            if (steps != 0)
                injectSelectionStep(steps);
        }

        private int pollAxis(ScratchAxisStepDetector detector, ScratchAxisBinding binding, double wallTime)
        {
            if (binding.IsEmpty || !tracker.TryGetValue(binding, out float value))
            {
                detector.Reset();
                return 0;
            }

            return detector.Update(value, wallTime);
        }

        /// <summary>
        /// 一格转动 → 一次全局选曲动作（等价于按一次 ↑/↓）。
        /// </summary>
        private void injectSelectionStep(int direction)
        {
            bool forward = direction > 0;

            if (invert.Value)
                forward = !forward;

            var action = forward ? GlobalAction.SelectNext : GlobalAction.SelectPrevious;

            // 按下后立即松开，模拟点按；Carousel 用 Scheduler.AddOnce 处理，同帧合并不会丢。
            globalBindings.TriggerPressed(action);
            globalBindings.TriggerReleased(action);
        }

        private void resetDetectors()
        {
            leftDetector.Reset();
            rightDetector.Reset();
        }
    }
}
