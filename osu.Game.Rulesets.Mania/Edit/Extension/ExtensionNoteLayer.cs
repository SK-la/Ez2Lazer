// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
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

        private readonly ExtensionColumn column;
        private readonly ExtensionArea area;

        private readonly Container lineContainer;
        private readonly Container noteContainer;

        private readonly Dictionary<ExtensionNote, Box> noteBoxes = new Dictionary<ExtensionNote, Box>();
        private readonly List<Box> gridLineBoxes = new List<Box>();
        private readonly Box previewBox;

        public ExtensionNoteLayer(ExtensionColumn column, ExtensionArea area)
        {
            this.column = column;
            this.area = area;

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                // Grid lines belong under the notes, so they get their own container instead of interleaving with them.
                lineContainer = new Container { RelativeSizeAxes = Axes.Both },
                noteContainer = new Container
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

        [BackgroundDependencyLoader]
        private void load()
        {
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
        /// Selects the notes inside <paramref name="region"/>, deselecting the rest unless they are preserved.
        /// </summary>
        /// <remarks>
        /// <paramref name="preserve"/> holds the notes which were already selected when an additive drag began.
        /// </remarks>
        public void UpdateBoxSelection(Quad region, IReadOnlyCollection<ExtensionNote>? preserve)
        {
            foreach (var (note, box) in noteBoxes)
            {
                bool selected = (box.Alpha > 0 && region.Contains(box.ScreenSpaceDrawQuad.Centre))
                                || preserve?.Contains(note) == true;

                if (note.Selected.Value != selected)
                    note.Selected.Value = selected;
            }
        }

        /// <summary>
        /// The note whose rendered box contains <paramref name="screenSpacePosition"/>, preferring the closest to its centre.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition)
        {
            ExtensionNote? result = null;
            float bestDistance = float.MaxValue;

            foreach (var (note, box) in noteBoxes)
            {
                if (box.Alpha <= 0 || !box.Contains(screenSpacePosition))
                    continue;

                float distance = Math.Abs(box.ToLocalSpace(screenSpacePosition).Y - box.DrawHeight / 2);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                result = note;
            }

            return result;
        }

        /// <summary>
        /// The distance from the bottom of this layer at which <paramref name="time"/> sits.
        /// </summary>
        /// <remarks>
        /// Negative for notes that have not yet reached the hit line, matching the playfield's own convention.
        /// </remarks>
        private float offsetAt(double time) => referenceContainer!.PositionAtTime(time);

        private void updateNotes()
        {
            foreach (var (note, box) in noteBoxes)
            {
                float height = heightOf(note);
                float top = DrawHeight + offsetAt(note.Time) - height;

                box.Y = top;
                box.Height = height;

                Colour4 targetColour = colourFor(note);

                if (box.Colour != targetColour)
                    box.Colour = targetColour;

                // Cull notes that fall outside the visible time window.
                bool visible = top <= DrawHeight && top + height >= 0;
                float targetAlpha = visible ? 1 : 0;

                if (box.Alpha != targetAlpha)
                    box.Alpha = targetAlpha;
            }
        }

        private float heightOf(ExtensionNote note)
            => note.Duration > 0
                ? Math.Max(minimum_height, Math.Abs(referenceContainer!.LengthAtTime(note.Time, note.Time + note.Duration)))
                : minimum_height;

        private void updateGridLines()
        {
            IReadOnlyList<ExtensionSnapLine>? lines = area.HoverColumn == column ? area.SnapLines : null;
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
                        && area.HoverTime is double time
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
                        addNoteBox((ExtensionNote)item!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems!)
                        removeNoteBox((ExtensionNote)item!);
                    break;

                default:
                    foreach (var box in noteBoxes.Values)
                        noteContainer.Remove(box, true);

                    noteBoxes.Clear();

                    foreach (var note in column.Notes)
                        addNoteBox(note);

                    break;
            }
        }

        private void addNoteBox(ExtensionNote note)
        {
            var box = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = minimum_height,
            };

            noteBoxes[note] = box;
            noteContainer.Add(box);
        }

        private static Colour4 colourFor(ExtensionNote note) => note.Selected.Value ? Colour4.Gold : Colour4.White.Opacity(0.65f);

        private void removeNoteBox(ExtensionNote note)
        {
            if (!noteBoxes.Remove(note, out var box))
                return;

            noteContainer.Remove(box, true);
        }
    }
}
