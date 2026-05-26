// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Audio.Asio;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Audio
{
    public static class AudioExtensions
    {
        /// <summary>
        /// Applies Ez2Lazer ASIO defaults using <see cref="EzAsioAudioDefaults.VirtualHostWarmUpNamePatterns"/>.
        /// </summary>
        public static void ApplyEzAsioDefaults(this AudioManager audioManager)
            => ApplyEzAsioDefaults(audioManager, EzAsioAudioDefaults.VirtualHostWarmUpNamePatterns);

        /// <summary>
        /// Applies ASIO virtual host warm-up name patterns (case-insensitive substring match on ASIO device names).
        /// </summary>
        public static void ApplyEzAsioDefaults(this AudioManager audioManager, IEnumerable<string> virtualHostWarmUpNamePatterns)
        {
            audioManager.ConfigureAsioVirtualHostWarmUpNamePatterns(virtualHostWarmUpNamePatterns);
        }

        public static void SetupAsioConfigurationSync(this AudioManager audioManager, Action<int, int> onFormatChanged, Action<int> onBufferSizeChanged)
        {
            audioManager.OnAsioDeviceConfigurationChanged += (sampleRate, bufferSize, bitDepth) =>
            {
                int intSampleRate = (int)sampleRate;
                Logger.Log($"ASIO device initialized with sample rate {intSampleRate}Hz, buffer size {bufferSize}, bit depth {bitDepth}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                onFormatChanged(intSampleRate, bitDepth);
                onBufferSizeChanged(bufferSize);
            };
        }

        public static EzAsioFormatOption ToFormatOption(int sampleRate, int bitDepth) => new EzAsioFormatOption(sampleRate, bitDepth);

        /// <summary>
        /// Refreshes ASIO format/buffer dropdowns after capabilities are read on the audio thread.
        /// </summary>
        public static void RequestAsioSettingsListRefresh(this AudioManager audioManager, string deviceSelection, Action refreshLists)
        {
            if (!EzAsioDeviceManager.TryParseDeviceSelection(deviceSelection, out string asioName))
            {
                refreshLists();
                return;
            }

            audioManager.RequestAsioCapabilitiesRefresh(asioName, refreshLists);
        }
    }
}
