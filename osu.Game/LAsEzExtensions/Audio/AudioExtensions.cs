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
        // 固定采样率列表，优先使用48kHz
        public static readonly int[] COMMON_SAMPLE_RATES = { 48000, 44100, 96000, 192000 };

        // 扩展方法：获取当前采样率
        public static int GetSampleRate(this AudioManager audioManager)
        {
            // Use the unified sample rate from AudioManager
            return audioManager.SampleRate.Value;
        }

        // 扩展方法：设置采样率
        public static void SetSampleRate(this AudioManager audioManager, int sampleRate)
        {
            // Set both the unified sample rate and ASIO-specific sample rate
            audioManager.SampleRate.Value = sampleRate;
        }

        // 扩展方法：获取支持的采样率列表
        public static IEnumerable<int> GetSupportedSampleRates(this AudioManager audioManager, string deviceName)
        {
            // 解析设备名称来确定输出模式
            (var mode, string parsedDeviceName, int? asioIndex) = parseSelection(deviceName, audioManager.UseExperimentalWasapi.Value);

            System.Diagnostics.Debug.WriteLine($"GetSupportedSampleRates: deviceName='{deviceName}', mode={mode}, parsedDeviceName='{parsedDeviceName}', asioIndex={asioIndex}");

            try
            {
                switch (mode)
                {
                    case AudioOutputMode.Asio:
                        // 对于ASIO设备，返回固定的常见采样率列表，因为实际支持的采样率是从这些中选择的
                        return COMMON_SAMPLE_RATES;

                    case AudioOutputMode.WasapiExclusive:
                    case AudioOutputMode.WasapiShared:
                    case AudioOutputMode.Default:
                    default:
                        // 对于其他设备，不需要采样率设置，返回空列表
                        return Enumerable.Empty<int>();
                }
            }
            catch
            {
                // 如果获取失败，返回默认列表
                return COMMON_SAMPLE_RATES;
            }
        }

        // 扩展方法：设置ASIO设备初始化事件监听器
        public static void SetupAsioSampleRateSync(this AudioManager audioManager, Action<int> onSampleRateChanged)
        {
            audioManager.OnAsioDeviceInitialized += sampleRate =>
            {
                int intSampleRate = (int)sampleRate;
                onSampleRateChanged(intSampleRate);

                // 更新统一的采样率设置和ASIO特定设置以反映实际使用的采样率
                audioManager.SampleRate.Value = intSampleRate;
            };
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
                int? index = null;

                if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
                {
                    try
                    {
                        // 通过反射获取AsioDeviceManager.AvailableDevices
                        var asioDeviceManagerType = typeof(AudioManager).Assembly.GetType("osu.Framework.Audio.Asio.AsioDeviceManager");
                        var availableDevicesProperty = asioDeviceManagerType?.GetProperty("AvailableDevices", BindingFlags.Public | BindingFlags.Static);

                        if (availableDevicesProperty != null)
                        {
                            if (availableDevicesProperty.GetValue(null) is IEnumerable<(int Index, string Name)> devices)
                            {
                                foreach (var device in devices)
                                {
                                    if (device.Name == baseName)
                                    {
                                        index = device.Index;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 如果反射失败，忽略错误
                    }
                }

                return (AudioOutputMode.Asio, baseName, index);
            }

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
