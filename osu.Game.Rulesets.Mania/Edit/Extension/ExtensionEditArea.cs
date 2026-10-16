// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Handles note interaction inside the extension column dock: placement, selection, movement and deletion.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="ExtensionNoteLayer"/> rendering. A click on empty space places a tap note,
    /// dragging it vertically extends it into a hold; a click on a note selects it, dragging it changes its time and column.
    /// Dropping a drag on a playfield column hands the notes over to the playfield model instead (see
    /// <see cref="ExtensionNoteConversion"/>); that hand-over is not undoable yet, unlike the playfield side.
    /// </remarks>
    public partial class ExtensionEditArea : CompositeDrawable
    {
        private readonly ExtensionColumnSet columnSet;
        private readonly ExtensionArea area;
        private readonly ManiaPlayfield playfield;

        private readonly List<(ExtensionNote note, double time, int columnIndex)> movingNotes = new List<(ExtensionNote, double, int)>();

        private ExtensionColumn? draftColumn;
        private ExtensionNote? draftNote;
        private double draftStartTime;

        private double movingStartTime;
        private int movingStartColumnIndex;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        /// <summary>
        /// Whether clicking empty column space creates notes. Disabled while the selection tool is active, where the
        /// same gesture belongs to the playfield's selection box.
        /// </summary>
        public Func<bool>? PlacementEnabled { get; set; }

        /// <summary>
        /// Positions owned by another interactive element (the dock scrollbar) which must not receive extension input.
        /// </summary>
        public Func<Vector2, bool>? ExcludePosition { get; set; }

        public ExtensionEditArea(ExtensionColumnSet columnSet, ExtensionArea area, ManiaPlayfield playfield)
        {
            this.columnSet = columnSet;
            this.area = area;
            this.playfield = playfield;

            RelativeSizeAxes = Axes.Both;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => ExcludePosition?.Invoke(screenSpacePos) != true && base.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            var note = area.NoteAt(e.ScreenSpaceMousePosition);

            if (note != null)
            {
                select(note, additive: e.ControlPressed || e.ShiftPressed);
                return true;
            }

            var column = area.ColumnAt(e.ScreenSpaceMousePosition);

            if (column == null || PlacementEnabled?.Invoke() == false)
                return false;

            columnSet.ClearSelection();
            columnSet.AddNote(column, snapTimeAt(e.ScreenSpaceMousePosition));

            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Right)
                return false;

            var note = area.NoteAt(e.ScreenSpaceMousePosition);

            if (note == null)
                return false;

            // Right-clicking outside the selection deletes just that note, so a stray click never wipes a multi-selection.
            if (!note.Selected.Value)
                columnSet.ClearSelection();

            note.Selected.Value = true;
            columnSet.RemoveSelected();

            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            // Shift is the dock-pan modifier; keep it out of note interaction so the two never compete.
            if (e.Button != MouseButton.Left || e.ShiftPressed)
                return false;

            var note = area.NoteAt(e.ScreenSpaceMousePosition);

            // On a note this is a move; anywhere else it is a placement, which the selection tool leaves to the
            // playfield's selection box instead.
            if (note == null && PlacementEnabled?.Invoke() == false)
                return false;

            movingNotes.Clear();

            if (note != null)
            {
                if (!note.Selected.Value)
                    select(note, additive: false);

                foreach (var selected in columnSet.SelectedNotes)
                    movingNotes.Add((selected, selected.Time, indexOf(selected.Column)));

                if (movingNotes.Count == 0)
                    return false;

                movingStartTime = snapTimeAt(e.ScreenSpaceMousePosition);
                movingStartColumnIndex = area.DrawableColumnAt(e.ScreenSpaceMousePosition) is DrawableExtensionColumn startColumn
                    ? indexOf(startColumn.Column)
                    : movingNotes[0].columnIndex;

                area.IsInteracting = true;
                return true;
            }

            var column = area.ColumnAt(e.ScreenSpaceMousePosition);

            if (column == null)
                return false;

            draftColumn = column;
            draftStartTime = snapTimeAt(e.ScreenSpaceMousePosition);

            area.IsInteracting = true;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (movingNotes.Count > 0)
            {
                moveSelection(e.ScreenSpaceMousePosition);
                return;
            }

            if (draftColumn == null)
                return;

            double duration = snapTimeAt(e.ScreenSpaceMousePosition) - draftStartTime;

            if (duration <= 0)
            {
                cancelDraft();
                return;
            }

            if (draftNote == null)
                draftNote = columnSet.AddNote(draftColumn, draftStartTime, duration);
            else
                draftNote.Duration = duration;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            area.IsInteracting = false;

            if (draftColumn != null && draftNote == null)
                columnSet.AddNote(draftColumn, draftStartTime);

            cancelDraft();

            if (movingNotes.Count > 0)
                dropSelection(e.ScreenSpaceMousePosition);

            movingNotes.Clear();
        }

        /// <summary>
        /// Ends a note drag: notes released over a playfield column become playfield objects, otherwise they stay in the columns.
        /// </summary>
        private void dropSelection(Vector2 screenSpacePosition)
        {
            var column = playfield.GetColumnByPosition(screenSpacePosition);

            if (column == null)
                return;

            // The playfield side is committed through EditorBeatmap so the editor's own undo covers it. Dropping the
            // extension notes is session state without history yet, so undo brings the playfield notes back but not them.
            editorBeatmap.BeginChange();

            foreach (var (note, _, _) in movingNotes)
            {
                editorBeatmap.Add(ExtensionNoteConversion.ToPlayfieldObject(note, column.Index, note.Time));
                columnSet.RemoveNote(note);
            }

            editorBeatmap.EndChange();
        }

        /// <summary>
        /// Moves the selection by the dragged time and column delta.
        /// </summary>
        /// <remarks>
        /// The column delta is applied to every note instead of collapsing the selection onto the hovered column, so a
        /// selection spanning several columns keeps its relative columns.
        /// </remarks>
        private void moveSelection(Vector2 screenSpacePosition)
        {
            double delta = snapTimeAt(screenSpacePosition) - movingStartTime;

            int? columnDelta = null;
            var target = area.DrawableColumnAt(screenSpacePosition);

            if (target != null)
            {
                int deltaInColumns = indexOf(target.Column) - movingStartColumnIndex;

                int minIndex = int.MaxValue;
                int maxIndex = int.MinValue;

                foreach (var (_, _, columnIndex) in movingNotes)
                {
                    minIndex = Math.Min(minIndex, columnIndex);
                    maxIndex = Math.Max(maxIndex, columnIndex);
                }

                if (columnSet.Columns.Count > 0)
                    columnDelta = Math.Clamp(deltaInColumns, -minIndex, columnSet.Columns.Count - 1 - maxIndex);
            }

            foreach (var (note, originalTime, columnIndex) in movingNotes)
            {
                double newTime = Math.Max(0, originalTime + delta);

                if (columnDelta is not null && columnDelta.Value != 0)
                    columnSet.MoveNote(note, columnSet.Columns[Math.Clamp(columnIndex + columnDelta.Value, 0, columnSet.Columns.Count - 1)], newTime);
                else
                    note.Time = newTime;
            }
        }

        private int indexOf(ExtensionColumn? column) => column == null ? 0 : Math.Max(0, columnSet.Columns.IndexOf(column));

        private void select(ExtensionNote note, bool additive)
        {
            if (!additive)
                columnSet.ClearSelection();

            note.Selected.Value = true;
        }

        private void cancelDraft()
        {
            if (draftNote != null)
            {
                columnSet.RemoveNote(draftNote);
                draftNote = null;
            }

            draftColumn = null;
        }

        private double snapTimeAt(Vector2 screenSpacePosition)
            => beatSnapProvider.SnapTime(area.TimeAt(screenSpacePosition) ?? Time.Current);
    }
}
