// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Input
{
    /// <summary>
    /// 转盘轴绑定：设备 GUID + 该设备上的轴下标（支持多设备 / 同设备多轴）。
    /// 序列化格式：<c>guid|axisIndex</c>；兼容旧版纯数字轴下标。
    /// </summary>
    public readonly struct ScratchAxisBinding : IEquatable<ScratchAxisBinding>
    {
        public string DeviceGuid { get; }
        public int AxisIndex { get; }
        public string DeviceName { get; }

        public bool IsEmpty => string.IsNullOrEmpty(DeviceGuid) && AxisIndex < 0;

        public ScratchAxisBinding(string deviceGuid, int axisIndex, string deviceName = "")
        {
            DeviceGuid = deviceGuid;
            AxisIndex = axisIndex;
            DeviceName = deviceName;
        }

        public static ScratchAxisBinding Empty => new ScratchAxisBinding(string.Empty, -1);

        public static ScratchAxisBinding Parse(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return Empty;

            // legacy: plain axis index
            if (int.TryParse(stored, out int legacyAxis))
                return new ScratchAxisBinding(string.Empty, legacyAxis);

            int sep = stored.LastIndexOf('|');
            if (sep <= 0 || sep >= stored.Length - 1)
                return Empty;

            string guid = stored[..sep];
            if (!int.TryParse(stored[(sep + 1)..], out int axis))
                return Empty;

            return new ScratchAxisBinding(guid, axis);
        }

        public override string ToString()
        {
            if (IsEmpty)
                return string.Empty;

            if (string.IsNullOrEmpty(DeviceGuid))
                return AxisIndex.ToString();

            return $"{DeviceGuid}|{AxisIndex}";
        }

        public string ToDisplayString()
        {
            if (IsEmpty)
                return "(none)";

            string name = string.IsNullOrEmpty(DeviceName) ? DeviceGuid : DeviceName;
            if (string.IsNullOrEmpty(name))
                return $"Axis {AxisIndex}";

            // GUID 过长时截断显示
            if (name.Length > 24 && string.IsNullOrEmpty(DeviceName))
                name = name[..8] + "…" + name[^4..];

            return $"{name} / Axis {AxisIndex}";
        }

        public bool Equals(ScratchAxisBinding other) =>
            string.Equals(DeviceGuid, other.DeviceGuid, StringComparison.OrdinalIgnoreCase)
            && AxisIndex == other.AxisIndex;

        public override bool Equals(object? obj) => obj is ScratchAxisBinding other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(DeviceGuid.ToLowerInvariant(), AxisIndex);
    }
}
