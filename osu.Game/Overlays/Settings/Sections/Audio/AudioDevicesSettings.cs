// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        private AsioFormatDropdown? sampleRateDropdown;

        private FormDropdown<int>? bufferSizeDropdown;

        private AudioDeviceDropdown dropdown = null!;

        private FormCheckBox? legacyAudio;

        private Bindable<int> configSampleRate = null!;
        private Bindable<int> configBitDepth = null!;

        private bool suppressAsioFormatChanges;

        [BackgroundDependencyLoader]
        private void load()
        {
            configSampleRate = ezConfig.GetBindable<int>(Ez2Setting.AsioSampleRate);
            configBitDepth = ezConfig.GetBindable<int>(Ez2Setting.AsioBitDepth);

            Children = new Drawable[]
            {
                new SettingsItemV2(dropdown = new AudioDeviceDropdown
                {
                    Caption = AudioSettingsStrings.OutputDevice,
                    HintText = EzSettingsStrings.AUDIO_DEVICE_OUTPUT_HINT,
                })
                {
                    Keywords = new[] { "speaker", "headphone", "output" }
                },
            };

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
            dropdown.Current = audio.AudioDevice;

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                var initialFormat = AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value);

                Add(new SettingsItemV2(sampleRateDropdown = new AsioFormatDropdown
                {
                    Caption = EzSettingsStrings.ASIO_SAMPLE_RATE_LABEL,
                    HintText = EzSettingsStrings.ASIO_SAMPLE_RATE_HINT,
                    Items = getDefaultFormatItems(),
                    Current = new Bindable<EzAsioFormatOption>(initialFormat),
                })
                {
                    Keywords = new[] { "sample", "rate", "frequency", "bit", "depth", "format" },
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
                        ensureDropdownContainsValue(bufferSizeDropdown, actualBufferSize);
                        bufferSizeDropdown.Current.Value = actualBufferSize;
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
                refreshAsioFormatItems(deviceSelection);
                refreshAsioFormatVisibility(deviceSelection);

                var format = AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value);
                ensureDropdownContainsValue(sampleRateDropdown, format);

                if (sampleRateDropdown.Current != null)
                    sampleRateDropdown.Current.Value = format;

                audio.SetAsioFormat(configSampleRate.Value, configBitDepth.Value);

                if (bufferSizeDropdown?.Current != null)
                    audio.SetAsioBufferSize(bufferSizeDropdown.Current.Value);
            }
            finally
            {
                suppressAsioFormatChanges = false;
            }
        }

        private string getCurrentDeviceSelection() => dropdown.Current?.Value ?? audio.AudioDevice.Value ?? string.Empty;

        private void onDeviceChanged(string? name)
        {
            Scheduler.AddOnce(() =>
            {
                updateItems();
                refreshAsioFormatVisibility(name);
                refreshAsioFormatItems(name);
            });
        }

        private void refreshAsioFormatVisibility(string? name)
        {
            bool isAsio = name?.Contains("ASIO") == true;

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

        private void refreshAsioFormatItems(string? deviceSelection)
        {
            if (sampleRateDropdown == null)
                return;

            deviceSelection ??= getCurrentDeviceSelection();

            if (!deviceSelection.Contains("ASIO", System.StringComparison.Ordinal))
            {
                sampleRateDropdown.Items = getDefaultFormatItems();
                return;
            }

            if (!EzAsioDeviceManager.TryParseDeviceSelection(deviceSelection, out string asioName))
            {
                sampleRateDropdown.Items = getDefaultFormatItems();
                return;
            }

            IReadOnlyList<EzAsioFormatOption> supported;

            try
            {
                supported = audio.GetAsioSupportedFormats(asioName);
            }
            catch (System.Exception ex)
            {
                Logger.Log($"Failed to query ASIO formats for '{asioName}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                supported = getDefaultFormatItems();
            }

            sampleRateDropdown.Items = supported.Count > 0
                ? supported
                : getDefaultFormatItems();

            if (sampleRateDropdown.Current == null)
                return;

            var current = AudioExtensions.ToFormatOption(configSampleRate.Value, configBitDepth.Value);
            ensureDropdownContainsValue(sampleRateDropdown, current);

            if (!suppressAsioFormatChanges)
                sampleRateDropdown.Current.Value = current;
        }

        private static IReadOnlyList<EzAsioFormatOption> getDefaultFormatItems()
        {
            var items = new List<EzAsioFormatOption>();

            foreach (int rate in EzAsioFormatOption.COMMON_SAMPLE_RATES)
            {
                foreach (int bits in EzAsioFormatOption.SUPPORTED_BIT_DEPTHS)
                    items.Add(new EzAsioFormatOption(rate, bits));
            }

            return items;
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

        private partial class AsioFormatDropdown : FormDropdown<EzAsioFormatOption>
        {
            protected override LocalisableString GenerateItemText(EzAsioFormatOption item)
                => item.DisplayName;
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
