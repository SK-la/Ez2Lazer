// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WinRT;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.EzOsuGame.Camera;
using SixLabors.ImageSharp.PixelFormats;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;

namespace osu.Game.EzCamera.Windows
{
    /// <summary>
    /// Local physical and virtual cameras via MediaCapture. No network sources.
    /// </summary>
    [SupportedOSPlatform("windows10.0.19041.0")]
    public sealed class WindowsEzCameraService : IEzCameraBackend
    {
        private static readonly string[] virtual_markers =
        {
            "virtual", "obs", "ndi", "manycam", "splitcam", "xsplit", "droidcam", "iriun",
        };

        public async Task<IReadOnlyList<EzCameraDevice>> ListDevicesAsync()
        {
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var list = new List<EzCameraDevice>(devices.Count);

            foreach (DeviceInformation device in devices)
                list.Add(new EzCameraDevice(device.Id, device.Name, Classify(device)));

            return list;
        }

        public IEzCameraSession Open(EzCameraOpenRequest request) => new WindowsEzCameraSession(request);

        public void Dispose()
        {
        }

        internal static EzCameraSourceKind Classify(DeviceInformation device)
        {
            string name = device.Name ?? string.Empty;

            foreach (string marker in virtual_markers)
            {
                if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return EzCameraSourceKind.Virtual;
            }

            // EnclosureLocation only reinforces a physical camera. USB webcams often omit it, so null is not "virtual".
            return EzCameraSourceKind.Physical;
        }
    }

    [SupportedOSPlatform("windows10.0.19041.0")]
    internal sealed class WindowsEzCameraSession : IEzCameraSession
    {
        private const int preferred_max_width = 1280;
        private const int preferred_max_height = 720;
        private const int absolute_max_width = 1920;
        private const int absolute_max_height = 1080;

        private readonly object publishGate = new object();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SemaphoreSlim controlLock = new SemaphoreSlim(1, 1);

        private MediaCapture? capture;
        private MediaFrameReader? reader;
        private EzCameraControls latestControls;
        private int controlsDirty;
        private int started;
        private int disposed;

        private Rgba32[]? published;
        private Rgba32[]? backBuffer;
        private int publishedWidth;
        private int publishedHeight;
        private long publishedSequence;

        private uint focusMin;
        private uint focusMax = 1;
        private uint focusStep = 1;
        private bool focusManual;
        private bool focusAuto;
        private TimeSpan exposureMin;
        private TimeSpan exposureMax;
        private TimeSpan exposureStep;
        private bool exposureManual;
        private bool exposureAuto;
        private uint whiteBalanceMin;
        private uint whiteBalanceMax = 1;
        private uint whiteBalanceStep = 1;
        private bool whiteBalanceManual;
        private bool whiteBalanceAuto;

        public WindowsEzCameraSession(EzCameraOpenRequest request)
        {
            latestControls = request.Controls;
            _ = Task.Run(() => start(request));
        }

        public string? Error { get; private set; }

        public EzCameraCapabilities Capabilities { get; private set; } = EzCameraCapabilities.Unknown;

        public bool TryCopyLatest(Span<Rgba32> destination, ref long sequence, out int width, out int height)
        {
            lock (publishGate)
            {
                if (published == null || publishedSequence == sequence)
                {
                    width = 0;
                    height = 0;
                    return false;
                }

                width = publishedWidth;
                height = publishedHeight;
                int count = width * height;

                if (destination.Length < count)
                    return false;

                published.AsSpan(0, count).CopyTo(destination);
                sequence = publishedSequence;
                return true;
            }
        }

        public void UpdateControls(EzCameraControls controls)
        {
            latestControls = controls;
            Volatile.Write(ref controlsDirty, 1);

            if (Volatile.Read(ref started) == 1)
                _ = applyUntilQuiet();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 1)
                return;

            cts.Cancel();
            _ = Task.Run(releaseAsync);
        }

