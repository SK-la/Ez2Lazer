// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.States;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// L/R 双转盘状态对，从 <see cref="JoystickState"/> 按配置轴索引取样。
    /// </summary>
    public class ScratchAxisPair
    {
        public ScratchAxisProcessor Left { get; } = new ScratchAxisProcessor();
        public ScratchAxisProcessor Right { get; } = new ScratchAxisProcessor();

        public BindableInt LeftAxisIndex { get; } = new BindableInt((int)JoystickAxisSource.GamePadLeftStickX)
        {
            MinValue = 0,
            MaxValue = (int)JoystickAxisSource.Axis16,
        };

        public BindableInt RightAxisIndex { get; } = new BindableInt((int)JoystickAxisSource.GamePadLeftStickY)
        {
            MinValue = 0,
            MaxValue = (int)JoystickAxisSource.Axis16,
        };

        /// <summary>
        /// 将死区/停转阈值同步到左右处理器。
        /// </summary>
        public void BindTuning(Bindable<double> deadzone, Bindable<int> stopThreshold)
        {
            Left.Deadzone.BindTo(deadzone);
            Right.Deadzone.BindTo(deadzone);
            Left.StopThreshold.BindTo(stopThreshold);
            Right.StopThreshold.BindTo(stopThreshold);
        }

        public void UpdateFrom(JoystickState joystick)
        {
            Left.Update(readAxis(joystick, LeftAxisIndex.Value));
            Right.Update(readAxis(joystick, RightAxisIndex.Value));
        }

        public void Reset()
        {
            Left.Reset();
            Right.Reset();
        }

        private static float readAxis(JoystickState joystick, int index)
        {
            if (index < 0 || index >= joystick.AxesValues.Length)
                return 0;

            return joystick.AxesValues[index];
        }
    }
}
