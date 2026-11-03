// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Camera;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;
using osuTK;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// Local camera preview. Does not enumerate devices, open a capture session, or upload frames unless
    /// <see cref="Ez2Setting.EzHudCameraEnabled"/> is on.
    /// </summary>
    public partial class EzHUDCamera : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_SOURCE_LABEL), nameof(EzHUDStrings.CAMERA_SOURCE_DESCRIPTION))]
        public Bindable<EzCameraSourceKind> SourceKind { get; } = new Bindable<EzCameraSourceKind>(EzCameraSourceKind.Physical);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_DEVICE_LABEL), nameof(EzHUDStrings.CAMERA_DEVICE_DESCRIPTION), SettingControlType = typeof(CameraDeviceSelectorControl))]
        public Bindable<string> DeviceId { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_AUTO_FOCUS_LABEL), nameof(EzHUDStrings.CAMERA_AUTO_FOCUS_DESCRIPTION))]
        public Bindable<bool> AutoFocus { get; } = new Bindable<bool>(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_FOCUS_LABEL), nameof(EzHUDStrings.CAMERA_FOCUS_DESCRIPTION), SettingControlType = typeof(CameraManualSlider))]
        public BindableNumber<float> Focus { get; } = new BindableNumber<float>(0.5f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_AUTO_EXPOSURE_LABEL), nameof(EzHUDStrings.CAMERA_AUTO_EXPOSURE_DESCRIPTION))]
        public Bindable<bool> AutoExposure { get; } = new Bindable<bool>(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_EXPOSURE_LABEL), nameof(EzHUDStrings.CAMERA_EXPOSURE_DESCRIPTION), SettingControlType = typeof(CameraManualSlider))]
        public BindableNumber<float> Exposure { get; } = new BindableNumber<float>(0.5f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_AUTO_WHITE_BALANCE_LABEL), nameof(EzHUDStrings.CAMERA_AUTO_WHITE_BALANCE_DESCRIPTION))]
        public Bindable<bool> AutoWhiteBalance { get; } = new Bindable<bool>(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_WHITE_BALANCE_LABEL), nameof(EzHUDStrings.CAMERA_WHITE_BALANCE_DESCRIPTION), SettingControlType = typeof(CameraManualSlider))]
        public BindableNumber<float> WhiteBalance { get; } = new BindableNumber<float>(0.5f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_FPS_LABEL), nameof(EzHUDStrings.CAMERA_FPS_DESCRIPTION))]
        public Bindable<EzCameraFrameRate> FrameRate { get; } = new Bindable<EzCameraFrameRate>(EzCameraFrameRate.Fps30);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOX_ELEMENT_WIDTH_LABEL), nameof(EzHUDStrings.BOX_ELEMENT_WIDTH_DESCRIPTION))]
        public BindableNumber<float> CameraWidth { get; } = new BindableNumber<float>(320)
        {
            MinValue = 80,
            MaxValue = 1920,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOX_ELEMENT_HEIGHT_LABEL), nameof(EzHUDStrings.BOX_ELEMENT_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> CameraHeight { get; } = new BindableNumber<float>(240)
        {
            MinValue = 60,
            MaxValue = 1080,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.CAMERA_CORNER_RADIUS_LABEL), nameof(EzHUDStrings.CAMERA_CORNER_RADIUS_DESCRIPTION))]
        public new BindableFloat CornerRadius { get; } = new BindableFloat(0)
        {
            MinValue = 0,
            MaxValue = 0.5f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ALPHA_LABEL), nameof(EzHUDStrings.ALPHA_DESCRIPTION))]
        public BindableNumber<float> DisplayAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzCameraHost cameraHost { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Bindable<bool> cameraEnabled = null!;
        private Sprite? preview;
        private OsuSpriteText? errorText;
        private Texture? texture;
        private IEzCameraSession? session;
        private EzCameraCapabilities appliedCapabilities = EzCameraCapabilities.Unknown;
        private Rgba32[] frameBuffer = Array.Empty<Rgba32>();
        private long seenSequence;
        private int openGeneration;
        private bool opening;
        private bool suppressKnown;
        private bool suppressCapture;
        private bool controlsDirty;
        private double nextControlApply;
        private string? activeKey;

        public EzHUDCamera()
        {
            Size = new Vector2(320, 240);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            cameraEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzHudCameraEnabled);
            cameraEnabled.ValueChanged += onCameraEnabledChanged;

            CameraWidth.BindValueChanged(v => Width = v.NewValue, true);
            CameraHeight.BindValueChanged(v => Height = v.NewValue, true);
            DisplayAlpha.BindValueChanged(v => Alpha = v.NewValue, true);

            SourceKind.BindValueChanged(_ => activeKey = null);
            DeviceId.BindValueChanged(_ => activeKey = null);
            FrameRate.BindValueChanged(_ => activeKey = null);
            AutoFocus.BindValueChanged(_ => controlsDirty = true);
            Focus.BindValueChanged(_ => controlsDirty = true);
            AutoExposure.BindValueChanged(_ => controlsDirty = true);
            Exposure.BindValueChanged(_ => controlsDirty = true);
            AutoWhiteBalance.BindValueChanged(_ => controlsDirty = true);
            WhiteBalance.BindValueChanged(_ => controlsDirty = true);
        }

        protected override void Update()
        {
            base.Update();

            // Master switch off: no device I/O, no texture upload, no control writes.
            if (!cameraEnabled.Value)
                return;

            if (isToolboxPreview())
                return;

            if (Alpha <= 0 || !IsPresent)
            {
                if (session != null || opening)
                    stopSession();
                return;
            }

            string key = requestKey();

            if (session != null && activeKey != key)
                stopSession();

            if (session == null)
            {
                ensureSession();
                return;
            }

            if (!ReferenceEquals(appliedCapabilities, session.Capabilities))
                appliedCapabilities = session.Capabilities;

            if (controlsDirty && Time.Current >= nextControlApply)
            {
                controlsDirty = false;
                nextControlApply = Time.Current + 100;
                session.UpdateControls(currentControls());
            }

            base.CornerRadius = CornerRadius.Value * Math.Min(DrawWidth, DrawHeight);
            uploadLatest();
            showSessionError();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (cameraEnabled != null)
                cameraEnabled.ValueChanged -= onCameraEnabledChanged;

            openGeneration++;
            opening = false;
            session?.Dispose();
            session = null;
            texture?.Dispose();
            texture = null;

            base.Dispose(isDisposing);
        }

        private void onCameraEnabledChanged(ValueChangedEvent<bool> e)
        {
            if (!e.NewValue)
                stopSession();
        }

        private bool isToolboxPreview()
        {
            if (!suppressKnown)
            {
                suppressCapture = this.FindClosestParent<SkinComponentToolbox>() != null;
                suppressKnown = true;
            }

            return suppressCapture;
        }

        private string requestKey() => $"{SourceKind.Value}|{DeviceId.Value}|{FrameRate.Value}";

        private EzCameraControls currentControls() => new EzCameraControls(
            AutoFocus.Value,
            Focus.Value,
            AutoExposure.Value,
            Exposure.Value,
            AutoWhiteBalance.Value,
            WhiteBalance.Value);

        private void ensureSession()
        {
            if (opening || session != null)
                return;

            opening = true;
            int generation = ++openGeneration;
            string key = requestKey();
            var request = new EzCameraOpenRequest
            {
                SourceKind = SourceKind.Value,
                DeviceId = DeviceId.Value,
                FrameRate = FrameRate.Value,
                Controls = currentControls(),
            };

            _ = openSession(generation, key, request);
        }

        private async Task openSession(int generation, string key, EzCameraOpenRequest request)
        {
            IEzCameraSession? opened = null;
            bool failed = false;

            try
            {
                opened = await Task.Run(() =>
                {
                    IEzCameraBackend? backend = cameraHost.TryGetBackend();
                    return backend?.Open(request);
                }).ConfigureAwait(false);
            }
            catch (Exception)
            {
                failed = true;
            }

            Schedule(() =>
            {
                opening = false;

                if (generation != openGeneration || !cameraEnabled.Value || isToolboxPreview())
                {
                    opened?.Dispose();
                    return;
                }

                if (failed || opened == null)
                {
                    showError(cameraHost.IsPlatformSupported
                        ? EzHUDStrings.CAMERA_UNAVAILABLE
                        : EzHUDStrings.CAMERA_PLATFORM_UNSUPPORTED);
                    return;
                }

                session = opened;
                activeKey = key;
                appliedCapabilities = EzCameraCapabilities.Unknown;
                controlsDirty = false;
            });
        }

        private void stopSession()
        {
            openGeneration++;
            opening = false;
            activeKey = null;
            seenSequence = 0;
            appliedCapabilities = EzCameraCapabilities.Unknown;

            IEzCameraSession? local = session;
            session = null;
            local?.Dispose();

            texture?.Dispose();
            texture = null;
            frameBuffer = Array.Empty<Rgba32>();

            preview?.Expire();
            preview = null;
            errorText?.Expire();
            errorText = null;
            Masking = false;
        }

        private void uploadLatest()
        {
            if (session == null)
                return;

            int width;
            int height;
            bool copied = session.TryCopyLatest(frameBuffer, ref seenSequence, out width, out height);

            if (!copied)
            {
                if (width > 0 && height > 0 && frameBuffer.Length < width * height)
                    frameBuffer = new Rgba32[width * height];

                return;
            }

            if (texture == null || texture.Width != width || texture.Height != height)
            {
                texture?.Dispose();
                texture = renderer.CreateTexture(width, height);
                ensurePreview().Texture = texture;
            }

            var upload = new MemoryAllocatorTextureUpload(width, height);
            frameBuffer.AsSpan(0, width * height).CopyTo(upload.RawData);
            upload.Bounds = new RectangleI(0, 0, width, height);
            texture.SetData(upload, Opacity.Opaque);
            hideError();
        }

        private Sprite ensurePreview()
        {
            if (preview != null)
                return preview;

            Masking = true;
            AddInternal(preview = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fit,
            });
            return preview;
        }

        private void showSessionError()
        {
            if (session == null || string.IsNullOrEmpty(session.Error))
                return;

            showError(session.Error);
        }

        private void showError(LocalisableString message)
        {
            if (errorText == null)
            {
                AddInternal(errorText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Width = 0.92f,
                    AllowMultiline = true,
                    Font = OsuFont.GetFont(size: 14),
                });
            }

            errorText.Text = message;
        }

        private void hideError()
        {
            errorText?.Expire();
            errorText = null;
        }

        /// <summary>
        /// Disables the manual slider only after <see cref="SettingsItem{T}.Current"/> is assigned.
        /// Doing it earlier throws when the skin settings panel copies the default value.
        /// </summary>
        public partial class CameraManualSlider : SettingsSlider<float>
        {
            private EzHUDCamera? source;

            protected override void LoadComplete()
            {
                base.LoadComplete();
                source = SettingSourceObject as EzHUDCamera;
            }

            protected override void Update()
            {
                base.Update();

                if (source == null)
                    return;

                bool disable = manualDisabled(source);

                if (Current.Disabled != disable)
                    Current.Disabled = disable;
            }

            protected override void Dispose(bool isDisposing)
            {
                if (source != null && Current.Disabled)
                    Current.Disabled = false;

                base.Dispose(isDisposing);
            }

            private bool manualDisabled(EzHUDCamera camera)
            {
                if (ReferenceEquals(Current, camera.Focus))
                    return camera.AutoFocus.Value || (camera.appliedCapabilities.Ready && !camera.appliedCapabilities.ManualFocus);

                if (ReferenceEquals(Current, camera.Exposure))
                    return camera.AutoExposure.Value || (camera.appliedCapabilities.Ready && !camera.appliedCapabilities.ManualExposure);

                if (ReferenceEquals(Current, camera.WhiteBalance))
                    return camera.AutoWhiteBalance.Value || (camera.appliedCapabilities.Ready && !camera.appliedCapabilities.ManualWhiteBalance);

                return false;
            }
        }

        public partial class CameraDeviceSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private Ez2ConfigManager ezConfig { get; set; } = null!;

            [Resolved]
            private EzCameraHost cameraHost { get; set; } = null!;

            private EzHUDCamera source = null!;
            private Bindable<bool> cameraEnabled = null!;
            private readonly Dictionary<string, string> labels = new Dictionary<string, string>();
            private int refreshGeneration;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                source = (EzHUDCamera)SettingSourceObject;
                cameraEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzHudCameraEnabled);
                cameraEnabled.ValueChanged += onCameraEnabledChanged;
                source.SourceKind.BindValueChanged(_ => scheduleRefresh(), true);
            }

            protected override void Dispose(bool isDisposing)
            {
                if (cameraEnabled != null)
                    cameraEnabled.ValueChanged -= onCameraEnabledChanged;

                base.Dispose(isDisposing);
            }

            private void onCameraEnabledChanged(ValueChangedEvent<bool> _) => scheduleRefresh();

            private void scheduleRefresh()
            {
                int generation = ++refreshGeneration;

                if (!cameraEnabled.Value)
                {
                    labels.Clear();
                    var kept = new List<string> { string.Empty };

                    if (!string.IsNullOrEmpty(source.DeviceId.Value))
                        kept.Add(source.DeviceId.Value);

                    applyIds(kept);
                    return;
                }

                _ = Task.Run(async () =>
                {
                    IReadOnlyList<EzCameraDevice> devices;

                    try
                    {
                        IEzCameraBackend? backend = cameraHost.TryGetBackend();
                        devices = backend == null
                            ? Array.Empty<EzCameraDevice>()
                            : await backend.ListDevicesAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        devices = Array.Empty<EzCameraDevice>();
                    }

                    Schedule(() =>
                    {
                        if (IsDisposed || generation != refreshGeneration || !cameraEnabled.Value)
                            return;

                        labels.Clear();
                        var ids = new List<string> { string.Empty };

                        foreach (EzCameraDevice device in devices)
                        {
                            if (device.Kind != source.SourceKind.Value)
                                continue;

                            labels[device.Id] = device.Name;
                            ids.Add(device.Id);
                        }

                        applyIds(ids);
                    });
                });
            }

            private void applyIds(IReadOnlyList<string> ids)
            {
                var items = new List<string>(ids);
                string current = source.DeviceId.Value ?? string.Empty;

                if (current.Length > 0 && !items.Contains(current))
                    items.Insert(0, current);

                Items = items;
            }

            protected override OsuDropdown<string> CreateDropdown() => new DeviceDropdown(labels);

            private partial class DeviceDropdown : OsuDropdown<string>
            {
                private readonly Dictionary<string, string> labels;

                public DeviceDropdown(Dictionary<string, string> labels)
                {
                    this.labels = labels;
                    RelativeSizeAxes = Axes.X;
                }

                protected override LocalisableString GenerateItemText(string item)
                {
                    if (string.IsNullOrEmpty(item))
                        return EzHUDStrings.CAMERA_DEVICE_DEFAULT;

                    return labels.GetValueOrDefault(item, item);
                }
            }
        }
    }
}
