// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Localization
{
    public class EzSettingsAudioString
    {
         #region 音频设备设置

        public static readonly EzLocalizationManager.EzLocalisableString AUDIO_DEVICE_OUTPUT_HINT = new EzLocalizationManager.EzLocalisableString(
            "ASIO 处于测试阶段！"
            + "\n对于虚拟音频驱动，如VoiceMeeter，可能需要先切换到物理输出设备，激活驱动后，之后再切换回VM。"
            + "\n请不要认为虚拟ASIO比WASAPI更好，如果没有声音请尝试重启。",
            "ASIO is testing! "
            + "\nFor virtual audio drivers like VoiceMeeter, you may need to switch to a physical output device first, activate the driver, and then switch back to VM."
            + "\nPlease do not assume virtual ASIO is better than WASAPI, and try restarting if there is no sound.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_SAMPLE_RATE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 输出格式（内部 PCM）",
            "ASIO Output Format (Internal PCM)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_SAMPLE_RATE_HINT = new EzLocalizationManager.EzLocalisableString(
            "仅在关闭「外部 PCM」时生效。所有音频仍会混音到统一的输出采样率。"
            + "\n推荐 48000 Hz，次选 44100 Hz。",
            "Only applies when External PCM is off. All audio is still mixed to a single output sample rate."
            + "\n48000 Hz is recommended; 44100 Hz is the alternative.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_BUFFER_SIZE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 缓冲区大小（内部 PCM）",
            "ASIO Buffer Size (Internal PCM)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_BUFFER_SIZE_HINT = new EzLocalizationManager.EzLocalisableString(
            "仅在关闭「外部 PCM」时生效。数值越低延迟越低，过低可能爆音或无法启动。",
            "Only applies when External PCM is off. Lower values reduce latency but may crackle or fail to start.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_PASSTHROUGH_LABEL = new EzLocalizationManager.EzLocalisableString(
            "ASIO 外部 PCM（推荐）",
            "ASIO External PCM (Recommended)");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_PASSTHROUGH_HINT = new EzLocalizationManager.EzLocalisableString(
            "开启：输出采样率、位深与缓冲区由 ASIO 驱动控制面板决定，游戏不覆盖（外部 PCM）。"
            + "\n关闭：使用下方游戏内设置指定输出格式（内部 PCM，适用于无驱动面板的设备）。"
            + "\n无论哪种模式，多路音效都会在混音后以统一格式输出。",
            "On: sample rate, bit depth, and buffer follow the ASIO driver control panel; the game does not override (external PCM)."
            + "\nOff: use the in-game settings below (internal PCM; for devices without a driver panel)."
            + "\nIn both modes, multiple sounds are mixed to one output format.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_RELOAD_DRIVER_LABEL = new EzLocalizationManager.EzLocalisableString(
            "重新加载 ASIO 驱动",
            "Reload ASIO Driver");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_RELOAD_DRIVER_HINT = new EzLocalizationManager.EzLocalisableString(
            "释放本程序占用的音频设备后，按当前输出设备选择重新初始化，并重新读取驱动当前生效的格式与缓冲区。",
            "Releases audio resources held by the game, re-initialises the current output device, and re-reads the driver's active format and buffer settings.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_RELOAD_DRIVER_FAILED_NOTIFICATION = new EzLocalizationManager.EzLocalisableString(
            "ASIO 驱动重新加载失败。请确认驱动控制面板中的设置，或尝试重启游戏。",
            "ASIO driver reload failed. Check the driver control panel settings, or try restarting the game.");

        public static readonly EzLocalizationManager.EzLocalisableString ASIO_OUTPUT_UNAVAILABLE_NOTIFICATION = new EzLocalizationManager.EzLocalisableString(
            "ASIO 输出未能启动，当前没有声音。请尝试重启游戏；若仍失败，请关闭占用该 ASIO 驱动的其他程序，或切换到其他音频设备。",
            "ASIO output failed to start; there is no audio. Try restarting the game. If it still fails, close other apps using this ASIO driver or switch to another audio device.");

        #endregion
    }
}
