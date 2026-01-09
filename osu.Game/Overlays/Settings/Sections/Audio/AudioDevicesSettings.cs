// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.LAsEzExtensions.Audio;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Framework.Logging;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class AudioDevicesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.AudioDevicesHeader;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private SettingsDropdown<string> dropdown = null!;

        private SettingsDropdown<int>? sampleRateDropdown;

        private SettingsCheckbox? wasapiExperimental;

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                dropdown = new AudioDeviceSettingsDropdown
                {
                    LabelText = AudioSettingsStrings.OutputDevice,
                    Keywords = new[] { "speaker", "headphone", "output" },
                    TooltipText = "ASIO is testing! For virtual devices, you may need to switch between physical devices before switching back to virtual devices, or the virtual device will be inactive."
                },
            };

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
            dropdown.Current = audio.AudioDevice;

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Add(sampleRateDropdown = new SettingsDropdown<int>
                {
                    LabelText = "ASIO Sample Rate(Testing)",
                    Keywords = new[] { "sample", "rate", "frequency" },
                    Items = AudioExtensions.COMMON_SAMPLE_RATES,
                    Current = ezConfig.GetBindable<int>(Ez2Setting.AsioSampleRate),
                    // Current = new Bindable<int>(audio.GetSampleRate()),
                    TooltipText = "48k is better, too high a value will cause delays and clock synchronization errors"
                });
                Add(wasapiExperimental = new SettingsCheckbox
                {
                    LabelText = AudioSettingsStrings.WasapiLabel,
                    TooltipText = AudioSettingsStrings.WasapiTooltip,
                    Current = audio.UseExperimentalWasapi,
                    Keywords = new[] { "wasapi", "latency", "exclusive" }
                });

                wasapiExperimental.Current.ValueChanged += _ => onDeviceChanged(string.Empty);
                sampleRateDropdown.Current.ValueChanged += e => audio.SetSampleRate(e.NewValue);

                // Setup ASIO sample rate synchronization
                audio.SetupAsioSampleRateSync(actualSampleRate =>
                {
                    // Update the sample rate dropdown to reflect the actual rate used by ASIO device
                    Schedule(() => sampleRateDropdown.Current.Value = actualSampleRate);
                });

                // 根据初始设备类型显示或隐藏采样率设置
                dropdown.Current.ValueChanged += e =>
                {
                    if (e.NewValue.Contains("(ASIO)")) sampleRateDropdown.Show();
                    else sampleRateDropdown.Hide();
                };
            }

            onDeviceChanged(string.Empty);
        }

        private void onDeviceChanged(string deviceName)
        {
            // Audio device notifications may arrive from the audio thread.
            // Ensure UI mutations happen on the update thread.
            Schedule(() =>
            {
                updateItems();
                updateSampleRates(!string.IsNullOrEmpty(deviceName));

                if (sampleRateDropdown != null)
                {
                    // 重新应用当前统一的采样率设置
                    int currentSampleRate = audio.GetSampleRate();
                    sampleRateDropdown.Current.Value = currentSampleRate;

                    // 根据设备类型显示或隐藏采样率设置
                    string selectedDevice = audio.AudioDevice.Value;
                    sampleRateDropdown.FadeTo(selectedDevice.Contains("(ASIO)") ? 1 : 0);
                }

                if (wasapiExperimental != null)
                {
                    if (wasapiExperimental.Current.Value)
                        wasapiExperimental.SetNoticeText(AudioSettingsStrings.WasapiNotice, true);
                    else
                        wasapiExperimental.ClearNoticeText();
                }
            });
        }

        private void updateItems()
        {
            var deviceItems = new List<string> { string.Empty };
            deviceItems.AddRange(audio.AudioDeviceNames);

            string preferredDeviceName = audio.AudioDevice.Value;
            if (deviceItems.All(kv => kv != preferredDeviceName))
                deviceItems.Add(preferredDeviceName);

            // The option dropdown for audio device selection lists all audio
            // device names. Dropdowns, however, may not have multiple identical
            // keys. Thus, we remove duplicate audio device names from
            // the dropdown. BASS does not give us a simple mechanism to select
            // specific audio devices in such a case anyways. Such
            // functionality would require involved OS-specific code.
            dropdown.Items = deviceItems
                             // Dropdown doesn't like null items. Somehow we are seeing some arrive here (see https://github.com/ppy/osu/issues/21271)
                             .Where(i => i.IsNotNull())
                             .Distinct()
                             .ToList();
        }

        private void updateSampleRates(bool forceSetCurrent = false)
        {
            if (sampleRateDropdown == null)
                return;

            string selectedDevice = audio.AudioDevice.Value;

            Logger.Log($"[AudioDevicesSettings] updateSampleRates called with selectedDevice: '{selectedDevice}', forceSetCurrent: {forceSetCurrent}", LoggingTarget.Runtime, LogLevel.Debug);

            // Check if the selected device is an ASIO device
            if (selectedDevice.Contains("(ASIO)"))
            {
                Logger.Log($"[AudioDevicesSettings] Detected ASIO device: '{selectedDevice}'", LoggingTarget.Runtime, LogLevel.Debug);

                // For ASIO devices, ensure current sample rate is valid for the fixed list
                int currentRate = audio.GetSampleRate();

                if (forceSetCurrent && !AudioExtensions.COMMON_SAMPLE_RATES.Contains(currentRate))
                {
                    // Set to first available rate if current rate is not supported
                    sampleRateDropdown.Current.Value = AudioExtensions.COMMON_SAMPLE_RATES[0];
                }
            }
            else
            {
                // For non-ASIO devices, no sample rate options
                // Items is already set to COMMON_SAMPLE_RATES, but since hidden, no issue
            }
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

        private partial class AudioDeviceSettingsDropdown : SettingsDropdown<string>
        {
            protected override OsuDropdown<string> CreateDropdown() => new AudioDeviceDropdownControl();

            private partial class AudioDeviceDropdownControl : DropdownControl
            {
                protected override LocalisableString GenerateItemText(string item)
                    => string.IsNullOrEmpty(item) ? CommonStrings.Default : base.GenerateItemText(item);
            }
        }
    }
}
