// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.EzOsuGame.Camera
{
    public enum EzCameraSourceKind
    {
        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_SOURCE_PHYSICAL))]
        Physical,

        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_SOURCE_VIRTUAL))]
        Virtual,
    }

    public enum EzCameraFrameRate
    {
        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_FPS_15))]
        Fps15 = 15,

        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_FPS_24))]
        Fps24 = 24,

        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_FPS_30))]
        Fps30 = 30,

        [LocalisableDescription(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_FPS_60))]
        Fps60 = 60,
    }

    /// <summary>
    /// A local capture device. <see cref="Id"/> is the value stored on the HUD component.
    /// </summary>
    public sealed class EzCameraDevice
    {
        public EzCameraDevice(string id, string name, EzCameraSourceKind kind)
        {
            Id = id;
            Name = name;
            Kind = kind;
        }

        public string Id { get; }

        public string Name { get; }

        public EzCameraSourceKind Kind { get; }
    }

    /// <summary>
    /// Which manual controls the open device actually exposes. Sliders stay editable until <see cref="Ready"/> is set.
    /// </summary>
    public sealed class EzCameraCapabilities
    {
        public static EzCameraCapabilities Unknown { get; } = new EzCameraCapabilities(false, false, false, false, false, false, false);

        public EzCameraCapabilities(bool ready, bool manualFocus, bool autoFocus, bool manualExposure, bool autoExposure, bool manualWhiteBalance, bool autoWhiteBalance)
        {
            Ready = ready;
            ManualFocus = manualFocus;
            AutoFocus = autoFocus;
            ManualExposure = manualExposure;
            AutoExposure = autoExposure;
            ManualWhiteBalance = manualWhiteBalance;
            AutoWhiteBalance = autoWhiteBalance;
        }

        public bool Ready { get; }

        public bool ManualFocus { get; }

        public bool AutoFocus { get; }

        public bool ManualExposure { get; }

        public bool AutoExposure { get; }

        public bool ManualWhiteBalance { get; }

        public bool AutoWhiteBalance { get; }
    }

    public readonly struct EzCameraControls
    {
        public EzCameraControls(bool autoFocus, float focus, bool autoExposure, float exposure, bool autoWhiteBalance, float whiteBalance)
        {
            AutoFocus = autoFocus;
            Focus = focus;
            AutoExposure = autoExposure;
            Exposure = exposure;
            AutoWhiteBalance = autoWhiteBalance;
            WhiteBalance = whiteBalance;
        }

        public bool AutoFocus { get; }

        /// <summary>0–1 across the device's manual range.</summary>
        public float Focus { get; }

        public bool AutoExposure { get; }

        /// <summary>0–1 across the device's manual range.</summary>
        public float Exposure { get; }

        public bool AutoWhiteBalance { get; }

        /// <summary>0–1 across the device's kelvin range.</summary>
        public float WhiteBalance { get; }
    }

    public sealed class EzCameraOpenRequest
    {
        public EzCameraSourceKind SourceKind { get; init; }

        public string DeviceId { get; init; } = string.Empty;

        public EzCameraFrameRate FrameRate { get; init; } = EzCameraFrameRate.Fps30;

        public EzCameraControls Controls { get; init; }
    }

    /// <summary>
    /// One open local camera. Frames are latest-only; callers copy on the draw thread.
    /// </summary>
    public interface IEzCameraSession : IDisposable
    {
        string? Error { get; }

        EzCameraCapabilities Capabilities { get; }

        /// <summary>
        /// Copies the newest frame when <paramref name="sequence"/> is behind.
        /// Returns false without advancing <paramref name="sequence"/> when there is no new frame,
        /// or when <paramref name="destination"/> is shorter than <paramref name="width"/> × <paramref name="height"/>.
        /// </summary>
        bool TryCopyLatest(Span<Rgba32> destination, ref long sequence, out int width, out int height);

        void UpdateControls(EzCameraControls controls);
    }

    /// <summary>
    /// Platform capture entry point. Constructing an implementation must not open a device.
    /// </summary>
    public interface IEzCameraBackend : IDisposable
    {
        Task<IReadOnlyList<EzCameraDevice>> ListDevicesAsync();

        IEzCameraSession Open(EzCameraOpenRequest request);
    }
}
