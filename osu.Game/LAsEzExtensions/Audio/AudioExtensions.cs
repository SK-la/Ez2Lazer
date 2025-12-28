// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework;
using osu.Framework.Audio;

namespace osu.Game.LAsEzExtensions.Audio
{
    public static class AudioExtensions
    {
        // 固定采样率列表，常见的值
        private static readonly int[] common_sample_rates = { 44100, 48000, 96000, 192000 };

        // 存储用户选择的采样率设置
        private static readonly Dictionary<string, int> device_sample_rates = new Dictionary<string, int>();

        // 扩展方法：获取当前采样率
        public static int GetSampleRate(this AudioManager audioManager)
        {
            string deviceKey = audioManager.AudioDevice.Value;
            return device_sample_rates.GetValueOrDefault(deviceKey, 44100);

            // 如果没有保存的设置，返回默认值
        }

        // 扩展方法：设置采样率
        public static void SetSampleRate(this AudioManager audioManager, int sampleRate)
        {
            string deviceKey = audioManager.AudioDevice.Value;
            device_sample_rates[deviceKey] = sampleRate;

            // 解析当前选择的设备来确定输出模式
            (var mode, string deviceName, int? asioIndex) = parseSelection(audioManager.AudioDevice.Value, audioManager.UseExperimentalWasapi.Value);

            // 对于ASIO设备，设置首选采样率以便在设备初始化时使用
            if (mode == AudioOutputMode.Asio)
            {
                audioManager.SetPreferredAsioSampleRate(sampleRate);
                // ASIO设备的采样率在初始化时决定，不需要运行时设置
                return;
            }

            // 尝试实际设置采样率（仅对非ASIO设备）
            trySetActualSampleRate(audioManager, sampleRate);
        }

        // 扩展方法：获取支持的采样率列表
        public static IEnumerable<int> GetSupportedSampleRates(this AudioManager audioManager, string deviceName)
        {
            // 解析设备名称来确定输出模式
            (var mode, string parsedDeviceName, int? asioIndex) = parseSelection(deviceName, audioManager.UseExperimentalWasapi.Value);

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
                            if (supportedSampleRatesProperty.GetValue(null) is IEnumerable<double> rates)
                                return rates.Select(r => (int)r);
                        }

                        return common_sample_rates;

                    case AudioOutputMode.WasapiExclusive:
                    case AudioOutputMode.WasapiShared:
                        // 对于WASAPI设备，返回常见的采样率
                        return common_sample_rates;

                    case AudioOutputMode.Default:
                    default:
                        // 对于默认BASS设备，返回常见的采样率
                        return common_sample_rates;
                }
            }
            catch
            {
                // 如果获取失败，返回默认列表
                return common_sample_rates;
            }
        }

        // 扩展方法：设置ASIO设备初始化事件监听器
        public static void SetupAsioSampleRateSync(this AudioManager audioManager, Action<int> onSampleRateChanged)
        {
            audioManager.OnAsioDeviceInitialized += sampleRate =>
            {
                int intSampleRate = (int)sampleRate;
                onSampleRateChanged(intSampleRate);

                // 更新设备采样率缓存
                string deviceKey = audioManager.AudioDevice.Value;
                device_sample_rates[deviceKey] = intSampleRate;
            };
        }

        // 尝试设置实际的采样率
        private static void trySetActualSampleRate(AudioManager audioManager, int sampleRate)
        {
            // 解析当前选择的设备来确定输出模式
            (var mode, string deviceName, int? asioIndex) = parseSelection(audioManager.AudioDevice.Value, audioManager.UseExperimentalWasapi.Value);

            try
            {
                switch (mode)
                {
                    case AudioOutputMode.Asio:
                        // 对于ASIO设备，尝试通过反射设置采样率
                        var bassAsioType = typeof(AudioManager).Assembly.GetType("osu.Framework.Audio.Asio.BassAsio");
                        var setRateMethod = bassAsioType?.GetMethod("SetRate", BindingFlags.Public | BindingFlags.Static);
                        if (setRateMethod != null) setRateMethod.Invoke(null, new object[] { (double)sampleRate });
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
            if (tryParseSuffixed(selection, "(ASIO)", out baseName)) return (AudioOutputMode.Asio, baseName, null);

            // 其他情况默认为默认模式
            return (AudioOutputMode.Default, selection, null);
        }

        // 尝试从后缀解析设备名称
        private static bool tryParseSuffixed(string value, string suffix, out string baseName)
        {
            baseName = string.Empty;

            if (value.EndsWith(suffix, StringComparison.Ordinal))
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
            Asio
        }
    }
}
