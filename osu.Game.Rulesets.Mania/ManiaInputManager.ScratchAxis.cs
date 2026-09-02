// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Input;
using osu.Game.Rulesets.Mania.EzMania.Input;

namespace osu.Game.Rulesets.Mania
{
    public partial class ManiaInputManager
    {
        private readonly ScratchAxisPair scratchAxes = new ScratchAxisPair();

        private ScratchAxisDeviceTracker scratchTracker = null!;
        private GameHost host = null!;

        private Bindable<bool> scratchEnabled = null!;
        private Bindable<bool> skipEmptyEdge = null!;
        private Bindable<string> leftBinding = null!;
        private Bindable<string> rightBinding = null!;
        private Bindable<double> deadzone = null!;
        private Bindable<int> stopThreshold = null!;

        private bool leftInjected;
        private bool rightInjected;
        private ManiaAction leftInjectedAction;
        private ManiaAction rightInjectedAction;

        [BackgroundDependencyLoader]
        private void loadScratchAxis(Ez2ConfigManager ezConfig, ScratchAxisDeviceTracker tracker, GameHost gameHost)
        {
            scratchTracker = tracker;
            host = gameHost;

            scratchEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisEnabled);
            skipEmptyEdge = ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);
            leftBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisL);
            rightBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisR);
            deadzone = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisDeadzone);
            stopThreshold = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisStopThreshold);

            scratchAxes.LeftBinding.BindTo(leftBinding);
            scratchAxes.RightBinding.BindTo(rightBinding);
            scratchAxes.BindTuning(deadzone, stopThreshold);

            scratchEnabled.BindValueChanged(_ => releaseInjected(), true);
            skipEmptyEdge.BindValueChanged(_ => releaseInjected());
        }

        protected override void Update()
        {
            base.Update();
            pollScratchAxes();
        }

        /// <summary>
        /// beatoraja：模拟皿开启时 AXIS± 由算法驱动，不再用轴绝对值正负当键。
        /// 开启转盘后屏蔽所有 Joystick 轴方向键（正/负），避免轴号映射不一致时漏屏蔽导致皿列常亮。
        /// </summary>
        internal bool ShouldSuppressJoystickAxisButton(JoystickButton button)
        {
            if (!scratchEnabled.Value)
                return false;

            return button >= JoystickButton.FirstAxisNegative;
        }

        private void pollScratchAxes()
        {
            if (ReplayInputHandler != null || !scratchEnabled.Value)
            {
                if (leftInjected || rightInjected)
                    releaseInjected();
                return;
            }

            if (!ManiaScratchColumnTemplate.TryResolve(variant, skipEmptyEdge.Value, out int leftCol, out int rightCol))
            {
                if (leftInjected || rightInjected)
                    releaseInjected();
                return;
            }

            double wallTime = host.UpdateThread.Clock.CurrentTime;
            scratchAxes.UpdateFrom(scratchTracker, wallTime);

            syncInjection(scratchAxes.Left.IsPressed.Value, ManiaAction.Key1 + leftCol, ref leftInjected, ref leftInjectedAction);
            syncInjection(scratchAxes.Right.IsPressed.Value, ManiaAction.Key1 + rightCol, ref rightInjected, ref rightInjectedAction);
        }

        private void syncInjection(bool nowPressed, ManiaAction action, ref bool injected, ref ManiaAction injectedAction)
        {
            if (nowPressed == injected && (injectedAction == action || !injected))
                return;

            if (nowPressed)
            {
                if (injected && injectedAction != action)
                    KeyBindingContainer.TriggerReleased(injectedAction);

                KeyBindingContainer.TriggerPressed(action);
                injected = true;
                injectedAction = action;
            }
            else if (injected)
            {
                KeyBindingContainer.TriggerReleased(injectedAction);
                injected = false;
            }
        }

        private void releaseInjected()
        {
            if (leftInjected)
            {
                KeyBindingContainer.TriggerReleased(leftInjectedAction);
                leftInjected = false;
            }

            if (rightInjected)
            {
                KeyBindingContainer.TriggerReleased(rightInjectedAction);
                rightInjected = false;
            }

            scratchAxes.Reset();
        }
    }
}
