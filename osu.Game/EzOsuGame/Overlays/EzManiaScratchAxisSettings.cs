// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Input;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Mania L/R 转盘轴设置：按设备 GUID+轴绑定，支持多设备/同设备多轴。
    /// </summary>
    public partial class EzManiaScratchAxisSettings : FillFlowContainer
    {
        private readonly ScratchAxisProcessor leftMonitor = new ScratchAxisProcessor();
        private readonly ScratchAxisProcessor rightMonitor = new ScratchAxisProcessor();
        private readonly Dictionary<(string guid, int axis), float> bindSampleLast = new Dictionary<(string, int), float>();

        private Bindable<bool> enabled = null!;
        private Bindable<string> leftBinding = null!;
        private Bindable<string> rightBinding = null!;
        private Bindable<double> deadzone = null!;
        private Bindable<int> stopThreshold = null!;

        private ScratchAxisDeviceTracker tracker = null!;

        private SettingsButtonV2 leftBindButton = null!;
        private SettingsButtonV2 rightBindButton = null!;
        private OsuSpriteText leftStatusText = null!;
        private OsuSpriteText rightStatusText = null!;

        private BindTarget? bindTarget;
        private (string guid, int axis, string name, float score)? bestBindCandidate;

        public EzManiaScratchAxisSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, ScratchAxisDeviceTracker scratchTracker)
        {
            tracker = scratchTracker;

            enabled = ezConfig.GetBindable<bool>(Ez2Setting.ManiaScratchAxisEnabled);
            leftBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisL);
            rightBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisR);
            deadzone = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisDeadzone);
            stopThreshold = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisStopThreshold);

            leftMonitor.Deadzone.BindTo(deadzone);
            rightMonitor.Deadzone.BindTo(deadzone);
            leftMonitor.StopThreshold.BindTo(stopThreshold);
            rightMonitor.StopThreshold.BindTo(stopThreshold);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.MANIA_SCRATCH_AXIS_ENABLED,
                    HintText = EzSettingsStrings.MANIA_SCRATCH_AXIS_ENABLED_TOOLTIP,
                    Current = enabled,
                })
                {
                    Keywords = new[] { "ez", "mania", "scratch", "turntable", "axis", "joystick", "转盘" }
                },
                leftBindButton = new SettingsButtonV2
                {
                    Keywords = new[] { "ez", "mania", "scratch", "l", "axis" },
                    Action = () => beginBind(BindTarget.Left),
                },
                createStatusRow(out leftStatusText),
                rightBindButton = new SettingsButtonV2
                {
                    Keywords = new[] { "ez", "mania", "scratch", "r", "axis" },
                    Action = () => beginBind(BindTarget.Right),
                },
                createStatusRow(out rightStatusText),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.SCRATCH_AXIS_DEADZONE,
                    HintText = EzSettingsStrings.SCRATCH_AXIS_DEADZONE_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current = deadzone,
                    KeyboardStep = 0.005f,
                })
                {
                    Keywords = new[] { "ez", "scratch", "deadzone", "jitter", "死区" }
                },
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = EzSettingsStrings.SCRATCH_AXIS_STOP_THRESHOLD,
                    HintText = EzSettingsStrings.SCRATCH_AXIS_STOP_THRESHOLD_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current = stopThreshold,
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "ez", "scratch", "stop", "threshold" }
                },
            };

            leftBinding.BindValueChanged(_ => refreshBindLabels(), true);
            rightBinding.BindValueChanged(_ => refreshBindLabels(), true);

            leftMonitor.IsPressed.BindValueChanged(_ => refreshStatus(leftMonitor, leftStatusText), true);
            leftMonitor.Direction.BindValueChanged(_ => refreshStatus(leftMonitor, leftStatusText));
            rightMonitor.IsPressed.BindValueChanged(_ => refreshStatus(rightMonitor, rightStatusText), true);
            rightMonitor.Direction.BindValueChanged(_ => refreshStatus(rightMonitor, rightStatusText));

            tracker.AxisMoved += onAxisMoved;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (tracker != null)
                tracker.AxisMoved -= onAxisMoved;

            base.Dispose(isDisposing);
        }

        public override bool AcceptsFocus => bindTarget != null;

        protected override void OnFocusLost(FocusLostEvent e)
        {
            commitBindIfPossible();
            endBind();
            base.OnFocusLost(e);
        }

        protected override void Update()
        {
            base.Update();

            double t = Time.Current;

            if (tracker.TryGetValue(decorate(leftBinding.Value), out float leftValue))
                leftMonitor.Update(leftValue, t);
            else
                leftMonitor.UpdateMissing(t);

            if (tracker.TryGetValue(decorate(rightBinding.Value), out float rightValue))
                rightMonitor.Update(rightValue, t);
            else
                rightMonitor.UpdateMissing(t);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (bindTarget != null && !leftBindButton.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)
                                   && !rightBindButton.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
            {
                commitBindIfPossible();
                endBind();
                return true;
            }

            return base.OnClick(e);
        }

        private void onAxisMoved(JoystickDeviceAxis axis)
        {
            if (string.IsNullOrEmpty(axis.Guid))
                return;

            var key = (axis.Guid, axis.AxisIndex);
            float last = bindSampleLast.GetValueOrDefault(key, axis.Value);
            float delta = Math.Abs(ScratchAxisProcessor.ShortestDelta(last, axis.Value));
            bindSampleLast[key] = axis.Value;

            float threshold = (float)Math.Max(0.02, deadzone.Value);

            if (bindTarget == null)
                return;

            if (delta < threshold)
                return;

            if (bestBindCandidate == null || delta > bestBindCandidate.Value.score)
                bestBindCandidate = (axis.Guid, axis.AxisIndex, axis.Name, delta);

            // 累计足够位移后立即确认，避免必须失焦才生效
            if (bestBindCandidate.Value.score >= threshold * 2)
            {
                commitBindIfPossible();
                endBind();
            }
        }

        private void beginBind(BindTarget target)
        {
            bindTarget = target;
            bestBindCandidate = null;
            bindSampleLast.Clear();
            refreshBindLabels();
            GetContainingFocusManager()?.ChangeFocus(this);
        }

        private void commitBindIfPossible()
        {
            if (bindTarget == null || bestBindCandidate == null)
                return;

            var binding = new ScratchAxisBinding(bestBindCandidate.Value.guid, bestBindCandidate.Value.axis, bestBindCandidate.Value.name);

            if (bindTarget == BindTarget.Left)
                leftBinding.Value = binding.ToString();
            else
                rightBinding.Value = binding.ToString();
        }

        private void endBind()
        {
            bindTarget = null;
            bestBindCandidate = null;
            refreshBindLabels();
        }

        private void refreshBindLabels()
        {
            leftBindButton.Text = formatBindLabel("L-Scratch", decorate(leftBinding.Value), bindTarget == BindTarget.Left);
            rightBindButton.Text = formatBindLabel("R-Scratch", decorate(rightBinding.Value), bindTarget == BindTarget.Right);
        }

        private ScratchAxisBinding decorate(string stored)
        {
            var binding = ScratchAxisBinding.Parse(stored);
            if (binding.IsEmpty || !string.IsNullOrEmpty(binding.DeviceName))
                return binding;

            string? name = tracker.GetDeviceName(binding.DeviceGuid);
            return string.IsNullOrEmpty(name)
                ? binding
                : new ScratchAxisBinding(binding.DeviceGuid, binding.AxisIndex, name);
        }

        private static LocalisableString formatBindLabel(string caption, ScratchAxisBinding binding, bool waiting)
        {
            if (waiting)
                return $"{caption}: [{EzSettingsStrings.SCRATCH_AXIS_BIND_HINT}]";

            return $"{caption}: {binding.ToDisplayString()}";
        }

        private static void refreshStatus(ScratchAxisProcessor processor, OsuSpriteText text)
        {
            if (!processor.IsPressed.Value || processor.Direction.Value == ScratchAxisDirection.None)
            {
                text.Text = EzSettingsStrings.SCRATCH_AXIS_STATUS_IDLE;
                return;
            }

            text.Text = processor.Direction.Value == ScratchAxisDirection.Clockwise
                ? EzSettingsStrings.SCRATCH_AXIS_STATUS_CW
                : EzSettingsStrings.SCRATCH_AXIS_STATUS_CCW;
        }

        private static Drawable createStatusRow(out OsuSpriteText statusText)
        {
            statusText = new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 14),
                Text = EzSettingsStrings.SCRATCH_AXIS_STATUS_IDLE,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = statusText,
            };
        }

        private enum BindTarget
        {
            Left,
            Right,
        }
    }
}
