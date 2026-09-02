// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Input;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch
{
    public partial class CatchInputManager
    {
        private readonly ScratchAxisPair scratchAxes = new ScratchAxisPair();

        private ScratchAxisDeviceTracker scratchTracker = null!;
        private GameHost host = null!;

        private Bindable<bool> scratchEnabled = null!;
        private Bindable<bool> catchScratchEz2Enabled = null!;
        private Bindable<string> leftBinding = null!;
        private Bindable<string> rightBinding = null!;
        private Bindable<double> deadzone = null!;
        private Bindable<int> stopThreshold = null!;

        private bool moveLeftInjected;
        private bool moveRightInjected;

        [Resolved(CanBeNull = true)]
        private DrawableCatchRuleset? drawableRuleset { get; set; }

        /// <summary>
        /// 任一转盘处于 pressed 状态时，Catch 判定时间窗放宽生效。
        /// </summary>
        public bool ScratchJudgmentAssistActive => CatchScratchAxisResolver.ResolveActive(scratchAxes.Left, scratchAxes.Right) != null;

        /// <summary>
        /// Ez2Catch 开启且转盘转动时的移动速度倍率（1 ~ 1.5）。
        /// </summary>
        public double ScratchMovementSpeedMultiplier { get; private set; } = 1;

        [BackgroundDependencyLoader]
        private void loadScratchAxis(Ez2ConfigManager ezConfig, ScratchAxisDeviceTracker tracker, GameHost gameHost)
        {
            scratchTracker = tracker;
            host = gameHost;

            scratchEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisEnabled);
            catchScratchEz2Enabled = ezConfig.GetBindable<bool>(Ez2Setting.CatchScratchEz2Enabled);
            leftBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisL);
            rightBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisR);
            deadzone = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisDeadzone);
            stopThreshold = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisStopThreshold);

            scratchAxes.LeftBinding.BindTo(leftBinding);
            scratchAxes.RightBinding.BindTo(rightBinding);
            scratchAxes.BindTuning(deadzone, stopThreshold);

            scratchEnabled.BindValueChanged(_ => releaseInjected(), true);
            catchScratchEz2Enabled.BindValueChanged(e => applyCatchEz2Tuning(e.NewValue), true);
        }

        protected override void Update()
        {
            base.Update();
            pollScratchAxes();
        }

        internal bool ShouldSuppressJoystickAxisButton(JoystickButton button)
        {
            if (!scratchEnabled.Value)
                return false;

            return button >= JoystickButton.FirstAxisNegative;
        }

        private void pollScratchAxes()
        {
            ScratchMovementSpeedMultiplier = 1;

            if (ReplayInputHandler != null || !scratchEnabled.Value || isRelaxActive())
            {
                if (moveLeftInjected || moveRightInjected)
                    releaseInjected();
                return;
            }

            double wallTime = host.UpdateThread.Clock.CurrentTime;
            scratchAxes.UpdateFrom(scratchTracker, wallTime);

            var active = CatchScratchAxisResolver.ResolveActive(scratchAxes.Left, scratchAxes.Right);

            if (catchScratchEz2Enabled.Value && active != null)
                ScratchMovementSpeedMultiplier = CatchScratchSpeedMapper.Map(active.SmoothedAngularVelocity);

            syncInjection(active?.Direction.Value == ScratchAxisDirection.CounterClockwise, CatchAction.MoveLeft, ref moveLeftInjected);
            syncInjection(active?.Direction.Value == ScratchAxisDirection.Clockwise, CatchAction.MoveRight, ref moveRightInjected);
        }

        private void applyCatchEz2Tuning(bool ez2Enabled)
        {
            double deadzoneMultiplier = ez2Enabled ? 0.5 : 1;
            int activationTicks = ez2Enabled ? 1 : 2;

            scratchAxes.Left.DeadzoneMultiplier.Value = deadzoneMultiplier;
            scratchAxes.Right.DeadzoneMultiplier.Value = deadzoneMultiplier;
            scratchAxes.Left.RequiredActivationTicks.Value = activationTicks;
            scratchAxes.Right.RequiredActivationTicks.Value = activationTicks;
        }

        private bool isRelaxActive() => drawableRuleset?.Mods.Any(m => m is ModRelax) == true;

        private void syncInjection(bool nowPressed, CatchAction action, ref bool injected)
        {
            if (nowPressed == injected)
                return;

            if (nowPressed)
            {
                if (action == CatchAction.MoveLeft && moveRightInjected)
                {
                    KeyBindingContainer.TriggerReleased(CatchAction.MoveRight);
                    moveRightInjected = false;
                }
                else if (action == CatchAction.MoveRight && moveLeftInjected)
                {
                    KeyBindingContainer.TriggerReleased(CatchAction.MoveLeft);
                    moveLeftInjected = false;
                }

                KeyBindingContainer.TriggerPressed(action);
                injected = true;
            }
            else if (injected)
            {
                KeyBindingContainer.TriggerReleased(action);
                injected = false;
            }
        }

        private void releaseInjected()
        {
            if (moveLeftInjected)
            {
                KeyBindingContainer.TriggerReleased(CatchAction.MoveLeft);
                moveLeftInjected = false;
            }

            if (moveRightInjected)
            {
                KeyBindingContainer.TriggerReleased(CatchAction.MoveRight);
                moveRightInjected = false;
            }

            scratchAxes.Reset();
        }
    }
}
