// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorPlaybackControls : Container
    {
        private readonly ProgressBar timeline;
        private readonly IconButton playPauseButton;
        private readonly OsuSpriteText progressText;

        private bool isPlaying;

        public Action<double>? OnSeek { get; set; }

        public Action<bool>? OnPlayStateChanged { get; set; }

        public EzSkinEditorPlaybackControls()
        {
            RelativeSizeAxes = Axes.X;
            Height = 48;

            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 8, Vertical = 6 },
                    Children = new Drawable[]
                    {
                        playPauseButton = new IconButton
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 32,
                            Height = 32,
                            Icon = FontAwesome.Regular.PauseCircle,
                            Action = togglePlayPause,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 40, Right = 72 },
                            Children = new Drawable[]
                            {
                                timeline = new ProgressBar(true)
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    BackgroundColour = Color4.Black.Opacity(0.45f),
                                    FillColour = Color4.CornflowerBlue,
                                },
                            },
                        },
                        progressText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                            Colour = Color4.White,
                            Text = "00:00.000",
                        },
                    },
                },
            };

            timeline.OnSeek = time => OnSeek?.Invoke(time);
            timeline.OnCommit = time => OnSeek?.Invoke(time);
        }

        public void SetRange(double minTime, double maxTime)
        {
            timeline.EndTime = maxTime;
            timeline.CurrentTime = minTime;
        }

        public void SetCurrentTime(double time, bool seeking = false)
        {
            if (!timeline.Seeking || seeking)
            {
                timeline.CurrentTime = time;
                progressText.Text = formatTime(time);
            }
        }

        public void SetPlaying(bool playing)
        {
            isPlaying = playing;
            playPauseButton.Icon = playing ? FontAwesome.Regular.PauseCircle : FontAwesome.Regular.PlayCircle;
        }

        private void togglePlayPause()
        {
            isPlaying = !isPlaying;
            SetPlaying(isPlaying);
            OnPlayStateChanged?.Invoke(isPlaying);
        }

        private static string formatTime(double time)
        {
            var span = TimeSpan.FromMilliseconds(Math.Max(0, time));
            return $"{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
        }
    }
}
