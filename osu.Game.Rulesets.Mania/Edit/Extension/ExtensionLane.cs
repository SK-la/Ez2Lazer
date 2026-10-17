// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// The scrolling surface of a single extension column: renders its notes and beat lines, and maps times to positions.
    /// </summary>
    /// <remarks>
    /// The mapping is taken from the editor's own <see cref="IScrollingInfo"/>, the same instance the playfield's columns
    /// use, so positions match the playable objects without measuring or converting between screen spaces. Only the
    /// surface's own height enters the calculation.
    /// </remarks>
    public partial class ExtensionLane : CompositeDrawable
    {
        private const float preview_height = 8;
        private const float preview_opacity = 0.4f;

        private readonly ExtensionColumn column;
        private readonly ExtensionArea area;

        private readonly Dictionary<ExtensionNote, DrawableExtensionNote> noteDrawables = new Dictionary<ExtensionNote, DrawableExtensionNote>();

        private readonly Container lineContainer;
        private readonly Container noteContainer;
        private readonly Box draftBox;
        private readonly Box previewBox;

        private IBindable<double> timeRange = null!;
        private IBindable<ScrollingDirection> direction = null!;
        private IBindable<IScrollAlgorithm> algorithm = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        /// <summary>
        /// The surface beat lines are drawn on. Registered with the editor's snap grid by the composer.
        /// </summary>
        public Container LineContainer => lineContainer;

        /// <summary>
        /// Whether time advances towards the bottom of the surface.
        /// </summary>
        public bool ScrollsDown => direction.Value == ScrollingDirection.Down || direction.Value == ScrollingDirection.Right;

        public ExtensionLane(ExtensionColumn column, ExtensionArea area)
        {
            this.column = column;
            this.area = area;

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                lineContainer = new Container { RelativeSizeAxes = Axes.Both },
                noteContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new[]
                    {
                        draftBox = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = preview_height,
                            Colour = Colour4.White.Opacity(preview_opacity),
                            Alpha = 0,
                        },
                    },
                },
                previewBox = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = preview_height,
                    Colour = Colour4.White.Opacity(preview_opacity),
                    Alpha = 0,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            timeRange = scrollingInfo.TimeRange.GetBoundCopy();
            direction = scrollingInfo.Direction.GetBoundCopy();
            algorithm = scrollingInfo.Algorithm.GetBoundCopy();

            column.Notes.BindCollectionChanged(onNotesChanged, true);
        }

        /// <summary>
        /// The local Y of <paramref name="time"/>.
        /// </summary>
        public float OffsetAt(double time)
        {
            float position = algorithm.Value.PositionAt(time, editorClock.CurrentTime, timeRange.Value, DrawHeight);
            return ScrollsDown ? DrawHeight - position : position;
        }

        /// <summary>
        /// The time at a local Y.
        /// </summary>
        public double TimeAt(float localY)
        {
            float position = ScrollsDown ? DrawHeight - localY : localY;
            return algorithm.Value.TimeAt(position, editorClock.CurrentTime, timeRange.Value, DrawHeight);
        }

        /// <summary>
        /// The time under <paramref name="screenSpacePosition"/>, or <c>null</c> before the surface is laid out.
        /// </summary>
        public double? TimeAt(Vector2 screenSpacePosition)
        {
            if (DrawHeight <= 0)
                return null;

            return TimeAt(ToLocalSpace(screenSpacePosition).Y);
        }

        /// <summary>
        /// The note rendered under <paramref name="screenSpacePosition"/>, preferring the one closest to its own centre.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition)
        {
            ExtensionNote? result = null;
            float bestDistance = float.MaxValue;

            foreach (var (note, drawable) in noteDrawables)
            {
                if (!drawable.Contains(screenSpacePosition))
                    continue;

                float distance = Math.Abs(drawable.ToLocalSpace(screenSpacePosition).Y - drawable.DrawHeight / 2);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                result = note;
            }

            return result;
        }

        protected override void Update()
        {
            base.Update();

            if (DrawHeight <= 0)
                return;

            updateDraft();
            updatePreview();
        }

        private void updateDraft()
        {
            if (area.Draft is not { } draft || draft.column != column || !area.CanPlace)
            {
                if (draftBox.Alpha != 0)
                    draftBox.Alpha = 0;

                return;
            }

            float startY = OffsetAt(draft.start);
            float endY = OffsetAt(draft.end);

            draftBox.Y = Math.Min(startY, endY);
            draftBox.Height = Math.Max(preview_height, Math.Abs(endY - startY));
            draftBox.Alpha = 1;
        }

        private void updatePreview()
        {
            double? time = area.HoverColumn == column ? area.HoverTime : null;

            bool show = time != null
                        && area.CanPlace
                        && !area.IsInteracting
                        && !area.IsHoveringNote;

            if (!show)
            {
                if (previewBox.Alpha != 0)
                    previewBox.Alpha = 0;

                return;
            }

            previewBox.Y = OffsetAt(time!.Value) - preview_height;

            if (previewBox.Alpha != 1)
                previewBox.Alpha = 1;
        }

        private void onNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems!)
                        addNote((ExtensionNote)item!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems!)
                        removeNote((ExtensionNote)item!);
                    break;

                default:
                    foreach (var drawable in noteDrawables.Values)
                        noteContainer.Remove(drawable, true);

                    noteDrawables.Clear();

                    foreach (var note in column.Notes)
                        addNote(note);

                    break;
            }
        }

        private void addNote(ExtensionNote note)
        {
            var drawable = new DrawableExtensionNote(note, this);

            noteDrawables[note] = drawable;
            noteContainer.Add(drawable);
        }

        private void removeNote(ExtensionNote note)
        {
            if (!noteDrawables.Remove(note, out var drawable))
                return;

            noteContainer.Remove(drawable, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                column.Notes.CollectionChanged -= onNotesChanged;

            base.Dispose(isDisposing);
        }
    }
}
