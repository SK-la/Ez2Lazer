// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
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

namespace osu.Game.EzOsuGame.Playback
{
    /// <summary>
    /// Component-layer playback bar: play/pause + seekable progress, bound to an <see cref="EzPlaybackController"/>.
    /// The controller owns the functional layer; this drawable only renders its transport state and forwards input.
    /// Width grows with the menu bar but the left edge never crosses screen center
    /// (when parented in a right-aligned header FillFlow with mode/skin siblings).
    /// </summary>
    public partial class EzPlaybackBar : Container
    {
        private const float min_width = 180;

        // 播放键列宽与时间码列宽都取固定值：AutoSize 列每帧按子项包围盒重新量宽，时间码换字（比例字体数字
        // 宽度不同）与按钮 hover/点击缩放都会带动列宽变化，进而让中间进度条反复改宽、右侧数字左右跳。
        private static readonly float control_column_width = EzUIStyleV1.SceneBarControlHeight();
        private const float time_column_width = 84;

        private readonly EzPlaybackController controller;

        private readonly ProgressBar timeline;
        private readonly IconButton playPauseButton;
        private readonly OsuSpriteText progressText;

        private readonly IBindable<bool> isPlaying;
        private readonly IBindable<double> minTime;
        private readonly IBindable<double> maxTime;
        private readonly IBindable<double> currentTime;

        public EzPlaybackBar(EzPlaybackController controller)
        {
            this.controller = controller;

            Width = min_width;
            RelativeSizeAxes = Axes.Y;

            // Three columns: fixed play button, fluid timeline, fixed time text. A GridContainer is used rather
            // than a horizontal fill flow because the timeline must take the remaining width, which a fill flow
            // cannot express without giving every child the same anchor. Both side columns are Absolute so their
            // width never depends on their content.
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, control_column_width),
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, time_column_width),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        playPauseButton = new IconButton
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(control_column_width),
                            Icon = FontAwesome.Regular.PlayCircle,
                            Action = controller.TogglePlayback,
                        },
                        timeline = new ProgressBar(true)
                        {
                            RelativeSizeAxes = Axes.Both,
                            BackgroundColour = Color4.Black.Opacity(0.45f),
                            FillColour = Color4.CornflowerBlue,
                        },
                        progressText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Margin = new MarginPadding { Right = 8 },
                            // fixedWidth keeps every digit the same advance, so the timecode never reflows mid-playback.
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold, fixedWidth: true),
                            Colour = Color4.White,
                            Text = "00:00.000",
                        },
                    },
                },
            };

            timeline.OnSeek = time =>
            {
                controller.Seek(time);
                progressText.Text = formatTime(time);
            };
            timeline.OnCommit = controller.Seek;

            isPlaying = controller.IsPlaying.GetBoundCopy();
            minTime = controller.MinTime.GetBoundCopy();
            maxTime = controller.MaxTime.GetBoundCopy();
            currentTime = controller.CurrentTime.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            isPlaying.BindValueChanged(p => playPauseButton.Icon = p.NewValue ? FontAwesome.Regular.PauseCircle : FontAwesome.Regular.PlayCircle, true);
            minTime.BindValueChanged(_ => applyRange());
            maxTime.BindValueChanged(_ => applyRange());
            currentTime.BindValueChanged(_ => updateProgressDisplay());
        }

        protected override void Update()
        {
            base.Update();

            updateWidth();
        }

        private void updateWidth()
        {
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

        private void applyRange()
        {
            timeline.EndTime = Math.Max(maxTime.Value, minTime.Value + 1);
            updateProgressDisplay();
        }

        private void updateProgressDisplay()
        {
            // While dragging, the bar owns the displayed position; don't fight it with the controller tick.
            if (timeline.Seeking)
                return;

            double time = currentTime.Value;
            timeline.CurrentTime = time;
            progressText.Text = formatTime(time);
        }

        private static string formatTime(double time)
        {
            var span = TimeSpan.FromMilliseconds(Math.Max(0, time));
            return $"{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
        }
    }
}
