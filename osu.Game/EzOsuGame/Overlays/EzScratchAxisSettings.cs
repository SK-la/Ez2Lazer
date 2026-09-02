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
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Input;
using CommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// L/R 转盘轴设置：按设备 GUID+轴绑定，支持 Mania 与 Catch。
    /// </summary>
    public partial class EzScratchAxisSettings : FillFlowContainer
    {
        /// <summary>绑定捕获用的累计位移阈值（与游玩死区独立，避免绑不上）。</summary>
        private const float bind_travel_threshold = 0.03f;

        private readonly ScratchAxisProcessor leftMonitor = new ScratchAxisProcessor();
        private readonly ScratchAxisProcessor rightMonitor = new ScratchAxisProcessor();
        private readonly Dictionary<(string device, int axis), float> bindLastValue = new Dictionary<(string, int), float>();
        private readonly Dictionary<(string device, int axis), float> bindTravel = new Dictionary<(string, int), float>();

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
        private OsuSpriteText bindHintText = null!;
        private FillFlowContainer cancelAndClearButtons = null!;

        private BindTarget? bindTarget;
        private (string device, int axis, string name, float travel)? bestBindCandidate;

        public EzScratchAxisSettings()
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

            enabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisEnabled);
            leftBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisL);
            rightBinding = ezConfig.GetBindable<string>(Ez2Setting.ScratchAxisR);
            deadzone = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisDeadzone);
            stopThreshold = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisStopThreshold);

            leftMonitor.Deadzone.BindTo(deadzone);
            rightMonitor.Deadzone.BindTo(deadzone);
            leftMonitor.StopThreshold.BindTo(stopThreshold);
            rightMonitor.StopThreshold.BindTo(stopThreshold);

            Children = new[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SCRATCH_AXIS_ENABLED,
                    HintText = EzSettingsStrings.SCRATCH_AXIS_ENABLED_TOOLTIP,
                    Current = enabled,
                })
                {
                    Keywords = new[] { "ez", "mania", "catch", "scratch", "turntable", "axis", "joystick", "转盘" }
                },
                leftBindButton = new SettingsButtonV2
                {
                    Keywords = new[] { "ez", "scratch", "l", "axis" },
                    Action = () => toggleBind(BindTarget.Left),
                },
                createStatusRow(out leftStatusText),
                rightBindButton = new SettingsButtonV2
                {
                    Keywords = new[] { "ez", "scratch", "r", "axis" },
                    Action = () => toggleBind(BindTarget.Right),
                },
                createStatusRow(out rightStatusText),
                createHintRow(out bindHintText),
                cancelAndClearButtons = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Spacing = new Vector2(5),
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new RoundedButton
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Text = CommonStrings.ButtonsCancel,
                            Size = new Vector2(120, 30),
                            Action = cancelBind,
                        },
                        new DangerousRoundedButton
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Text = InputSettingsStrings.ClearBindingButton,
                            Size = new Vector2(120, 30),
                            Action = clearBinding,
                        },
                    },
                },
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
            refreshBindHint();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
                tracker.AxisMoved -= onAxisMoved;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (bindTarget != null && e.Key == Key.Escape)
            {
                cancelBind();
                return true;
            }

            return base.OnKeyDown(e);
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

        private void onAxisMoved(JoystickDeviceAxis axis)
        {
            string deviceKey = !string.IsNullOrEmpty(axis.Guid) ? axis.Guid : $"id:{axis.InstanceId}";
            var key = (deviceKey, axis.AxisIndex);

            if (bindTarget == null)
                return;

            if (!bindLastValue.TryGetValue(key, out float last))
            {
                bindLastValue[key] = axis.Value;
                bindTravel[key] = 0;
                refreshBindHint();
                return;
            }

            float delta = Math.Abs(ScratchAxisProcessor.ShortestDelta(last, axis.Value));
            bindLastValue[key] = axis.Value;

            if (delta < 0.001f)
                return;

            float travel = bindTravel.GetValueOrDefault(key) + delta;
            bindTravel[key] = travel;

            if (bestBindCandidate == null || travel > bestBindCandidate.Value.travel)
                bestBindCandidate = (deviceKey, axis.AxisIndex, axis.Name, travel);

            refreshBindHint();

            if (travel >= bind_travel_threshold)
                commitBindIfPossible();
        }

        private void toggleBind(BindTarget target)
        {
            if (bindTarget == target)
            {
                cancelBind();
                return;
            }

            beginBind(target);
        }

        private void beginBind(BindTarget target)
        {
            bindTarget = target;
            bestBindCandidate = null;
            bindLastValue.Clear();
            bindTravel.Clear();
            refreshBindLabels();
            refreshBindHint();
            showClearButtons(true);
        }

        private void cancelBind()
        {
            bindTarget = null;
            bestBindCandidate = null;
            bindLastValue.Clear();
            bindTravel.Clear();
            refreshBindLabels();
            refreshBindHint();
            showClearButtons(false);
        }

        private void clearBinding()
        {
            if (bindTarget == null)
                return;

            if (bindTarget == BindTarget.Left)
                leftBinding.Value = string.Empty;
            else
                rightBinding.Value = string.Empty;

            endBind();
        }

        private void commitBindIfPossible()
        {
            if (bindTarget == null || bestBindCandidate == null)
                return;

            var binding = new ScratchAxisBinding(
                bestBindCandidate.Value.device,
                bestBindCandidate.Value.axis,
                bestBindCandidate.Value.name);

            if (bindTarget == BindTarget.Left)
                leftBinding.Value = binding.ToString();
            else
                rightBinding.Value = binding.ToString();
        }

        private void endBind()
        {
            bindTarget = null;
            bestBindCandidate = null;
            bindLastValue.Clear();
            bindTravel.Clear();
            refreshBindLabels();
            refreshBindHint();
            showClearButtons(false);
        }

        private void showClearButtons(bool show)
        {
            if (show)
                cancelAndClearButtons.FadeIn(200, Easing.OutQuint);
            else
                cancelAndClearButtons.FadeOut(200, Easing.OutQuint);
        }

        private void refreshBindLabels()
        {
            leftBindButton.Text = formatBindLabel("L-Scratch", decorate(leftBinding.Value), bindTarget == BindTarget.Left);
            rightBindButton.Text = formatBindLabel("R-Scratch", decorate(rightBinding.Value), bindTarget == BindTarget.Right);
        }

        private void refreshBindHint()
        {
            if (bindTarget == null)
            {
                bindHintText.Text = EzSettingsStrings.SCRATCH_AXIS_BIND_IDLE_HINT;
                return;
            }

            float travel = bestBindCandidate?.travel ?? 0;
            bindHintText.Text = $"Listening… travel {travel:0.###} / {bind_travel_threshold:0.###}  |  Move {travel:0.###} / {bind_travel_threshold:0.###}";
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

        private static Drawable createHintRow(out OsuSpriteText hintText)
        {
            hintText = new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 12),
                Colour = Colour4.Gray,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = hintText,
            };
        }

        private enum BindTarget
        {
            Left,
            Right,
        }
    }
}
