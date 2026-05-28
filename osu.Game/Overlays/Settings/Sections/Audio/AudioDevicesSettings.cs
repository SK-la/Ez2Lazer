// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Asio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Audio;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class AudioDevicesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.AudioDevicesHeader;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private AsioFormatDropdown? sampleRateDropdown;
        private SettingsItemV2? sampleRateSettingsItem;

        private FormDropdown<int>? bufferSizeDropdown;
        private SettingsItemV2? bufferSizeSettingsItem;

        private AudioDeviceDropdown dropdown = null!;
        private SettingsItemV2? outputDeviceSettingsItem;

        private FormCheckBox? legacyAudio;
        private FormCheckBox? asioPassThrough;
        private SettingsItemV2? asioPassThroughSettingsItem;

        private SettingsButtonV2? reloadAsioDriverButton;

        private Bindable<int> configSampleRate = null!;
        private Bindable<int> configBitDepth = null!;
        private Bindable<int> configBufferSize = null!;
        private Bindable<bool> configAsioUseExternalPCM = null!;

        private bool suppressAsioFormatChanges;

        [BackgroundDependencyLoader]
        private void load()
        {
            configSampleRate = ezConfig.GetBindable<int>(Ez2Setting.AsioSampleRate);
            configBitDepth = ezConfig.GetBindable<int>(Ez2Setting.AsioBitDepth);
            configBufferSize = ezConfig.GetBindable<int>(Ez2Setting.AsioBufferSize);
            configAsioUseExternalPCM = ezConfig.GetBindable<bool>(Ez2Setting.AsioUseExternalPCM);

            Children = new Drawable[]
            {
                outputDeviceSettingsItem = new SettingsItemV2(dropdown = new AudioDeviceDropdown
                {
                    Caption = AudioSettingsStrings.OutputDevice,
                    HintText = EzSettingsStrings.AUDIO_DEVICE_OUTPUT_HINT,
                })
                {
                    Keywords = new[] { "speaker", "headphone", "output" }
                },
            };

            audio.ApplyEzAsioDefaults();
            audio.OnAsioOutputUnavailable = () => Schedule(() =>
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = EzSettingsStrings.ASIO_OUTPUT_UNAVAILABLE_NOTIFICATION,
                });
            });

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
            dropdown.Current = audio.AudioDevice;

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                var initialFormat = AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value);
                int initialBuffer = configBufferSize.Value > 0 ? configBufferSize.Value : AudioOutputDefaults.DEFAULT_ASIO_BUFFER_SIZE;

                sampleRateDropdown = new AsioFormatDropdown
                {
                    Caption = EzSettingsStrings.ASIO_SAMPLE_RATE_LABEL,
                    HintText = EzSettingsStrings.ASIO_SAMPLE_RATE_HINT,
                    Items = new[] { initialFormat },
                    Current = new Bindable<EzAsioFormatOption>(initialFormat),
                };

                bufferSizeDropdown = new FormDropdown<int>
                {
                    Caption = EzSettingsStrings.ASIO_BUFFER_SIZE_LABEL,
                    HintText = EzSettingsStrings.ASIO_BUFFER_SIZE_HINT,
                    Current = configBufferSize,
                    Items = new[] { initialBuffer },
                };

                Add(sampleRateSettingsItem = new SettingsItemV2(sampleRateDropdown)
                {
                    Keywords = new[] { "sample", "rate", "frequency", "bit", "depth", "format" },
                });
                Add(bufferSizeSettingsItem = new SettingsItemV2(bufferSizeDropdown)
                {
                    Keywords = new[] { "asio", "buffer", "latency" },
                });
                Add(asioPassThroughSettingsItem = new SettingsItemV2(asioPassThrough = new FormCheckBox
                {
                    Caption = EzSettingsStrings.ASIO_PASSTHROUGH_LABEL,
                    HintText = EzSettingsStrings.ASIO_PASSTHROUGH_HINT,
                    Current = configAsioUseExternalPCM,
                })
                {
                    Keywords = new[] { "asio", "pcm", "external", "internal", "passthrough", "driver", "panel" },
                });
                Add(reloadAsioDriverButton = new SettingsButtonV2
                {
                    Text = EzSettingsStrings.ASIO_RELOAD_DRIVER_LABEL,
                    TooltipText = EzSettingsStrings.ASIO_RELOAD_DRIVER_HINT,
                    Keywords = new[] { "asio", "reload", "driver", "refresh", "restart" },
                    Action = reloadAsioDriver,
                });
                Add(new SettingsItemV2(legacyAudio = new LegacyAudioCheckbox())
                {
                    Keywords = new[] { "wasapi", "latency", "exclusive", "legacy", "experimental" },
                });

                audio.SetupAsioConfigurationSync((actualSampleRate, actualBitDepth) =>
                {
                    Schedule(() =>
                    {
                        if (sampleRateDropdown?.Current == null)
                            return;

                        suppressAsioFormatChanges = true;

                        try
                        {
                            var format = AudioExtensions.ToFormatOption(actualSampleRate, actualBitDepth);
                            ensureDropdownContainsValue(sampleRateDropdown, format);
                            sampleRateDropdown.Current.Value = format;
                            configSampleRate.Value = actualSampleRate;
                            configBitDepth.Value = actualBitDepth;

                            requestAsioSettingsListRefresh(getCurrentDeviceSelection());
                        }
                        finally
                        {
                            suppressAsioFormatChanges = false;
                        }
                    });
                }, actualBufferSize =>
                {
                    Schedule(() =>
                    {
                        suppressAsioFormatChanges = true;

                        try
                        {
                            ensureDropdownContainsValue(bufferSizeDropdown, actualBufferSize);
                            if (bufferSizeDropdown?.Current != null)
                                bufferSizeDropdown.Current.Value = actualBufferSize;
                        }
                        finally
                        {
                            suppressAsioFormatChanges = false;
                        }
                    });
                });

                legacyAudio.Current.ValueChanged += _ => onDeviceChanged(string.Empty);

                dropdown.Current.ValueChanged += e => onDeviceChanged(e.NewValue);
                sampleRateDropdown.Current.BindValueChanged(e =>
                {
                    if (suppressAsioFormatChanges)
                        return;

                    Logger.Log($"User set ASIO format to {e.NewValue}", LoggingTarget.Runtime, LogLevel.Debug);
                    suppressAsioFormatChanges = true;

                    try
                    {
                        audio.SetAsioFormat(e.NewValue.SampleRate, e.NewValue.BitDepth);
                    }
                    finally
                    {
                        suppressAsioFormatChanges = false;
                    }
                });

                bufferSizeDropdown.Current.BindValueChanged(e =>
                {
                    if (suppressAsioFormatChanges)
                        return;

                    Logger.Log($"User set ASIO buffer size to {e.NewValue}", LoggingTarget.Runtime, LogLevel.Debug);
                    audio.SetAsioBufferSize(e.NewValue);
                });

                asioPassThrough!.Current.BindValueChanged(e =>
                {
                    Logger.Log($"User set ASIO PCM mode to {(e.NewValue ? "external" : "internal")}", LoggingTarget.Runtime, LogLevel.Debug);
                    audio.SetAsioUseExternalPCM(e.NewValue);
                    refreshAsioSettingVisibility(getCurrentDeviceSelection());
                    updateAudioDeviceStatusNote(getCurrentDeviceSelection());
                }, true);

                setAsioSettingsVisible(false);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (RuntimeInfo.OS != RuntimeInfo.Platform.Windows || sampleRateDropdown == null)
                return;

            Scheduler.AddOnce(initialiseAsioSettings);
        }

        private void initialiseAsioSettings()
        {
            updateItems();

            suppressAsioFormatChanges = true;

            try
            {
                string deviceSelection = getCurrentDeviceSelection();
                setAsioSettingsVisible(isAsioSelection(deviceSelection));

                if (!isAsioSelection(deviceSelection))
                    return;

                audio.SetAsioUseExternalPCM(configAsioUseExternalPCM.Value);

                requestAsioSettingsListRefresh(deviceSelection);
                updateAudioDeviceStatusNote(deviceSelection);

                var format = AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value);
                ensureDropdownContainsValue(sampleRateDropdown, format);

                if (sampleRateDropdown?.Current != null && !configAsioUseExternalPCM.Value)
                    sampleRateDropdown.Current.Value = pickSupportedFormat(format);

                // Driver-panel mode: do not push saved fallback values onto the ASIO driver on open.
                if (!configAsioUseExternalPCM.Value)
                {
                    audio.SetAsioFormat(configSampleRate.Value, configBitDepth.Value);
                    audio.SetAsioBufferSize(bufferSizeDropdown?.Current.Value ?? configBufferSize.Value);
                }
            }
            finally
            {
                suppressAsioFormatChanges = false;
            }
        }

        private string getCurrentDeviceSelection() => dropdown.Current.Value ?? audio.AudioDevice.Value ?? string.Empty;

        private static bool isAsioSelection(string? selection) => selection?.Contains("ASIO", StringComparison.Ordinal) == true;

        private void onDeviceChanged(string? _)
        {
            Scheduler.AddOnce(() =>
            {
                updateItems();

                string deviceSelection = getCurrentDeviceSelection();
                setAsioSettingsVisible(isAsioSelection(deviceSelection));

                // Do not query/touch the ASIO driver from the UI thread here — that raced audio-thread init and caused silent output.
                // Lists refresh after audio-thread initialisation via SetupAsioConfigurationSync, or from cache if already running.
                requestAsioSettingsListRefresh(deviceSelection);
                updateAudioDeviceStatusNote(deviceSelection);
            });
        }

        private void requestAsioSettingsListRefresh(string deviceSelection)
        {
            if (!isAsioSelection(deviceSelection))
                return;

            audio.RequestAsioSettingsListRefresh(deviceSelection, () =>
            {
                refreshAsioFormatItems(deviceSelection);
                refreshAsioBufferItems(deviceSelection);
                updateAudioDeviceStatusNote(deviceSelection);
            });
        }

        private void setAsioSettingsVisible(bool visible)
        {
            if (visible)
            {
                asioPassThroughSettingsItem?.Show();
                reloadAsioDriverButton?.Show();
                refreshAsioSettingVisibility(getCurrentDeviceSelection());
            }
            else
            {
                sampleRateSettingsItem?.Hide();
                bufferSizeSettingsItem?.Hide();
                asioPassThroughSettingsItem?.Hide();
                reloadAsioDriverButton?.Hide();
            }
        }

        private void reloadAsioDriver()
        {
            if (reloadAsioDriverButton == null)
                return;

            reloadAsioDriverButton.Enabled.Value = false;

            audio.ReloadCurrentAudioDevice(success =>
            {
                Schedule(() =>
                {
                    reloadAsioDriverButton.Enabled.Value = true;

                    string deviceSelection = getCurrentDeviceSelection();
                    requestAsioSettingsListRefresh(deviceSelection);
                    updateAudioDeviceStatusNote(deviceSelection);

                    if (!success)
                    {
                        notifications?.Post(new SimpleNotification
                        {
                            Text = EzSettingsStrings.ASIO_RELOAD_DRIVER_FAILED_NOTIFICATION,
                        });
                    }
                });
            });
        }

        private void refreshAsioSettingVisibility(string? deviceSelection)
        {
            bool isAsio = isAsioSelection(deviceSelection);
            bool useExternalPCM = configAsioUseExternalPCM.Value;

            if (!isAsio || useExternalPCM)
            {
                sampleRateSettingsItem?.Hide();
                bufferSizeSettingsItem?.Hide();
            }
            else
            {
                sampleRateSettingsItem?.Show();
                bufferSizeSettingsItem?.Show();
            }
        }

        private void refreshAsioFormatItems(string deviceSelection)
        {
            if (sampleRateDropdown == null)
                return;

            if (!EzAsioDeviceManager.TryParseDeviceSelection(deviceSelection, out string asioName))
            {
                sampleRateDropdown.Items = new[] { AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value) };
                return;
            }

            IReadOnlyList<EzAsioFormatOption> supported;

            try
            {
                supported = audio.GetAsioSupportedFormats(asioName);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to query ASIO formats for '{asioName}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                supported = new[] { AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value) };
            }

            if (supported.Count == 0)
                supported = new[] { AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value) };

            sampleRateDropdown.Items = supported;

            var desired = AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value);
            var picked = pickSupportedFormat(desired);
            ensureDropdownContainsValue(sampleRateDropdown, picked);

            if (!suppressAsioFormatChanges && !configAsioUseExternalPCM.Value)
            {
                sampleRateDropdown.Current.Value = picked;
                configSampleRate.Value = picked.SampleRate;
                configBitDepth.Value = picked.BitDepth;
            }
        }

        private EzAsioFormatOption pickSupportedFormat(EzAsioFormatOption desired)
        {
            if (sampleRateDropdown?.Items == null)
                return desired;

            if (sampleRateDropdown.Items.Any(i => i == desired))
                return desired;

            return sampleRateDropdown.Items.First();
        }

        private void refreshAsioBufferItems(string deviceSelection)
        {
            if (bufferSizeDropdown == null)
                return;

            if (!EzAsioDeviceManager.TryParseDeviceSelection(deviceSelection, out string asioName))
            {
                bufferSizeDropdown.Items = new[] { configBufferSize.Value };
                return;
            }

            IReadOnlyList<int> supported;

            try
            {
                supported = audio.GetAsioSupportedBufferSizes(asioName);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to query ASIO buffer sizes for '{asioName}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                supported = new[] { configBufferSize.Value };
            }

            if (supported.Count == 0)
                supported = new[] { configBufferSize.Value > 0 ? configBufferSize.Value : AudioOutputDefaults.DEFAULT_ASIO_BUFFER_SIZE };

            bufferSizeDropdown.Items = supported;

            int desired = configBufferSize.Value > 0 ? configBufferSize.Value : supported[0];
            int picked = pickSupportedBufferSize(desired, supported);
            ensureDropdownContainsValue(bufferSizeDropdown, picked);

            if (!suppressAsioFormatChanges && !configAsioUseExternalPCM.Value)
            {
                bufferSizeDropdown.Current.Value = picked;
                configBufferSize.Value = picked;
            }
        }

        private static int pickSupportedBufferSize(int desired, IReadOnlyList<int> supported)
        {
            if (supported.Contains(desired))
                return desired;

            return supported.OrderBy(v => Math.Abs(v - desired)).First();
        }

        private static void ensureDropdownContainsValue(AsioFormatDropdown? dropdown, EzAsioFormatOption value)
        {
            if (dropdown == null)
                return;

            var items = dropdown.Items?.ToList() ?? new List<EzAsioFormatOption>();

            if (items.Any(i => i == value))
                return;

            dropdown.Items = items.Append(value).Distinct().OrderBy(v => v.SampleRate).ThenBy(v => v.BitDepth).ToList();
        }

        private static void ensureDropdownContainsValue(FormDropdown<int>? dropdown, int value)
        {
            if (dropdown == null)
                return;

            if (dropdown.Items.Contains(value))
                return;

            dropdown.Items = dropdown.Items.Append(value).Distinct().OrderBy(v => v).ToList();
        }

        private void updateItems()
        {
            var deviceItems = new List<string> { string.Empty };
            deviceItems.AddRange(audio.AudioDeviceNames);

            string? preferredDeviceName = audio.AudioDevice.Value;

            if (!string.IsNullOrEmpty(preferredDeviceName) && deviceItems.All(kv => kv != preferredDeviceName))
                deviceItems.Add(preferredDeviceName);

            dropdown.Items = deviceItems
                             .Where(i => i.IsNotNull())
                             .Distinct()
                             .ToList();
        }

        private void updateAudioDeviceStatusNote(string? deviceSelection)
        {
            if (outputDeviceSettingsItem == null)
                return;

            if (!isAsioSelection(deviceSelection))
            {
                outputDeviceSettingsItem.Note.Value = null;
                return;
            }

            if (!EzAsioDeviceManager.TryParseDeviceSelection(deviceSelection ?? string.Empty, out string asioName))
            {
                outputDeviceSettingsItem.Note.Value = null;
                return;
            }

            outputDeviceSettingsItem.Note.Value = new SettingsNote.Data(audio.GetAsioStatusNote(asioName).ToDisplayText(), SettingsNote.Type.Informational);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (audio.IsNotNull())
            {
                audio.OnNewDevice -= onDeviceChanged;
                audio.OnLostDevice -= onDeviceChanged;
            }
        }

        private partial class AudioDeviceDropdown : FormDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item) => string.IsNullOrEmpty(item) ? CommonStrings.Default : base.GenerateItemText(item);
        }

        private partial class AsioFormatDropdown : FormDropdown<EzAsioFormatOption>
        {
            protected override LocalisableString GenerateItemText(EzAsioFormatOption item) => item.DisplayName;
        }
    }

    public partial class LegacyAudioCheckbox : FormCheckBox
    {
        private Bindable<bool> configExperimentalAudio = null!;

        public LegacyAudioCheckbox()
        {
            Caption = AudioSettingsStrings.LegacyAudioLabel;
            HintText = AudioSettingsStrings.LegacyAudioTooltip;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            configExperimentalAudio = audio.UseExperimentalWasapi.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.ValueChanged += legacy =>
            {
                configExperimentalAudio.Value = !legacy.NewValue;
            };

            configExperimentalAudio.BindValueChanged(experimental =>
            {
                Current.Value = !experimental.NewValue;
            }, true);
        }
    }
}
