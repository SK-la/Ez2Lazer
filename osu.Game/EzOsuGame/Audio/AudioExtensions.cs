// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio;
using osu.Framework.Audio.Asio;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Audio
{
    public static class AudioExtensions
    {
        public static readonly int[] COMMON_BUFFER_SIZES = { 64, 128, 256, 512, 1024, 2048 };

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
    }
}
