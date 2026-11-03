// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Handlers.Joystick;
using osu.Framework.Input.States;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 订阅 <see cref="JoystickHandler.DeviceAxisChanged"/>，维护多设备轴值表，供转盘绑定与 Mania/Catch 游玩读取。
    /// </summary>
    [Cached]
    public partial class ScratchAxisDeviceTracker : Component
    {
        private readonly ConcurrentDictionary<(string guid, int axis), float> valuesByGuid =
            new ConcurrentDictionary<(string, int), float>();

        private readonly ConcurrentDictionary<string, string> namesByGuid =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 任意设备轴发生变化时（已调度到更新线程）。
        /// </summary>
        public event Action<JoystickDeviceAxis>? AxisMoved;

        private JoystickHandler? joystickHandler;
        private Scheduler? scheduler;

        private Bindable<string> scratchAxisL = null!;
        private Bindable<string> scratchAxisR = null!;

        [BackgroundDependencyLoader]
        private void load(GameHost host, Ez2ConfigManager config)
        {
            scheduler = host.UpdateThread.Scheduler;
            joystickHandler = host.AvailableInputHandlers.OfType<JoystickHandler>().FirstOrDefault();

            if (joystickHandler != null)
                joystickHandler.DeviceAxisChanged += onDeviceAxisChanged;

            scratchAxisL = config.GetBindable<string>(Ez2Setting.ScratchAxisL);
            scratchAxisR = config.GetBindable<string>(Ez2Setting.ScratchAxisR);

            scratchAxisL.BindValueChanged(_ => updateContinuousAxes());
            scratchAxisR.BindValueChanged(_ => updateContinuousAxes(), true);
        }

        /// <summary>
        /// 告知框架哪些轴是转盘：转盘停在何处便留在何处，框架据轴值合成的方向按键会一直处于按下态，
        /// 从而让所有 Exact 匹配的键位组合整局失效。
        /// </summary>
        private void updateContinuousAxes()
        {
            joystickHandler?.SetContinuousAxes(new[] { scratchAxisL.Value, scratchAxisR.Value }
                                               .Select(ScratchAxisBinding.Parse)
                                               .Where(b => !b.IsEmpty && b.AxisIndex >= 0 && b.AxisIndex < JoystickState.MAX_AXES)
                                               .Select(b => (JoystickAxisSource)b.AxisIndex));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (joystickHandler != null)
                joystickHandler.DeviceAxisChanged -= onDeviceAxisChanged;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// 读取绑定对应的当前轴值。尚无该设备轴采样时返回 false（不要用 0 冒充）。
        /// </summary>
        public bool TryGetValue(ScratchAxisBinding binding, out float value)
        {
            value = 0;

            if (binding.IsEmpty || string.IsNullOrEmpty(binding.DeviceGuid))
                return false;

            return valuesByGuid.TryGetValue((binding.DeviceGuid, binding.AxisIndex), out value);
        }

        /// <summary>
        /// 仅在已有采样时返回轴值；否则返回 0（不应用于转动检测）。
        /// </summary>
        public float GetValue(ScratchAxisBinding binding) =>
            TryGetValue(binding, out float value) ? value : 0;

        public string? GetDeviceName(string guid) =>
            namesByGuid.GetValueOrDefault(guid);

        private void onDeviceAxisChanged(JoystickDeviceAxis axis)
        {
            string deviceKey = !string.IsNullOrEmpty(axis.Guid)
                ? axis.Guid
                : $"id:{axis.InstanceId}";

            valuesByGuid[(deviceKey, axis.AxisIndex)] = axis.Value;
            namesByGuid[deviceKey] = string.IsNullOrEmpty(axis.Name) ? deviceKey : axis.Name;

            var captured = axis;
            scheduler?.Add(() => AxisMoved?.Invoke(captured), false);
        }
    }
}
