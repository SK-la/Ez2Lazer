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
                    Keywords = new[] { "speaker", "headphone", "output" }
                },
                sampleRateDropdown = new SettingsDropdown<int>
                {
                    LabelText = "Sample Rate",
                    Keywords = new[] { "sample", "rate", "frequency" },
                    Current = new Bindable<int>(48000),
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

                // 重新应用当前设备的采样率设置
                int currentSampleRate = audio.GetSampleRate();
                sampleRateDropdown.Current.Value = currentSampleRate;
                audio.SetSampleRate(currentSampleRate);

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
            var supportedRates = audio.GetSupportedSampleRates(audio.AudioDevice.Value);
            sampleRateDropdown.Items = supportedRates.ToList();
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
