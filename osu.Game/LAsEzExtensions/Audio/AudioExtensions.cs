// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Framework;
using ManagedBass;

namespace osu.Game.LAsEzExtensions.Audio
{
    public static class AudioExtensions
    {
        // 固定采样率列表，常见的值
        private static readonly int[] CommonSampleRates = { 44100, 48000, 88200, 96000, 176400, 192000 };

        // 存储用户选择的采样率设置
        private static readonly Dictionary<string, int> deviceSampleRates = new Dictionary<string, int>();

        // 扩展方法：获取当前采样率
        public static int GetSampleRate(this AudioManager audioManager)
        {
            string deviceKey = audioManager.AudioDevice.Value;
            if (deviceSampleRates.TryGetValue(deviceKey, out int savedRate))
                return savedRate;

            // 如果没有保存的设置，返回默认值
            return 44100;
        }

        // 扩展方法：设置采样率
        public static void SetSampleRate(this AudioManager audioManager, int sampleRate)
        {
            string deviceKey = audioManager.AudioDevice.Value;
            deviceSampleRates[deviceKey] = sampleRate;

            // 解析当前选择的设备来确定输出模式
            var (mode, deviceName, asioIndex) = parseSelection(audioManager.AudioDevice.Value, audioManager.UseExperimentalWasapi.Value);

            // 对于ASIO设备，设置首选采样率以便在设备初始化时使用
            if (mode == AudioOutputMode.Asio)
            {
                AudioManager.SetPreferredAsioSampleRate(sampleRate);
            }

            // 尝试实际设置采样率
            TrySetActualSampleRate(audioManager, sampleRate);
        }

        // 尝试设置实际的采样率
        private static void TrySetActualSampleRate(AudioManager audioManager, int sampleRate)
        {
            // 解析当前选择的设备来确定输出模式
            var (mode, deviceName, asioIndex) = parseSelection(audioManager.AudioDevice.Value, audioManager.UseExperimentalWasapi.Value);

            try
            {
                switch (mode)
                {
                    case AudioOutputMode.Asio:
                        // 对于ASIO设备，尝试通过反射设置采样率
                        var bassAsioType = typeof(AudioManager).Assembly.GetType("osu.Framework.Audio.Asio.BassAsio");
                        var setRateMethod = bassAsioType?.GetMethod("SetRate", BindingFlags.Public | BindingFlags.Static);
                        if (setRateMethod != null)
                        {
                            setRateMethod.Invoke(null, new object[] { (double)sampleRate });
                        }
                        break;

                    case AudioOutputMode.WasapiExclusive:
                    case AudioOutputMode.WasapiShared:
                        // 对于WASAPI设备，暂时不做任何操作
                        break;

                    case AudioOutputMode.Default:
                    default:
                        // 对于默认BASS设备，采样率通常不能运行时改变
                        break;
                }
            }
            catch
            {
                // 如果设置失败，忽略错误
            }
        }

        // 扩展方法：获取支持的采样率列表
        public static IEnumerable<int> GetSupportedSampleRates(this AudioManager audioManager, string deviceName)
        {
            // 解析设备名称来确定输出模式
            var (mode, parsedDeviceName, asioIndex) = parseSelection(deviceName, audioManager.UseExperimentalWasapi.Value);

            try
            {
                switch (mode)
                {
                    case AudioOutputMode.Asio:
                        // 对于ASIO设备，尝试获取支持的采样率
                        var asioAudioFormatType = typeof(AudioManager).Assembly.GetType("osu.Framework.Audio.Asio.AsioAudioFormat");
                        var supportedSampleRatesProperty = asioAudioFormatType?.GetProperty("SupportedSampleRates", BindingFlags.Public | BindingFlags.Static);
                        if (supportedSampleRatesProperty != null)
                        {
                            var rates = supportedSampleRatesProperty.GetValue(null) as IEnumerable<double>;
                            if (rates != null)
                                return rates.Select(r => (int)r);
                        }
                        return CommonSampleRates;

                    case AudioOutputMode.WasapiExclusive:
                    case AudioOutputMode.WasapiShared:
                        // 对于WASAPI设备，返回常见的采样率
                        return CommonSampleRates;

                    case AudioOutputMode.Default:
                    default:
                        // 对于默认BASS设备，返回常见的采样率
                        return CommonSampleRates;
                }
            }
            catch
            {
                // 如果获取失败，返回默认列表
                return CommonSampleRates;
            }
        }

        // 解析设备选择字符串，返回输出模式
        private static (AudioOutputMode mode, string deviceName, int? asioDeviceIndex) parseSelection(string selection, bool useExperimentalWasapi)
        {
            // 默认设备
            if (string.IsNullOrEmpty(selection))
            {
                return (useExperimentalWasapi && RuntimeInfo.OS == RuntimeInfo.Platform.Windows
                    ? AudioOutputMode.WasapiShared
                    : AudioOutputMode.Default, string.Empty, null);
            }

            // 检查是否是WASAPI独占模式
            if (tryParseSuffixed(selection, "(WASAPI Exclusive)", out string baseName))
                return (AudioOutputMode.WasapiExclusive, baseName, null);

            // 检查是否是ASIO模式
            if (tryParseSuffixed(selection, "(ASIO)", out baseName))
            {
                return (AudioOutputMode.Asio, baseName, null);
            }

            // 其他情况默认为默认模式
            return (AudioOutputMode.Default, selection, null);
        }

        // 尝试从后缀解析设备名称
        private static bool tryParseSuffixed(string value, string suffix, out string baseName)
        {
            baseName = string.Empty;
            if (value.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                baseName = value.Substring(0, value.Length - suffix.Length);
                return true;
            }
            return false;
        }

        // 音频输出模式枚举（从osu-framework复制）
        private enum AudioOutputMode
        {
            Default,
            WasapiShared,
            WasapiExclusive,
            Asio,
        }
    }
}
