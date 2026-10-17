// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// The editor overlay of the extension dock: placement, selection, movement and deletion of extension notes.
    /// </summary>
    /// <remarks>
    /// This is the dock's counterpart of the composer's <see cref="BlueprintContainer{T}"/> pipeline. Selection, marquee,
    /// dragging and deletion are inherited rather than reimplemented; only placement and the time/column mapping are
    /// specific to the dock. Nothing here puts extension notes into <see cref="EditorBeatmap.HitObjects"/>; converting at the
    /// region boundary is explicit.
    /// </remarks>
    public partial class ExtensionBlueprintContainer : BlueprintContainer<ExtensionNote>
    {
        private const double minimum_hold_duration = 1;

        private readonly ExtensionColumnSet columnSet;
        private readonly ExtensionArea area;
        private readonly ManiaPlayfield playfield;

        private readonly HashSet<ExtensionNote> trackedNotes = new HashSet<ExtensionNote>();
        private bool needsSync;

        private ExtensionColumn? placementColumn;
        private double placementStartTime;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IEditorChangeHandler? changeHandler { get; set; }

        /// <summary>
        /// Decides whether a screen position belongs to this overlay. Set by the host so the playfield's container and this
        /// one never both claim the same position.
        /// </summary>
        public Func<Vector2, bool>? OwnsInputAt { get; set; }

        /// <summary>
        /// Raised when the selection of extension notes changes.
        /// </summary>
        public event Action? SelectionChanged;

        /// <summary>
        /// Whether any extension note is selected.
        /// </summary>
        public bool HasSelection => SelectedItems.Count > 0;

        /// <summary>
        /// The selected extension notes.
        /// </summary>
        public IReadOnlyList<ExtensionNote> SelectedNotes => SelectedItems.ToArray();

        public ExtensionBlueprintContainer(ExtensionColumnSet columnSet, ExtensionArea area, ManiaPlayfield playfield)
        {
            this.columnSet = columnSet;
            this.area = area;
            this.playfield = playfield;

            RelativeSizeAxes = Axes.Both;

            columnSet.Columns.BindCollectionChanged(onColumnsChanged, true);
            area.LaneAdded += _ => needsSync = true;
            area.LaneRemoved += _ => needsSync = true;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => OwnsInputAt?.Invoke(screenSpacePos) == true;

        /// <summary>
        /// Removes every selected note.
        /// </summary>
        private ExtensionSelectionHandler? handler;

        public void DeleteSelection() => handler?.DeleteSelection();

        /// <summary>
        /// Replaces the selection with every note of <paramref name="column"/>.
        /// </summary>
        public void SelectAll(ExtensionColumn column)
        {
            syncBlueprints();
            DeselectAll();

            foreach (var note in column.Notes)
                select(note);
        }

        /// <summary>
        /// Replaces the selection with every note in the dock.
        /// </summary>
        public void SelectAllNotes() => SelectAll();

        /// <summary>
        /// Clears the dock's selection.
        /// </summary>
        public void ClearSelection() => DeselectAll();

        protected override void Update()
        {
            base.Update();

            if (!needsSync)
                return;

            needsSync = false;
            syncBlueprints();
        }

        protected override SelectionHandler<ExtensionNote> CreateSelectionHandler() => handler = new ExtensionSelectionHandler(columnSet);

        protected override SelectionBlueprint<ExtensionNote> CreateBlueprintFor(ExtensionNote item) => new ExtensionNoteBlueprint(item, area);

        protected override void SelectAll()
        {
            syncBlueprints();

            foreach (var column in columnSet.Columns)
            {
                foreach (var note in column.Notes)
                    select(note);
            }
        }

        private void select(ExtensionNote note)
        {
            SelectionBlueprints.FirstOrDefault(b => b.Item == note)?.Select();
        }

        protected override void OnBlueprintSelected(SelectionBlueprint<ExtensionNote> blueprint)
        {
            base.OnBlueprintSelected(blueprint);
            SelectionChanged?.Invoke();
        }

        protected override void OnBlueprintDeselected(SelectionBlueprint<ExtensionNote> blueprint)
        {
            base.OnBlueprintDeselected(blueprint);
            SelectionChanged?.Invoke();
        }

        #region Input

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Left && tryBeginPlacement(e.ScreenSpaceMouseDownPosition))
                return true;

            return base.OnMouseDown(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (placementColumn != null)
            {
                area.IsInteracting = true;
                updateDraft(e.ScreenSpaceMousePosition);
                return true;
            }

            return base.OnDragStart(e);
        }

        protected override void OnDrag(DragEvent e)
        {
            if (placementColumn != null)
            {
                updateDraft(e.ScreenSpaceMousePosition);
                return;
            }

            base.OnDrag(e);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            // Completion is handled on mouse up, which is the one event both a click and a drag end go through, so a
            // placement cannot be committed twice and cannot be stranded when the release lands outside the dock.
            if (placementColumn != null)
                return;

            base.OnDragEnd(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == MouseButton.Left && placementColumn != null)
            {
                commitPlacement(e.ScreenSpaceMousePosition);
                return;
            }

            base.OnMouseUp(e);
        }

        #endregion

        #region Placement

        private bool tryBeginPlacement(Vector2 screenSpacePosition)
        {
            if (!area.CanPlace)
                return false;

            var column = area.DrawableColumnAt(screenSpacePosition);

            if (column == null || column.NoteAt(screenSpacePosition) != null)
                return false;

            if (column.TimeAt(screenSpacePosition) is not double time)
                return false;

            placementColumn = column.Column;
            placementStartTime = beatSnapProvider.SnapTime(time);
            area.IsInteracting = true;

            return true;
        }

        private void updateDraft(Vector2 screenSpacePosition)
        {
            if (placementColumn == null)
                return;

            if (area.TimeAt(screenSpacePosition) is not double time)
                return;

            area.Draft = (placementColumn, Math.Min(placementStartTime, beatSnapProvider.SnapTime(time)), Math.Max(placementStartTime, beatSnapProvider.SnapTime(time)));
        }

        private void commitPlacement(Vector2 screenSpacePosition)
        {
            var column = placementColumn;

            if (column == null)
                return;

            double? current = area.TimeAt(screenSpacePosition);

            placementColumn = null;
            area.IsInteracting = false;
            area.Draft = null;

            double start = placementStartTime;
            double end = current != null ? beatSnapProvider.SnapTime(current.Value) : start;

            if (Math.Abs(end - start) < minimum_hold_duration)
            {
                addNote(column, start, 0);
                return;
            }

            addNote(column, Math.Min(start, end), Math.Abs(end - start));
        }

        private void addNote(ExtensionColumn column, double time, double duration)
        {
            changeHandler?.BeginChange();
            columnSet.AddNote(column, Math.Max(0, time), duration);
            changeHandler?.EndChange();
        }

        #endregion

        #region Movement

        /// <summary>
        /// The state each selected note had when the current drag began.
        /// </summary>
        /// <remarks>
        /// Movements are recomputed from this snapshot on every drag event rather than added to the note's current position,
        /// so a drag never accumulates rounding or snap drift.
        /// </remarks>
        private Dictionary<ExtensionNote, (ExtensionColumn column, int columnIndex, double time)>? dragOriginals;

        private int dragReferenceColumnIndex;
        private double dragReferenceTime;

        protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<ExtensionNote> blueprint, Vector2[] originalSnapPositions)> blueprints)
        {
            if (blueprints.Count == 0 || columnSet.Columns.Count == 0)
                return false;

            var reference = blueprints[0].blueprint;
            var targetDrawable = area.DrawableColumnAt(e.ScreenSpaceMousePosition);

            if (targetDrawable == null)
                return false;

            if (dragOriginals == null)
            {
                dragOriginals = new Dictionary<ExtensionNote, (ExtensionColumn, int, double)>();

                foreach (var note in SelectedNotes)
                {
                    if (note.Column == null)
                        continue;

                    dragOriginals[note] = (note.Column, columnSet.Columns.IndexOf(note.Column), note.Time);
                }

                if (!dragOriginals.TryGetValue(reference.Item, out var referenceOriginal))
                {
                    dragOriginals = null;
                    return false;
                }

                dragReferenceColumnIndex = referenceOriginal.columnIndex;
                dragReferenceTime = referenceOriginal.time;
            }

            if (area.LaneOf(targetDrawable.Column) is not ExtensionLane targetLane)
                return false;

            // The grab time comes from the press, not the current pointer, so the note keeps the cursor's offset.
            if (targetLane.TimeAt(e.ScreenSpaceMouseDownPosition) is not double grabTime)
                return false;

            if (targetLane.TimeAt(e.ScreenSpaceMousePosition) is not double targetTime)
                return false;

            // Snapping is applied to the reference note and then shared by the whole selection, so relative spacing between
            // the dragged notes survives the snap instead of each note collapsing onto its own beat.
            double rawDelta = targetTime - grabTime;
            double referenceTime = Math.Max(0, dragReferenceTime + rawDelta);
            double timeDelta = rawDelta + (beatSnapProvider.SnapTime(referenceTime) - referenceTime);
            int columnDelta = columnSet.Columns.IndexOf(targetDrawable.Column) - dragReferenceColumnIndex;

            bool moved = false;

            foreach (var (note, original) in dragOriginals)
            {
                int index = Math.Clamp(original.columnIndex + columnDelta, 0, columnSet.Columns.Count - 1);
                var target = columnSet.Columns[index];
                double newTime = Math.Max(0, original.time + timeDelta);

                if (target == original.column && newTime == original.time)
                    continue;

                columnSet.MoveNote(note, target, newTime);
                moved = true;
            }

            return moved;
        }

        protected override void DragOperationCompleted()
        {
            dragOriginals = null;

            base.DragOperationCompleted();

            var column = playfield.GetColumnByPosition(GetContainingInputManager().CurrentState.Mouse.Position);

            if (column == null)
                return;

            dropIntoPlayfield(column);
        }

        /// <summary>
        /// Converts the selected extension notes into playfield objects at <paramref name="target"/>.
        /// </summary>
        /// <remarks>
        /// This is the only point where extension data becomes <see cref="HitObject"/> data, which keeps the two models
        /// from mixing anywhere else.
        /// </remarks>
        private void dropIntoPlayfield(Column target)
        {
            var notes = SelectedNotes;

            if (notes.Count == 0)
                return;

            DeselectAll();

            changeHandler?.BeginChange();

            var added = new List<HitObject>();

            foreach (var note in notes)
            {
                HitObject hitObject;

                if (note.Duration > 0)
                {
                    hitObject = new HoldNote
                    {
                        Column = target.Index,
                        StartTime = note.Time,
                        EndTime = note.Time + note.Duration,
                    };
                }
                else
                {
                    hitObject = new Note
                    {
                        Column = target.Index,
                        StartTime = note.Time,
                    };
                }

                editorBeatmap.Add(hitObject);
                added.Add(hitObject);
                columnSet.RemoveNote(note);
            }

            changeHandler?.EndChange();

            foreach (var hitObject in added)
                editorBeatmap.SelectedHitObjects.Add(hitObject);
        }

        #endregion

        #region Blueprint lifecycle

        private void onColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var column in columnSet.Columns)
                column.Notes.CollectionChanged -= onNotesChanged;

            foreach (var column in columnSet.Columns)
                column.Notes.CollectionChanged += onNotesChanged;

            needsSync = true;
        }

        private void onNotesChanged(object? sender, NotifyCollectionChangedEventArgs e) => needsSync = true;

        private void syncBlueprints()
        {
            // The lane surfaces are recreated with the columns, so blueprints re-resolve their surface instead of holding
            // one. Only notes that left their column lose their blueprint.
            foreach (var note in trackedNotes.ToArray())
            {
                if (note.Column != null && columnSet.Columns.Contains(note.Column) && note.Column.Notes.Contains(note))
                    continue;

                trackedNotes.Remove(note);
                RemoveBlueprintFor(note);
            }

            foreach (var column in columnSet.Columns)
            {
                foreach (var note in column.Notes)
                {
                    if (trackedNotes.Add(note))
                        AddBlueprintFor(note);
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            columnSet.Columns.CollectionChanged -= onColumnsChanged;

            foreach (var column in columnSet.Columns)
                column.Notes.CollectionChanged -= onNotesChanged;

            base.Dispose(isDisposing);
        }

        #endregion
    }
}
