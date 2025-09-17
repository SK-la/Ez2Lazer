// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// A vertical timeline displaying the waveform along the Y axis.
    /// Contains no interactive elements or hit objects, only the waveform and bar lines.
    /// Synchronizes zoom and scroll position with the main horizontal timeline.
    /// </summary>
    public partial class EzVerticalTimeline : CompositeDrawable
    {
        private WaveformGraph waveform = null!;
        private Box background = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private Timeline timeline { get; set; } = null!;

        public EzVerticalTimeline()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0.5f;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        waveform = new WaveformGraph
                        {
                            RelativeSizeAxes = Axes.Both,
                            BaseColour = colours.Blue.Opacity(0.2f),
                            LowColour = colours.BlueLighter,
                            MidColour = colours.BlueDark,
                            HighColour = colours.BlueDarker,
                            Rotation = 90
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateWaveform();
            beatmap.BindValueChanged(_ => updateWaveform(), true);
        }

        private void updateWaveform()
        {
            waveform.Waveform = beatmap.Value.Waveform;
            Scheduler.AddOnce(applyVisualOffset, beatmap);
        }

        private void applyVisualOffset(IBindable<WorkingBeatmap> beatmap)
        {
            waveform.RelativePositionAxes = Axes.Y;

            if (beatmap.Value.Track.Length > 0)
                waveform.Y = -(float)(Editor.WAVEFORM_VISUAL_OFFSET / beatmap.Value.Track.Length);
            else
            {
                // sometimes this can be the case immediately after a track switch.
                // reschedule with the hope that the track length eventually populates.
                Scheduler.AddOnce(applyVisualOffset, beatmap);
            }
        }

        private void updateWaveformResolution()
        {
            // Update the waveform resolution based on the main timeline's zoom
            waveform.Resolution = (float)timeline.VisibleRange / DrawHeight;
        }

        private void onTimelineScroll(ValueChangedEvent<double> scroll)
        {
            // Update the waveform position to match the main timeline's scroll position
            // Convert horizontal scroll position to vertical offset
            float scrollRatio = (float)(scroll.NewValue / timeline.DrawWidth);
            waveform.Y = -scrollRatio * DrawHeight
                         - (float)(Editor.WAVEFORM_VISUAL_OFFSET / Math.Max(1, beatmap.Value.Track.Length));
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // if this is not a precision scroll event, let the editor handle the seek itself (for snapping support)
            if (!e.AltPressed && !e.IsPrecise)
                return false;

            return base.OnScroll(e);
        }
    }
}
