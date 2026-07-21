// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// Mania L/R 转盘轴设置：开关、轴绑定、死区、实时顺逆状态。
    /// </summary>
    public partial class EzManiaScratchAxisSettings : FillFlowContainer
    {
        private readonly ScratchAxisProcessor leftMonitor = new ScratchAxisProcessor();
        private readonly ScratchAxisProcessor rightMonitor = new ScratchAxisProcessor();

        private Bindable<bool> enabled = null!;
        private Bindable<int> leftAxis = null!;
        private Bindable<int> rightAxis = null!;
        private Bindable<double> deadzone = null!;
        private Bindable<int> stopThreshold = null!;

        private SettingsButtonV2 leftBindButton = null!;
        private SettingsButtonV2 rightBindButton = null!;
        private OsuSpriteText leftStatusText = null!;
        private OsuSpriteText rightStatusText = null!;

        private BindTarget? bindTarget;

        public EzManiaScratchAxisSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            enabled = ezConfig.GetBindable<bool>(Ez2Setting.ManiaScratchAxisEnabled);
            leftAxis = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisL);
            rightAxis = ezConfig.GetBindable<int>(Ez2Setting.ScratchAxisR);
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

            leftAxis.BindValueChanged(_ => refreshBindLabels(), true);
            rightAxis.BindValueChanged(_ => refreshBindLabels(), true);

            leftMonitor.IsPressed.BindValueChanged(_ => refreshStatus(leftMonitor, leftStatusText), true);
            leftMonitor.Direction.BindValueChanged(_ => refreshStatus(leftMonitor, leftStatusText));
            rightMonitor.IsPressed.BindValueChanged(_ => refreshStatus(rightMonitor, rightStatusText), true);
            rightMonitor.Direction.BindValueChanged(_ => refreshStatus(rightMonitor, rightStatusText));
        }

        public override bool AcceptsFocus => bindTarget != null;

        protected override void OnFocusLost(FocusLostEvent e)
        {
            endBind();
            base.OnFocusLost(e);
        }

        protected override void Update()
        {
            base.Update();

            var joystick = GetContainingInputManager()?.CurrentState.Joystick;
            if (joystick == null)
                return;

            leftMonitor.Update(readAxis(joystick.AxesValues, leftAxis.Value));
            rightMonitor.Update(readAxis(joystick.AxesValues, rightAxis.Value));
        }

        protected override bool OnJoystickAxisMove(JoystickAxisMoveEvent e)
        {
            if (bindTarget == null)
                return false;

            if (Math.Abs(e.Delta) < Math.Max(0.05, deadzone.Value))
                return false;

            int axisIndex = (int)e.Axis.Source;
            if (bindTarget == BindTarget.Left)
                leftAxis.Value = axisIndex;
            else
                rightAxis.Value = axisIndex;

            endBind();
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            // 点击空白处取消绑定等待
            if (bindTarget != null && !leftBindButton.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)
                                   && !rightBindButton.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
            {
                endBind();
                return true;
            }

            return base.OnClick(e);
        }

        private void beginBind(BindTarget target)
        {
            bindTarget = target;
            refreshBindLabels();
            GetContainingFocusManager()?.ChangeFocus(this);
        }

        private void endBind()
        {
            bindTarget = null;
            refreshBindLabels();
        }

        private void refreshBindLabels()
        {
            leftBindButton.Text = formatBindLabel("L-Scratch", leftAxis.Value, bindTarget == BindTarget.Left);
            rightBindButton.Text = formatBindLabel("R-Scratch", rightAxis.Value, bindTarget == BindTarget.Right);
        }

        private static LocalisableString formatBindLabel(string caption, int axisIndex, bool waiting)
        {
            string axisName = ((JoystickAxisSource)axisIndex).ToString();

            if (waiting)
                return $"{caption}: [{EzSettingsStrings.SCRATCH_AXIS_BIND_HINT}]";

            return $"{caption}: {axisName}";
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

        private static float readAxis(float[] values, int index)
        {
            if (index < 0 || index >= values.Length)
                return 0;

            return values[index];
        }

        private enum BindTarget
        {
            Left,
            Right,
        }
    }
}
