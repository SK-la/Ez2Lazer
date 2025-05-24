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
        private void load(OsuConfigManager config)
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

            var ezskinSettings = new EzSkinSettings();
            GlobalConfigStore.EZConfig = ezskinSettings;
        }
    }

    public static class GlobalConfigStore
    {
        public static OsuConfigManager? Config { get; set; }
        public static EzSkinSettings? EZConfig { get; set; }
        public static event Action? OnRefresh;

        public static void TriggerRefresh() => OnRefresh?.Invoke();
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
