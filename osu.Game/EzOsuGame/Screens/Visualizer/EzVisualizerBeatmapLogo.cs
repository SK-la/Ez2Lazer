// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Screens.Visualizer
{
    public partial class EzVisualizerBeatmapLogo : CompositeDrawable
    {
        public const float BASE_SIZE = 350;
        private const float progress_padding = 10;

        /// <summary>
        /// Relative radius band (from centre) that accepts seek input on the progress ring.
        /// </summary>
        private const float seek_inner_ratio = 0.78f;

        private const float seek_outer_ratio = 1.02f;

        public Color4 ProgressColour
        {
            get => progress.Colour;
            set => progress.Colour = value;
        }

        private IBindable<WorkingBeatmap> beatmap = null!;

        private CircularProgress progress = null!;
        private Container backgroundLayer = null!;
        private BeatmapBackground? currentBackground;
        private bool seeking;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        public EzVisualizerBeatmapLogo()
        {
            Size = new Vector2(BASE_SIZE);
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<WorkingBeatmap> workingBeatmap)
        {
            Origin = Anchor.Centre;
            Anchor = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Child = backgroundLayer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(progress_padding),
                    Child = progress = new CircularProgress
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        InnerRadius = 0.03f,
                        Colour = Color4.White,
                    }
                }
            };

            beatmap = workingBeatmap.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(onBeatmapChanged, true);
        }

        protected override void Update()
        {
            base.Update();

            if (seeking)
                return;

            var track = musicController.CurrentTrack;
            progress.Progress = track.Length <= 0 ? 0 : track.CurrentTime / track.Length;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            var local = ToLocalSpace(screenSpacePos);
            float radius = DrawSize.X / 2f;
            float distance = Vector2.Distance(local, DrawSize / 2f);
            return distance >= radius * seek_inner_ratio && distance <= radius * seek_outer_ratio;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left || !canSeek())
                return false;

            seeking = true;
            seekTo(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => seeking;

        protected override void OnDrag(DragEvent e)
        {
            if (seeking)
                seekTo(e.ScreenSpaceMousePosition);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button != MouseButton.Left)
                return;

            seeking = false;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            seeking = false;
            base.OnDragEnd(e);
        }

        private bool canSeek() => musicController.AllowTrackControl.Value && musicController.CurrentTrack.Length > 0;

        private void seekTo(Vector2 screenSpacePosition)
        {
            if (!canSeek())
                return;

            var local = ToLocalSpace(screenSpacePosition);
            var delta = local - DrawSize / 2f;

            // 0 at top, increasing clockwise to match CircularProgress.
            float angle = MathF.Atan2(delta.X, -delta.Y);
            if (angle < 0)
                angle += MathF.Tau;

            double progressValue = angle / MathF.Tau;
            progress.Progress = progressValue;
            musicController.SeekTo(progressValue * musicController.CurrentTrack.Length);
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> e)
        {
            LoadComponentAsync(new BeatmapBackground(e.NewValue)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.DarkGray,
            }, newBackground =>
            {
                if (beatmap.Value != e.NewValue)
                {
                    newBackground.Dispose();
                    return;
                }

                currentBackground?.FadeOut(300, Easing.OutQuint);
                currentBackground?.Expire();

                currentBackground = newBackground;
                backgroundLayer.Add(newBackground);
                newBackground.FadeInFromZero(300, Easing.OutQuint);
            });
        }
    }
}
