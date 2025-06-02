// VideoBackgroundScreen.cs

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;

namespace osu.Game.Screens.Backgrounds
{
    public partial class VideoBackgroundScreen : Background
    {
        private readonly string videoPath;

        public VideoBackgroundScreen(string videoPath)
        {
            this.videoPath = videoPath;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, EzSkinSettingsManager ezSkinConfig)
        {
            var video = new Video(videoPath)
            {
                RelativeSizeAxes = Axes.Both,
                Loop = true,
            };

            video.FillMode = FillMode.Fill;
            video.FillAspectRatio = 1.0f * video.DrawSize.X / video.DrawSize.Y;

            AddInternal(video);

            //下面只用于注册全局设置
            GlobalConfigStore.Config = config;
            GlobalConfigStore.EZConfig = ezSkinConfig;
            GlobalConfigStore.RegisterConfigListeners();
        }
    }

    public static class GlobalConfigStore
    {
        public static OsuConfigManager? Config { get; set; }
        public static EzSkinSettingsManager? EZConfig { get; set; }
        public static event Action? OnRefresh;

        public static void TriggerRefresh() => OnRefresh?.Invoke();

        public static void RegisterConfigListeners()
        {
            if (Config == null || EZConfig == null) return;

            // 监听常用的配置项
            Config.GetBindable<double>(OsuSetting.ColumnWidth).ValueChanged += _ => TriggerRefresh();
            Config.GetBindable<double>(OsuSetting.SpecialFactor).ValueChanged += _ => TriggerRefresh();
            EZConfig.GetBindable<double>(EzSkinSetting.VirtualHitPosition).ValueChanged += _ => TriggerRefresh();

            // 可以根据需要添加更多配置项的监听
        }
    }

    public partial class StreamVideoBackgroundScreen : Background
    {
        private readonly Stream videoStream;

        public StreamVideoBackgroundScreen(Stream videoStream)
        {
            this.videoStream = videoStream;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var video = new Video(videoStream)
            {
                RelativeSizeAxes = Axes.Both,
                Loop = true
            };
            AddInternal(video);
        }
    }
}
