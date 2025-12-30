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

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class AudioDevicesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.AudioDevicesHeader;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private SettingsDropdown<string> dropdown = null!;

        private SettingsDropdown<int> sampleRateDropdown = null!;

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
                    TooltipText = "ASIO is testing!"
                },
                sampleRateDropdown = new SettingsDropdown<int>
                {
                    LabelText = "ASIO Sample Rate(Testing)",
                    Keywords = new[] { "sample", "rate", "frequency" },
                    Current = new Bindable<int>(48000),
                    TooltipText = "48k is better, too high a value will cause delays and clock synchronization errors"
                },
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Add(wasapiExperimental = new SettingsCheckbox
                {
                    LabelText = AudioSettingsStrings.WasapiLabel,
                    TooltipText = AudioSettingsStrings.WasapiTooltip,
                    Current = audio.UseExperimentalWasapi,
                    Keywords = new[] { "wasapi", "latency", "exclusive" }
                });

                wasapiExperimental.Current.ValueChanged += _ => onDeviceChanged(string.Empty);
            }

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
            dropdown.Current = audio.AudioDevice;
            sampleRateDropdown.Current.Value = audio.GetSampleRate();
            sampleRateDropdown.Current.ValueChanged += e => audio.SetSampleRate(e.NewValue);

            // Setup ASIO sample rate synchronization
            audio.SetupAsioSampleRateSync(actualSampleRate =>
            {
                // Update the sample rate dropdown to reflect the actual rate used by ASIO device
                Schedule(() => sampleRateDropdown.Current.Value = actualSampleRate);
            });

            onDeviceChanged(string.Empty);
        }

        private void onDeviceChanged(string _)
        {
            // Audio device notifications may arrive from the audio thread.
            // Ensure UI mutations happen on the update thread.
            Schedule(() =>
            {
                updateItems();
                updateSampleRates();

                // 重新应用当前统一的采样率设置
                int currentSampleRate = audio.GetSampleRate();
                sampleRateDropdown.Current.Value = currentSampleRate;

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

        private void updateSampleRates()
        {
            string selectedDevice = audio.AudioDevice.Value;
            
            // Check if the selected device is an ASIO device
            if (selectedDevice.Contains("(ASIO)"))
            {
                // For ASIO devices, get the actual device name without the "(ASIO)" suffix
                string asioDeviceName = selectedDevice.Replace(" (ASIO)", "");
                
                // Get supported sample rates for this specific ASIO device
                var supportedRates = audio.GetAsioDeviceSupportedSampleRates(asioDeviceName);
                
                // Convert double array to int array for the dropdown
                sampleRateDropdown.Items = supportedRates.Select(rate => (int)rate).ToList();
                
                Logger.Log($"Updated ASIO sample rates for device '{asioDeviceName}': {string.Join(", ", supportedRates)}", LoggingTarget.Runtime, LogLevel.Debug);
            }
            else
            {
                // For non-ASIO devices, use the existing method
                var supportedRates = audio.GetSupportedSampleRates(selectedDevice);
                sampleRateDropdown.Items = supportedRates.ToList();
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
