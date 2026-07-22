// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Handlers.Joystick;
using osu.Framework.Platform;
using osu.Framework.Threading;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 订阅 <see cref="JoystickHandler.DeviceAxisChanged"/>，维护多设备轴值表，供转盘绑定与游玩读取。
    /// </summary>
    /// <remarks>
    /// TODO(Catch)：可由此读取绑定轴的 Direction 做左右移动。
    /// </remarks>
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

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            scheduler = host.UpdateThread.Scheduler;
            joystickHandler = host.AvailableInputHandlers.OfType<JoystickHandler>().FirstOrDefault();

            if (joystickHandler != null)
                joystickHandler.DeviceAxisChanged += onDeviceAxisChanged;
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
            namesByGuid.TryGetValue(guid, out string? name) ? name : null;

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
