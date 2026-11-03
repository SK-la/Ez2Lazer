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
using osu.Game.Graphics;
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
    /// The surface's own height is copied from the playfield column it mirrors (see <see cref="DrawableExtensionColumn"/>),
    /// so this mapping and the playfield's scrolling container produce the same screen positions for the same time.
    /// </remarks>
    public partial class ExtensionLane : CompositeDrawable
    {
        /// <summary>
        /// Height of the placement highlight. Equal to the least note height so the highlight marks exactly the slot the
        /// note will occupy; only its shape and colour distinguish it from a placed note.
        /// </summary>
        private const float preview_height = 6;

        private const float draft_opacity = 0.5f;
        private const float grid_line_height = 1.5f;

        /// <summary>
        /// The least height a note is drawn with, so that a tap or a very short hold stays visible.
        /// </summary>
        public const float MINIMUM_NOTE_HEIGHT = 10;

        private readonly ExtensionColumn column;
        private readonly ExtensionArea area;

        private readonly Dictionary<ExtensionNote, DrawableExtensionNote> noteDrawables = new Dictionary<ExtensionNote, DrawableExtensionNote>();
        private readonly List<Box> gridLineBoxes = new List<Box>();

        private readonly Container noteContainer;
        private readonly Box draftBox;
        private readonly Circle previewPiece;

        private IBindable<double> timeRange = null!;
        private IBindable<ScrollingDirection> direction = null!;
        private IBindable<IScrollAlgorithm> algorithm = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        /// <summary>
        /// The surface beat lines are drawn on, below the notes.
        /// </summary>
        public Container LineContainer { get; }

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
                LineContainer = new Container { RelativeSizeAxes = Axes.Both },
                noteContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new[]
                    {
                        draftBox = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = preview_height,
                            Alpha = 0,
                        },
                    },
                },
                // Mirrors the playfield's placement piece: a yellow highlight instead of a note silhouette, so the cursor's
                // slot never reads as a note that is already there.
                previewPiece = new Circle
                {
                    RelativeSizeAxes = Axes.X,
                    Height = preview_height,
                    Alpha = 0,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo, OsuColour colours)
        {
            timeRange = scrollingInfo.TimeRange.GetBoundCopy();
            direction = scrollingInfo.Direction.GetBoundCopy();
            algorithm = scrollingInfo.Algorithm.GetBoundCopy();

            previewPiece.Colour = colours.Yellow;
            draftBox.Colour = colours.Yellow;

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
        /// The local top edge and height of a note starting at <paramref name="time"/> with <paramref name="duration"/>.
        /// </summary>
        /// <remarks>
        /// Placed the way the playfield places its own drawables: the note's hit edge sits on its start row and the body
        /// extends towards the side the note arrives from. A tap or a very short hold therefore grows away from the row
        /// instead of straddling it, which keeps the note, its beat line and its selection outline on the same row.
        /// </remarks>
        public (float top, float height) SpanOf(double time, double duration)
        {
            float head = OffsetAt(time);
            float tail = OffsetAt(time + duration);

            float top = Math.Min(head, tail);
            float height = Math.Max(head, tail) - top;

            if (height >= MINIMUM_NOTE_HEIGHT)
                return (top, height);

            return ScrollsDown
                ? (head - MINIMUM_NOTE_HEIGHT, minimum_note_height: MINIMUM_NOTE_HEIGHT)
                : (head, minimum_note_height: MINIMUM_NOTE_HEIGHT);
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

            updateGridLines();
            updateDraft();
            updatePreview();
        }

        /// <summary>
        /// Re-renders the beat divisor lines supplied by the area, which decides whether the cursor or a selection owns them.
        /// </summary>
        private void updateGridLines()
        {
            IReadOnlyList<ExtensionSnapLine>? lines = area.LinesFor(column);
            int count = lines?.Count ?? 0;

            while (gridLineBoxes.Count < count)
            {
                var box = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = grid_line_height,
                };

                gridLineBoxes.Add(box);
                LineContainer.Add(box);
            }

            for (int i = 0; i < gridLineBoxes.Count; i++)
            {
                var box = gridLineBoxes[i];

                if (i >= count)
                {
                    if (box.Alpha != 0)
                        box.Alpha = 0;

                    continue;
                }

                var line = lines![i];

                box.Y = OffsetAt(line.Time) - grid_line_height / 2;
                box.Colour = line.Colour;
                box.Alpha = line.Alpha;
            }
        }

        private void updateDraft()
        {
            if (area.Draft is not { } draft || draft.column != column || !area.CanPlace)
            {
                if (draftBox.Alpha != 0)
                    draftBox.Alpha = 0;

                return;
            }

            var span = SpanOf(draft.start, draft.end - draft.start);

            draftBox.Y = span.top;
            draftBox.Height = span.height;

            if (draftBox.Alpha != draft_opacity)
                draftBox.Alpha = draft_opacity;
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
                if (previewPiece.Alpha != 0)
                    previewPiece.Alpha = 0;

                return;
            }

            var span = SpanOf(time!.Value, 0);

            // Centred on the slot the note would occupy, like the playfield's placement piece is offset by half a note.
            previewPiece.Y = span.top + span.height / 2 - preview_height / 2;

            if (previewPiece.Alpha != 1)
                previewPiece.Alpha = 1;
        }

        private void onNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (object item in e.NewItems!)
                        addNote((ExtensionNote)item!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (object item in e.OldItems!)
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
