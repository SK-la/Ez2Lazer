// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Menu-bar playback cluster: play/pause + seekable progress.
    /// Width grows with the menu bar but the left edge never crosses screen center
    /// (when parented in a right-aligned header FillFlow with mode/skin siblings).
    /// </summary>
    public partial class EzSkinEditorPlaybackControls : Container
    {
        private const float min_width = 180;

        private readonly ProgressBar timeline;
        private readonly IconButton playPauseButton;
        private readonly OsuSpriteText progressText;

        private bool isPlaying;

        public Action<double>? OnSeek { get; set; }

        /// <summary>Invoked when the play/pause control is clicked; host should toggle real playback state.</summary>
        public Action? OnPlayPauseClicked { get; set; }

        public EzSkinEditorPlaybackControls()
        {
            Width = min_width;
            RelativeSizeAxes = Axes.Y;

            float controlHeight = EzUIStyleV1.SceneBarControlHeight();

            Children = new Drawable[]
            {
                playPauseButton = new IconButton
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(controlHeight),
                    Icon = FontAwesome.Regular.PlayCircle,
                    Action = () => OnPlayPauseClicked?.Invoke(),
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = controlHeight + 8, Right = 72 },
                    Child = timeline = new ProgressBar(true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        BackgroundColour = Color4.Black.Opacity(0.45f),
                        FillColour = Color4.CornflowerBlue,
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
            };

            timeline.OnSeek = time => OnSeek?.Invoke(time);
            timeline.OnCommit = time => OnSeek?.Invoke(time);
        }

        protected override void Update()
        {
            base.Update();

            if (Parent is not FillFlowContainer flow || flow.Parent == null)
                return;

            float barWidth = flow.Parent.DrawWidth;
            if (barWidth <= 0)
                return;

            float trailing = flow.Padding.TotalHorizontal;

            foreach (var child in flow.Children)
            {
                if (child == this || !child.IsPresent)
                    continue;

                trailing += child.LayoutSize.X + flow.Spacing.X;
            }

            // Right-aligned header: left edge at ≥ screen center ⇒ width ≤ half − siblings.
            float target = Math.Max(min_width, barWidth * 0.5f - trailing);

            if (!Precision.AlmostEquals(Width, target, 0.5f))
                Width = target;
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

        private static string formatTime(double time)
        {
            var span = TimeSpan.FromMilliseconds(Math.Max(0, time));
            return $"{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
        }
    }
}
