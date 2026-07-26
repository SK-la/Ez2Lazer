// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Input;
using osu.Game.Rulesets.Mania.EzMania.Input;
using osu.Game.Rulesets.UI;

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

        private int leftScratchAxisIndex = -1;
        private int rightScratchAxisIndex = -1;

        private bool leftInjected;
        private bool rightInjected;
        private ManiaAction leftInjectedAction;
        private ManiaAction rightInjectedAction;

        [BackgroundDependencyLoader]
        private void loadScratchAxis(Ez2ConfigManager ezConfig, ScratchAxisDeviceTracker tracker, GameHost gameHost)
        {
            scratchTracker = tracker;
            host = gameHost;

            scratchEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ManiaScratchAxisEnabled);
            skipEmptyEdge = ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);
            leftBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisL);
            rightBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisR);
            deadzone = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisDeadzone);
            stopThreshold = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisStopThreshold);

            scratchAxes.LeftBinding.BindTo(leftBinding);
            scratchAxes.RightBinding.BindTo(rightBinding);
            scratchAxes.BindTuning(deadzone, stopThreshold);

            leftBinding.BindValueChanged(_ => refreshScratchAxisIndices(), true);
            rightBinding.BindValueChanged(_ => refreshScratchAxisIndices());

            scratchEnabled.BindValueChanged(_ => releaseInjected(), true);
            skipEmptyEdge.BindValueChanged(_ => releaseInjected());
        }

        protected override void Update()
        {
            base.Update();
            pollScratchAxes();
        }

        /// <summary>
        /// beatoraja：模拟皿开启时 AXIS± 由算法驱动，不再用「轴绝对值正负」当键。
        /// osu 默认 JoystickAxisInput 会在过零时疯狂切换 Axis+/−，若绑到皿列就会每秒上百次 OnPressed。
        /// </summary>
        internal bool ShouldSuppressJoystickAxisButton(JoystickButton button)
        {
            if (!scratchEnabled.Value)
                return false;

            if (!tryGetAxisDirectionIndex(button, out int axisIndex))
                return false;

            return axisIndex == leftScratchAxisIndex || axisIndex == rightScratchAxisIndex;
        }

        private void refreshScratchAxisIndices()
        {
            var left = ScratchAxisBinding.Parse(leftBinding.Value);
            var right = ScratchAxisBinding.Parse(rightBinding.Value);
            leftScratchAxisIndex = left.IsEmpty ? -1 : left.AxisIndex;
            rightScratchAxisIndex = right.IsEmpty ? -1 : right.AxisIndex;
        }

        private static bool tryGetAxisDirectionIndex(JoystickButton button, out int axisIndex)
        {
            if (button >= JoystickButton.FirstAxisPositive)
            {
                axisIndex = button - JoystickButton.FirstAxisPositive;
                return true;
            }

            if (button >= JoystickButton.FirstAxisNegative)
            {
                axisIndex = button - JoystickButton.FirstAxisNegative;
                return true;
            }

            axisIndex = -1;
            return false;
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

            // 墙钟：避免 FrameStable 停表导致永不松开
            double wallTime = host.UpdateThread.Clock.CurrentTime;
            scratchAxes.UpdateFrom(scratchTracker, wallTime);

            syncInjection(scratchAxes.Left.IsPressed.Value, ManiaAction.Key1 + leftCol, ref leftInjected, ref leftInjectedAction);
            syncInjection(scratchAxes.Right.IsPressed.Value, ManiaAction.Key1 + rightCol, ref rightInjected, ref rightInjectedAction);
        }

        private void syncInjection(bool nowPressed, ManiaAction action, ref bool injected, ref ManiaAction injectedAction)
        {
            if (nowPressed)
            {
                // 若 Axis± 键位曾把同一 action 松开，pressedActions 可能已丢，需重新断言按住
                bool missingFromContainer = !KeyBindingContainer.PressedActions.Contains(action);

                if (!injected || missingFromContainer)
                {
                    KeyBindingContainer.TriggerPressed(action);
                    injected = true;
                    injectedAction = action;
                }
                else if (injectedAction != action)
                {
                    KeyBindingContainer.TriggerReleased(injectedAction);
                    KeyBindingContainer.TriggerPressed(action);
                    injectedAction = action;
                }
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
