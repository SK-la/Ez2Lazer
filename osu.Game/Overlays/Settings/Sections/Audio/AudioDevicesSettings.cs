// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.EzOsuGame.Audio;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class AudioDevicesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.AudioDevicesHeader;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private FormDropdown<int>? sampleRateDropdown;

        private FormDropdown<int>? bufferSizeDropdown;

        private AudioDeviceDropdown dropdown = null!;

        private FormCheckBox? wasapiExperimental;

        private readonly Bindable<SettingsNote.Data?> wasapiExperimentalNote = new Bindable<SettingsNote.Data?>();

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(dropdown = new AudioDeviceDropdown
                {
                    Caption = AudioSettingsStrings.OutputDevice,
                    HintText = EzSettingsStrings.AUDIO_DEVICE_OUTPUT_HINT,
                })
                {
                    Keywords = new[] { "speaker", "headphone", "output" },
                },
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Add(new SettingsItemV2(sampleRateDropdown = new FormDropdown<int>
                {
                    Caption = EzSettingsStrings.ASIO_SAMPLE_RATE_LABEL,
                    HintText = EzSettingsStrings.ASIO_SAMPLE_RATE_HINT,
                    Current = ezConfig.GetBindable<int>(Ez2Setting.AsioSampleRate),
                    Items = AudioExtensions.COMMON_SAMPLE_RATES,
                })
                {
                    Keywords = new[] { "sample", "rate", "frequency" },
                });
                Add(new SettingsItemV2(bufferSizeDropdown = new FormDropdown<int>
                {
                    Caption = EzSettingsStrings.ASIO_BUFFER_SIZE_LABEL,
                    HintText = EzSettingsStrings.ASIO_BUFFER_SIZE_HINT,
                    Current = ezConfig.GetBindable<int>(Ez2Setting.AsioBufferSize),
                    Items = AudioExtensions.COMMON_BUFFER_SIZES,
                })
                {
                    Keywords = new[] { "asio", "buffer", "latency" },
                });
                Add(new SettingsItemV2(wasapiExperimental = new FormCheckBox
                {
                    Caption = AudioSettingsStrings.WasapiLabel,
                    HintText = AudioSettingsStrings.WasapiTooltip,
                    Current = audio.UseExperimentalWasapi,
                })
                {
                    Keywords = new[] { "wasapi", "latency", "exclusive" },
                    Note = { BindTarget = wasapiExperimentalNote },
                });

                // Setup ASIO configuration synchronization.
                audio.SetupAsioConfigurationSync(actualSampleRate =>
                {
                    Schedule(() =>
                    {
                        ensureDropdownContainsValue(sampleRateDropdown, actualSampleRate);
                        sampleRateDropdown.Current.Value = actualSampleRate;
                    });
                }, actualBufferSize =>
                {
                    Schedule(() =>
                    {
                        ensureDropdownContainsValue(bufferSizeDropdown, actualBufferSize);
                        bufferSizeDropdown.Current.Value = actualBufferSize;
                    });
                });

                wasapiExperimental.Current.ValueChanged += _ => onDeviceChanged(string.Empty);
                dropdown.Current.ValueChanged += e => onDeviceChanged(e.NewValue);
                sampleRateDropdown.Current.ValueChanged += e =>
                {
                    Logger.Log($"User set sample rate to {e.NewValue}Hz", LoggingTarget.Runtime, LogLevel.Debug);
                    audio.SetPreferredAsioSampleRate(e.NewValue);
                };
                bufferSizeDropdown.Current.ValueChanged += e =>
                {
                    Logger.Log($"User set ASIO buffer size to {e.NewValue}", LoggingTarget.Runtime, LogLevel.Debug);
                    audio.SetAsioBufferSize(e.NewValue);
                };

                audio.SetPreferredAsioSampleRate(sampleRateDropdown.Current.Value);
                audio.SetAsioBufferSize(bufferSizeDropdown.Current.Value);
            }

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
            dropdown.Current = audio.AudioDevice;
        }

        private void onDeviceChanged(string name)
        {
            updateItems();

            if (wasapiExperimental != null)
            {
                if (wasapiExperimental.Current.Value)
                    wasapiExperimentalNote.Value = new SettingsNote.Data(AudioSettingsStrings.WasapiNotice, SettingsNote.Type.Warning);
                else
                    wasapiExperimentalNote.Value = null;
            }

            bool isAsio = name.Contains("ASIO");

            if (sampleRateDropdown != null)
            {
                if (isAsio)
                    sampleRateDropdown.Show();
                else
                    sampleRateDropdown.Hide();
            }

            if (bufferSizeDropdown != null)
            {
                if (isAsio)
                    bufferSizeDropdown.Show();
                else
                    bufferSizeDropdown.Hide();
            }
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
            protected override LocalisableString GenerateItemText(string item)
                => string.IsNullOrEmpty(item) ? CommonStrings.Default : base.GenerateItemText(item);
        }
    }
}