        private async Task start(EzCameraOpenRequest request)
        {
            try
            {
                string deviceId = await resolveDeviceId(request).ConfigureAwait(false);

                if (cts.IsCancellationRequested || deviceId.Length == 0)
                {
                    Error ??= "没有可用的摄像头。";
                    return;
                }

                var settings = new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = deviceId,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                };

                var localCapture = new MediaCapture();
                await localCapture.InitializeAsync(settings).AsTask(cts.Token).ConfigureAwait(false);

                if (cts.IsCancellationRequested)
                {
                    localCapture.Dispose();
                    return;
                }

                capture = localCapture;
                readCapabilities(localCapture);

                MediaFrameSource? source = null;

                foreach (MediaFrameSource candidate in localCapture.FrameSources.Values)
                {
                    if (candidate.Info.SourceKind == MediaFrameSourceKind.Color)
                    {
                        source = candidate;
                        break;
                    }
                }

                source ??= localCapture.FrameSources.Values.FirstOrDefault();

                if (source == null)
                {
                    Error = "摄像头没有视频源。";
                    return;
                }

                MediaFrameFormat? format = chooseFormat(source, (int)request.FrameRate);

                if (format != null)
                    await source.SetFormatAsync(format).AsTask(cts.Token).ConfigureAwait(false);

                MediaFrameReader localReader;

                try
                {
                    localReader = await localCapture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8).AsTask(cts.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    localReader = await localCapture.CreateFrameReaderAsync(source).AsTask(cts.Token).ConfigureAwait(false);
                }

                if (cts.IsCancellationRequested)
                {
                    localReader.Dispose();
                    return;
                }

                localReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                localReader.FrameArrived += onFrame;
                reader = localReader;

                await localReader.StartAsync().AsTask(cts.Token).ConfigureAwait(false);
                Volatile.Write(ref started, 1);
                Volatile.Write(ref controlsDirty, 1);
                await applyUntilQuiet().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                Error = "没有摄像头权限，或设备正被占用。";
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
        }

        private async Task<string> resolveDeviceId(EzCameraOpenRequest request)
        {
            if (!string.IsNullOrEmpty(request.DeviceId))
                return request.DeviceId;

            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(cts.Token).ConfigureAwait(false);

            foreach (DeviceInformation device in devices)
            {
                if (WindowsEzCameraService.Classify(device) == request.SourceKind)
                    return device.Id;
            }

            return devices.Count > 0 ? devices[0].Id : string.Empty;
        }

        private void readCapabilities(MediaCapture localCapture)
        {
            VideoDeviceController controller = localCapture.VideoDeviceController;

            try
            {
                FocusControl focus = controller.FocusControl;
                focusAuto = focus.Supported && focus.SupportedPresets.Contains(FocusPreset.Auto);
                focusManual = focus.Supported && focus.Max > focus.Min;
                focusMin = focus.Min;
                focusMax = focus.Max;
                focusStep = Math.Max(1u, focus.Step);
            }
            catch (Exception)
            {
                focusAuto = false;
                focusManual = false;
            }

            try
            {
                ExposureControl exposure = controller.ExposureControl;
                exposureAuto = exposure.Supported;
                exposureManual = exposure.Supported && exposure.Max > exposure.Min;
                exposureMin = exposure.Min;
                exposureMax = exposure.Max;
                exposureStep = exposure.Step;
            }
            catch (Exception)
            {
                exposureAuto = false;
                exposureManual = false;
            }

            try
            {
                WhiteBalanceControl whiteBalance = controller.WhiteBalanceControl;
                whiteBalanceAuto = whiteBalance.Supported;
                whiteBalanceManual = whiteBalance.Supported && whiteBalance.Max > whiteBalance.Min;
                whiteBalanceMin = whiteBalance.Min;
                whiteBalanceMax = whiteBalance.Max;
                whiteBalanceStep = Math.Max(1u, whiteBalance.Step);
            }
            catch (Exception)
            {
                whiteBalanceAuto = false;
                whiteBalanceManual = false;
            }

            Capabilities = new EzCameraCapabilities(true, focusManual, focusAuto, exposureManual, exposureAuto, whiteBalanceManual, whiteBalanceAuto);
        }

