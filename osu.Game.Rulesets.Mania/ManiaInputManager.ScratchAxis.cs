// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
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

            scratchEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ManiaScratchAxisEnabled);
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

            // FrameStable 的 Time.Current 在追帧/进图停表时可能长时间不变，停转阈值永远到不了 → 列一直按住。
            double wallTime = host.UpdateThread.Clock.CurrentTime;
            scratchAxes.UpdateFrom(scratchTracker, wallTime);

            syncInjection(scratchAxes.Left.IsPressed.Value, ManiaAction.Key1 + leftCol, ref leftInjected, ref leftInjectedAction);
            syncInjection(scratchAxes.Right.IsPressed.Value, ManiaAction.Key1 + rightCol, ref rightInjected, ref rightInjectedAction);
        }

        private void syncInjection(bool nowPressed, ManiaAction action, ref bool injected, ref ManiaAction injectedAction)
        {
            if (nowPressed)
            {
                if (!injected)
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
