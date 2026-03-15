// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Audio;
using osu.Framework.Logging;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.LAsEzExtensions.Audio
{
    public static class AudioExtensions
    {
        // 固定采样率列表，优先使用48kHz
        public static readonly int[] COMMON_SAMPLE_RATES = { 48000, 44100, 96000, 192000 };

        public static readonly int[] COMMON_BUFFER_SIZES = { 64, 128, 256, 512, 1024, 2048 };

        public static void SetupAsioConfigurationSync(this AudioManager audioManager, Action<int> onSampleRateChanged, Action<int> onBufferSizeChanged)
        {
            audioManager.OnAsioDeviceConfigurationChanged += (sampleRate, bufferSize) =>
            {
                int intSampleRate = (int)sampleRate;
                Logger.Log($"ASIO device initialized with sample rate {intSampleRate}Hz and buffer size {bufferSize}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                onSampleRateChanged(intSampleRate);
                onBufferSizeChanged(bufferSize);
            };
        }
    }
}
