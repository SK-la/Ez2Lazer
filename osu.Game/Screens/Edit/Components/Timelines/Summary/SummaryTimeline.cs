// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.LAsEzExtensions.Screens.Edit;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osuTK;

namespace osu.Game.Screens.Edit.Components.Timelines.Summary
{
    /// <summary>
    /// The timeline that sits at the bottom of the editor.
    /// </summary>
    public partial class SummaryTimeline : BottomBarContainer
    {
        private LoopIntervalDisplay loopInterval = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private LoopMarker loopStartMarker = null!;
        private LoopMarker loopEndMarker = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Background.Colour = colourProvider.Background6;

            Children = new Drawable[]
            {
                new Container
                {
                    Name = "centre line",
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background2,
                    Children = new Drawable[]
                    {
                        new Circle
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.Centre,
                            Size = new Vector2(5)
                        },
                        new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            EdgeSmoothness = new Vector2(0, 1),
                        },
                        new Circle
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.Centre,
                            Size = new Vector2(5)
                        },
                    }
                },
                new BreakPart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                loopInterval = new LoopIntervalDisplay
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                new KiaiPart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                new ControlPointPart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.4f
                },
                new BookmarkPart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.4f
                },
                new PreviewTimePart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                loopStartMarker = new LoopMarker(true)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                loopEndMarker = new LoopMarker(false)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                new MarkerPart { RelativeSizeAxes = Axes.Both },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            loopStartMarker.TimeAtX = x => x / DrawWidth * editorClock.TrackLength;
            loopEndMarker.TimeAtX = x => x / DrawWidth * editorClock.TrackLength;

            loopStartMarker.TimeChanged += time => editorClock.SetLoopStartTime(time);
            loopEndMarker.TimeChanged += time => editorClock.SetLoopEndTime(time);

            editorClock.LoopStartTime.BindValueChanged(_ => updateLoopInterval());
            editorClock.LoopEndTime.BindValueChanged(_ => updateLoopInterval());
            editorClock.LoopEnabled.BindValueChanged(enabled =>
            {
                loopInterval.FadeTo(enabled.NewValue ? 1 : 0, 200, Easing.OutQuint);
                loopStartMarker.FadeTo(enabled.NewValue ? 1 : 0, 200, Easing.OutQuint);
                loopEndMarker.FadeTo(enabled.NewValue ? 1 : 0, 200, Easing.OutQuint);
            });
        }

        protected override void Update()
        {
            base.Update();

            loopStartMarker.X = (float)(editorClock.LoopStartTime.Value / editorClock.TrackLength * DrawWidth);
            loopEndMarker.X = (float)(editorClock.LoopEndTime.Value / editorClock.TrackLength * DrawWidth);
        }

        private void updateLoopInterval()
        {
            float startX = (float)(editorClock.LoopStartTime.Value / editorClock.TrackLength * DrawWidth);
            float endX = (float)(editorClock.LoopEndTime.Value / editorClock.TrackLength * DrawWidth);
            loopInterval.UpdateInterval(startX, endX);
        }
    }
}
