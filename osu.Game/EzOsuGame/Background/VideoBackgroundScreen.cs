// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;

namespace osu.Game.EzOsuGame.Background
{
    public partial class VideoBackgroundScreen : Graphics.Backgrounds.Background
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
                Loop = true,
            };

            video.FillMode = FillMode.Fill;
            video.FillAspectRatio = 1.0f * video.DrawSize.X / video.DrawSize.Y;

            AddInternal(video);
        }
    }

    public partial class StreamVideoBackgroundScreen : Graphics.Backgrounds.Background
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
