// VideoBackgroundScreen.cs

using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;
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
        private void load()
        {
            var video = new Video(videoPath)
            {
                RelativeSizeAxes = Axes.Both,
                Loop = true
            };
            AddInternal(video);
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
