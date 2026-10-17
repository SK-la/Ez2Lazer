// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Renders the notes, beat divisor lines and placement preview of a single extension column.
    /// </summary>
    /// <remarks>
    /// Times are mapped through the playfield column's own <see cref="ScrollingHitObjectContainer"/>, so the column scrolls
    /// with exactly the playfield's speed and phase instead of a derived approximation. The layer's geometry is kept equal
    /// to that container (see <see cref="DrawableExtensionColumn"/>), which makes both bottoms — and therefore the hit line — coincide.
    /// </remarks>
    public partial class ExtensionNoteLayer : CompositeDrawable
    {
        private const float minimum_height = 8;
        private const float grid_line_height = 1.5f;
        private const float preview_opacity = 0.4f;

        private const float hold_body_opacity = 0.45f;
        private const float hold_tail_opacity = 0.25f;
        private const float tap_opacity = 0.85f;

        private readonly ExtensionColumn column;
        private readonly ExtensionArea area;

        private readonly Container lineContainer;
        private readonly Container noteContainer;
        private readonly Container tailContainer;
        private readonly Container headContainer;
        private readonly Container overlayContainer;

        private readonly Dictionary<ExtensionNote, NoteVisual> noteVisuals = new Dictionary<ExtensionNote, NoteVisual>();
        private readonly List<Box> gridLineBoxes = new List<Box>();
        private readonly Box previewBox;

        private IEzSkinInfo? ezSkinInfo;

        public ExtensionNoteLayer(ExtensionColumn column, ExtensionArea area)
        {
            this.column = column;
            this.area = area;

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                // Grid lines belong under the notes, so they get their own container instead of interleaving with them.
                lineContainer = new Container { RelativeSizeAxes = Axes.Both },
                noteContainer = new Container { RelativeSizeAxes = Axes.Both },
                // Hold ends are layered above every body so an overlapped tail/head pair still reads as two pieces.
                tailContainer = new Container { RelativeSizeAxes = Axes.Both },
                headContainer = new Container { RelativeSizeAxes = Axes.Both },
                overlayContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new[]
                    {
                        previewBox = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = minimum_height,
                            Colour = Colour4.White.Opacity(preview_opacity),
                            Alpha = 0,
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo? ezSkinInfo)
        {
            this.ezSkinInfo = ezSkinInfo;

            column.Notes.BindCollectionChanged(onNotesChanged, true);
        }

        protected override void Update()
        {
            base.Update();

            if (referenceContainer is not { DrawHeight: > 0 })
                return;

            updateNotes();
            updateGridLines();
            updatePreview();
        }

        /// <summary>
        /// The playfield container the column mirrors its time axis with, if the playfield is laid out.
        /// </summary>
        private ScrollingHitObjectContainer? referenceContainer => area.ReferenceColumn?.HitObjectContainer as ScrollingHitObjectContainer;

        /// <summary>
        /// Selects the notes the given box covers, deselecting the rest unless they are preserved.
        /// </summary>
        /// <remarks>
        /// Decided by the drawn rectangle against each note's drawn rectangle, the same way the playfield's own box
        /// decides. Deriving a time band from the pointer instead needs the box to overshoot a note's head by its own
        /// height, because a note occupies times earlier than the time its head sits on.
        /// <paramref name="preserve"/> holds the notes which were already selected when an additive drag began.
        /// </remarks>
        public void UpdateBoxSelection(Quad selectionQuad, HashSet<ExtensionNote>? preserve)
        {
            foreach (var visual in noteVisuals.Values)
            {
                bool selected = (visual.Body.Alpha > 0 && selectionQuad.Contains(visual.Body.ScreenSpaceDrawQuad.Centre))
                                || preserve?.Contains(visual.Note) == true;

                if (visual.Note.Selected.Value != selected)
                    visual.Note.Selected.Value = selected;
            }
        }

        /// <summary>
        /// The note whose rendered box contains <paramref name="screenSpacePosition"/>, preferring the closest to its centre.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition)
        {
            ExtensionNote? result = null;
            float bestDistance = float.MaxValue;

            foreach (var visual in noteVisuals.Values)
            {
                Box box = visual.Body;

                if (box.Alpha <= 0 || !box.Contains(screenSpacePosition))
                    continue;

                float distance = Math.Abs(box.ToLocalSpace(screenSpacePosition).Y - box.DrawHeight / 2);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                result = visual.Note;
            }

            return result;
        }

        /// <summary>
        /// The distance from the bottom of this layer at which <paramref name="time"/> sits.
        /// </summary>
        /// <remarks>
        /// Negative for notes that have not yet reached the hit line, matching the playfield's own convention.
        /// </remarks>
        private float offsetAt(double time) => referenceContainer!.PositionAtTime(time) * referenceScale;

        /// <summary>
        /// Converts an offset or length from the mirrored container's own coordinate space into this layer's.
        /// </summary>
        /// <remarks>
        /// The column lives under the dock rather than under the playfield's adjustment container, so the playfield scale
        /// sits between the two spaces. Both spans cover the same physical height (see <see cref="DrawableExtensionColumn"/>),
        /// which makes their ratio the entire correction; without it an offset drifts further the further it sits from the hit line.
        /// </remarks>
        private float referenceScale
        {
            get
            {
                float containerHeight = referenceContainer!.DrawHeight;
                return containerHeight > 0 ? DrawHeight / containerHeight : 1;
            }
        }

        private void updateNotes()
        {
            // Mirrors the playfield hold rendering, where the fake tail setting replaces the tail piece with body.
            bool hideTail = ezSkinInfo?.ManiaLNGradientEnable.Value != false;

            foreach (var visual in noteVisuals.Values)
            {
                ExtensionNote note = visual.Note;

                float height = heightOf(note);
                float bottom = DrawHeight + offsetAt(note.Time);
                float top = bottom - height;

                visual.Body.Y = top;
                visual.Body.Height = height;

                bool isHold = note.Duration > 0;
                bool visible = top <= DrawHeight && bottom >= 0;

                // A short hold would otherwise be all ends and no body, so each end takes at most half of it.
                float endHeight = isHold ? Math.Min(minimum_height, height * 0.5f) : 0;

                visual.Head.Height = endHeight;
                visual.Head.Y = bottom - endHeight;

                visual.Tail.Height = endHeight;
                visual.Tail.Y = top;

                var (bodyColour, headColour, tailColour) = coloursFor(note.Selected.Value, isHold);

                if (visual.Body.Colour != bodyColour)
                    visual.Body.Colour = bodyColour;

                if (visual.Head.Colour != headColour)
                    visual.Head.Colour = headColour;

                if (visual.Tail.Colour != tailColour)
                    visual.Tail.Colour = tailColour;

                setAlpha(visual.Body, visible);
                setAlpha(visual.Head, visible && isHold);
                setAlpha(visual.Tail, visible && isHold && !hideTail);
            }
        }

        private float heightOf(ExtensionNote note)
            => note.Duration > 0
                ? Math.Max(minimum_height, Math.Abs(referenceContainer!.LengthAtTime(note.Time, note.Time + note.Duration)) * referenceScale)
                : minimum_height;

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
                lineContainer.Add(box);
            }

            for (int i = 0; i < gridLineBoxes.Count; i++)
            {
                var box = gridLineBoxes[i];

                if (lines == null || i >= count)
                {
                    if (box.Alpha != 0)
                        box.Alpha = 0;

                    continue;
                }

                var line = lines[i];

                box.Y = DrawHeight + offsetAt(line.Time) - grid_line_height;
                box.Colour = line.Colour;
                box.Alpha = line.Alpha;
            }
        }

        private void updatePreview()
        {
            bool show = area.HoverColumn == column
                        && area.CanPlace
                        && area.HoverTime is double
                        && !area.IsInteracting
                        && !area.IsHoveringNote;

            if (!show)
            {
                if (previewBox.Alpha != 0)
                    previewBox.Alpha = 0;

                return;
            }

            previewBox.Y = DrawHeight + offsetAt(area.HoverTime!.Value) - minimum_height;

            if (previewBox.Alpha != 1)
                previewBox.Alpha = 1;
        }

        private void onNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems!)
                        addNoteVisual((ExtensionNote)item!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems!)
                        removeNoteVisual((ExtensionNote)item!);
                    break;

                default:
                    foreach (var visual in noteVisuals.Values)
                        removeVisual(visual);

                    noteVisuals.Clear();

                    foreach (var note in column.Notes)
                        addNoteVisual(note);

                    break;
            }
        }

        private void addNoteVisual(ExtensionNote note)
        {
            var visual = new NoteVisual
            {
                Note = note,
                Body = new Box { RelativeSizeAxes = Axes.X, Height = minimum_height },
                Head = new Box { RelativeSizeAxes = Axes.X, Height = minimum_height },
                Tail = new Box { RelativeSizeAxes = Axes.X, Height = minimum_height },
            };

            noteVisuals[note] = visual;

            noteContainer.Add(visual.Body);
            tailContainer.Add(visual.Tail);
            headContainer.Add(visual.Head);
        }

        private void removeNoteVisual(ExtensionNote note)
        {
            if (!noteVisuals.Remove(note, out var visual))
                return;

            removeVisual(visual);
        }

        private void removeVisual(NoteVisual visual)
        {
            noteContainer.Remove(visual.Body, true);
            tailContainer.Remove(visual.Tail, true);
            headContainer.Remove(visual.Head, true);
        }

        /// <summary>
        /// The body, head and tail colours of a note. A hold keeps its ends opaque and its body translucent, so the ends
        /// stay readable where a tail runs into the next note's head.
        /// </summary>
        private static (Colour4 body, Colour4 head, Colour4 tail) coloursFor(bool selected, bool isHold)
        {
            if (selected)
            {
                return isHold
                    ? (Colour4.Gold.Opacity(hold_body_opacity), Colour4.Gold, Colour4.Gold.Opacity(hold_tail_opacity))
                    : (Colour4.Gold, Colour4.Gold, Colour4.Gold);
            }

            return isHold
                ? (Colour4.White.Opacity(hold_body_opacity), Colour4.White, Colour4.White.Opacity(hold_tail_opacity))
                : (Colour4.White.Opacity(tap_opacity), Colour4.White, Colour4.White);
        }

        private static void setAlpha(Drawable drawable, bool visible)
        {
            float target = visible ? 1 : 0;

            if (drawable.Alpha != target)
                drawable.Alpha = target;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                column.Notes.CollectionChanged -= onNotesChanged;

            base.Dispose(isDisposing);
        }

        private sealed class NoteVisual
        {
            public ExtensionNote Note = null!;
            public Box Body = null!;
            public Box Head = null!;
            public Box Tail = null!;
        }
    }
}
