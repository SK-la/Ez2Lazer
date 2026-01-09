// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework;
using osu.Framework.Audio;
using osu.Framework.Logging;

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
            Logger.Log($"Setting sample rate to {sampleRate}Hz", LoggingTarget.Runtime, LogLevel.Debug);
            // 使用静态 preferred 采样率，避免触发 ValueChanged
            AudioManager.SetPreferredAsioSampleRate(sampleRate);
            // 设置 unified sample rate
            audioManager.SampleRate.Value = sampleRate;
        }

        // 扩展方法：设置ASIO设备初始化事件监听器
        public static void SetupAsioSampleRateSync(this AudioManager audioManager, Action<int> onSampleRateChanged)
        {
            audioManager.OnAsioDeviceInitialized += sampleRate =>
            {
                int intSampleRate = (int)sampleRate;
                Logger.Log($"ASIO device initialized with sample rate {intSampleRate}Hz", LoggingTarget.Runtime, LogLevel.Debug);
                onSampleRateChanged(intSampleRate);

                // 不要更新统一的采样率设置，避免覆盖用户设置导致循环
                // audioManager.SampleRate.Value = intSampleRate;
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
