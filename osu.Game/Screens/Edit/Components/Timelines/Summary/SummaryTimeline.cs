// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Screens.Edit;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osu.Game.Utils;
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

        [Resolved(canBeNull: true)]
        private IBindable<IReadOnlyList<Mod>>? mods { get; set; }

        private ScheduledDelegate? pendingSync;

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
                    RelativeSizeAxes = Axes.Y,
                    Alpha = 0,
                },
                loopStartMarker = new LoopMarker(true)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                loopEndMarker = new LoopMarker(false)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
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
                new MarkerPart { RelativeSizeAxes = Axes.Both },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            float getTimelineWidth() => Content.ChildSize.X;

            float clampX(float x)
            {
                float w = getTimelineWidth();
                return Math.Clamp(x, -w / 2, w / 2);
            }

            float xAtTime(double time)
            {
                float w = getTimelineWidth();
                return (float)(time / editorClock.TrackLength * w - w / 2);
            }

            double timeAtX(float x)
            {
                float w = getTimelineWidth();
                x = clampX(x);
                return (x + w / 2) / w * editorClock.TrackLength;
            }

            loopStartMarker.ClampX = clampX;
            loopStartMarker.XAtTime = xAtTime;
            loopStartMarker.SnapTime = editorClock.GetSnappedTime;
            loopStartMarker.TimeAtX = timeAtX;

            loopEndMarker.ClampX = clampX;
            loopEndMarker.XAtTime = xAtTime;
            loopEndMarker.SnapTime = editorClock.GetSnappedTime;
            loopEndMarker.TimeAtX = timeAtX;

            loopStartMarker.TimeChanged += time => editorClock.SetLoopStartTime(time);
            loopEndMarker.TimeChanged += time => editorClock.SetLoopEndTime(time);

            editorClock.LoopStartTime.BindValueChanged(_ => updateLoopInterval());
            editorClock.LoopEndTime.BindValueChanged(_ => updateLoopInterval());
            editorClock.LoopStartTime.BindValueChanged(_ => scheduleSyncToMods());
            editorClock.LoopEndTime.BindValueChanged(_ => scheduleSyncToMods());
            editorClock.LoopEnabled.BindValueChanged(_ => scheduleSyncToMods());
            editorClock.LoopEnabled.BindValueChanged(enabled =>
            {
                loopInterval.FadeTo(enabled.NewValue ? 1 : 0, 200, Easing.OutQuint);
                // loopStartMarker.FadeTo(enabled.NewValue ? 1 : 0, 200, Easing.OutQuint);
                // loopEndMarker.FadeTo(enabled.NewValue ? 1 : 0, 200, Easing.OutQuint);
            });
        }

        protected override void Update()
        {
            base.Update();

            float timelineWidth = Content.ChildSize.X;

            if (!loopStartMarker.IsDragged)
                loopStartMarker.X = (float)(editorClock.LoopStartTime.Value / editorClock.TrackLength * timelineWidth - timelineWidth / 2);
            if (!loopEndMarker.IsDragged)
                loopEndMarker.X = (float)(editorClock.LoopEndTime.Value / editorClock.TrackLength * timelineWidth - timelineWidth / 2);
        }

        private void updateLoopInterval()
        {
            float timelineWidth = Content.ChildSize.X;
            float startX = (float)(editorClock.LoopStartTime.Value / editorClock.TrackLength * timelineWidth - timelineWidth / 2);
            float endX = (float)(editorClock.LoopEndTime.Value / editorClock.TrackLength * timelineWidth - timelineWidth / 2);
            loopInterval.UpdateInterval(startX, endX);
        }

        private void scheduleSyncToMods()
        {
            pendingSync?.Cancel();
            pendingSync = Scheduler.AddDelayed(syncLoopRangeToMods, 200);
        }

        private void syncLoopRangeToMods()
        {
            double start = editorClock.LoopStartTime.Value;
            double end = editorClock.LoopEndTime.Value;

            if (end <= start)
                return;

            // Always update the global session store, regardless of loop enabled state.
            LoopTimeRangeStore.Set(start, end);

            if (mods == null)
                return;

            bool applied = false;

            foreach (var rangeMod in ModUtils.FlattenMods(mods.Value).OfType<ILoopTimeRangeMod>())
            {
                rangeMod.SetLoopTimeRange(start, end);
                applied = true;
            }

            // If the user returns to song select without restarting, the mod overlay may already be alive.
            // It only reacts to SelectedMods bindable value changes, so force a value update to propagate the changed settings.
            if (applied && mods is Bindable<IReadOnlyList<Mod>> writableMods)
                writableMods.Value = writableMods.Value.ToArray();
        }
    }
}