        private async Task applyUntilQuiet()
        {
            if (!await controlLock.WaitAsync(0).ConfigureAwait(false))
                return;

            try
            {
                MediaCapture? localCapture = capture;

                if (localCapture == null)
                    return;

                while (Volatile.Read(ref disposed) == 0)
                {
                    Volatile.Write(ref controlsDirty, 0);
                    EzCameraControls controls = latestControls;
                    await applyOnce(localCapture, controls).ConfigureAwait(false);

                    if (Volatile.Read(ref controlsDirty) == 0)
                        return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Error ??= e.Message;
            }
            finally
            {
                controlLock.Release();
            }
        }

        private async Task applyOnce(MediaCapture localCapture, EzCameraControls controls)
        {
            VideoDeviceController controller = localCapture.VideoDeviceController;

            try
            {
                FocusControl focus = controller.FocusControl;

                if (focus.Supported)
                {
                    if (controls.AutoFocus && focusAuto)
                        await focus.SetPresetAsync(FocusPreset.Auto, false).AsTask(cts.Token).ConfigureAwait(false);
                    else if (focusManual)
                    {
                        focus.Configure(new FocusSettings
                        {
                            Mode = FocusMode.Manual,
                            Value = mapUint(controls.Focus, focusMin, focusMax, focusStep),
                            DisableDriverFallback = false,
                        });
                        await focus.FocusAsync().AsTask(cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                ExposureControl exposure = controller.ExposureControl;

                if (exposure.Supported)
                {
                    if (controls.AutoExposure && exposureAuto)
                        await exposure.SetAutoAsync(true).AsTask(cts.Token).ConfigureAwait(false);
                    else if (exposureManual)
                    {
                        await exposure.SetAutoAsync(false).AsTask(cts.Token).ConfigureAwait(false);
                        await exposure.SetValueAsync(mapTime(controls.Exposure, exposureMin, exposureMax, exposureStep)).AsTask(cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                WhiteBalanceControl whiteBalance = controller.WhiteBalanceControl;

                if (whiteBalance.Supported)
                {
                    if (controls.AutoWhiteBalance && whiteBalanceAuto)
                        await whiteBalance.SetPresetAsync(ColorTemperaturePreset.Auto).AsTask(cts.Token).ConfigureAwait(false);
                    else if (whiteBalanceManual)
                        await whiteBalance.SetValueAsync(mapUint(controls.WhiteBalance, whiteBalanceMin, whiteBalanceMax, whiteBalanceStep)).AsTask(cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }

        private void onFrame(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (Volatile.Read(ref disposed) != 0)
                return;

            try
            {
                using MediaFrameReference? frame = sender.TryAcquireLatestFrame();
                SoftwareBitmap? bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;

                if (bitmap == null)
                    return;

                SoftwareBitmap? converted = null;

                try
                {
                    SoftwareBitmap source = bitmap;

                    if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
                    {
                        converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
                        source = converted;
                    }

                    publish(source);
                    Error = null;
                }
                finally
                {
                    converted?.Dispose();
                }
            }
            catch (Exception e)
            {
                Error ??= e.Message;
            }
        }

        private unsafe void publish(SoftwareBitmap bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            if (width <= 0 || height <= 0 || width > absolute_max_width || height > absolute_max_height)
                return;

            using BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
            using IMemoryBufferReference reference = buffer.CreateReference();
            BitmapPlaneDescription plane = buffer.GetPlaneDescription(0);
            int stride = plane.Stride;

            if (stride <= 0)
                return;

            // CsWinRT projects the buffer as IInspectable, so a direct cast to the COM byte-access interface fails.
            IntPtr inspectable = ((IWinRTObject)reference).NativeObject.ThisPtr;
            Guid byteAccessId = new Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d");
            int queryResult = Marshal.QueryInterface(inspectable, in byteAccessId, out IntPtr byteAccess);

            if (queryResult < 0)
                Marshal.ThrowExceptionForHR(queryResult);

            try
            {
                byte* src = getBuffer(byteAccess, out uint capacity);
                int count = width * height;
                long needed = (long)plane.StartIndex + (long)(height - 1) * stride + (long)width * 4;

                if (needed > capacity)
                    return;

                if (backBuffer == null || backBuffer.Length < count)
                    backBuffer = new Rgba32[count];

                int destinationIndex = 0;

                for (int y = 0; y < height; y++)
                {
                    byte* row = src + plane.StartIndex + y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        byte* pixel = row + (x * 4);
                        backBuffer[destinationIndex++] = new Rgba32(pixel[2], pixel[1], pixel[0], byte.MaxValue);
                    }
                }

                lock (publishGate)
                {
                    Rgba32[]? previous = published;
                    published = backBuffer;
                    publishedWidth = width;
                    publishedHeight = height;
                    publishedSequence++;
                    backBuffer = previous != null && previous.Length >= count ? previous : new Rgba32[count];
                }
            }
            finally
            {
                Marshal.Release(byteAccess);
            }
        }

        private static unsafe byte* getBuffer(IntPtr byteAccess, out uint capacity)
        {
            IntPtr* vtable = *(IntPtr**)byteAccess;
            var getBufferPointer = (delegate* unmanaged[Stdcall]<IntPtr, byte**, uint*, int>)vtable[3];
            byte* data;
            uint size;
            int result = getBufferPointer(byteAccess, &data, &size);

            if (result < 0)
                Marshal.ThrowExceptionForHR(result);

            capacity = size;
            return data;
        }

        private async Task releaseAsync()
        {
            try
            {
                MediaFrameReader? localReader = reader;
                reader = null;

                if (localReader != null)
                {
                    localReader.FrameArrived -= onFrame;

                    try
                    {
                        await localReader.StopAsync();
                    }
                    catch (Exception)
                    {
                    }

                    localReader.Dispose();
                }

                capture?.Dispose();
                capture = null;
            }
            catch (Exception)
            {
            }
            finally
            {
                cts.Dispose();
                controlLock.Dispose();
            }
        }

        private static MediaFrameFormat? chooseFormat(MediaFrameSource source, int targetFps)
        {
            MediaFrameFormat? bestUnderCap = null;
            int bestScore = int.MinValue;
            MediaFrameFormat? smallest = null;
            ulong smallestArea = ulong.MaxValue;

            foreach (MediaFrameFormat format in source.SupportedFormats)
            {
                VideoMediaFrameFormat? video = format.VideoFormat;

                if (video == null || video.Width == 0 || video.Height == 0)
                    continue;

                ulong area = (ulong)video.Width * video.Height;

                if (area < smallestArea)
                {
                    smallestArea = area;
                    smallest = format;
                }

                if (video.Width > preferred_max_width || video.Height > preferred_max_height)
                    continue;

                int fps = estimateFps(format);
                int score = (int)(area / 1000) - Math.Abs(fps - targetFps) * 1000;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestUnderCap = format;
                }
            }

            return bestUnderCap ?? smallest;
        }

        private static int estimateFps(MediaFrameFormat format)
        {
            uint denominator = format.FrameRate.Denominator;

            if (denominator == 0)
                return 0;

            return (int)Math.Round(format.FrameRate.Numerator / (double)denominator);
        }

        private static uint mapUint(float amount, uint min, uint max, uint step)
        {
            if (max <= min)
                return min;

            amount = Math.Clamp(amount, 0, 1);
            double value = min + ((double)max - min) * amount;

            if (step > 1)
                value = min + Math.Round((value - min) / step) * step;

            return (uint)Math.Clamp(value, min, max);
        }

        private static TimeSpan mapTime(float amount, TimeSpan min, TimeSpan max, TimeSpan step)
        {
            if (max <= min)
                return min;

            amount = Math.Clamp(amount, 0, 1);
            double ticks = min.Ticks + (max.Ticks - min.Ticks) * amount;

            if (step > TimeSpan.Zero)
                ticks = min.Ticks + Math.Round((ticks - min.Ticks) / step.Ticks) * step.Ticks;

            return TimeSpan.FromTicks((long)Math.Clamp(ticks, min.Ticks, max.Ticks));
        }
    }

}
