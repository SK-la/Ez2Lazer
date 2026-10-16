// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Renders the extension columns of a <see cref="ExtensionColumnSet"/>, follows the cursor over them, and resolves
    /// what a screen position points at.
    /// </summary>
    /// <remarks>
    /// A placeholder column is shown while the set is empty, so the extension area stays visible.
    /// </remarks>
    public partial class ExtensionArea : CompositeDrawable
    {
        private const float placeholder_width = 24;

        private readonly ExtensionColumnSet columnSet;
        private readonly ManiaPlayfield playfield;

        private readonly ExtensionSnapGrid snapGrid = new ExtensionSnapGrid();

        private (double start, double end)? selectionTimeRange;

        private FillFlowContainer<DrawableExtensionColumn> columnFlow = null!;
        private Drawable placeholder = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        /// <summary>
        /// Whether clicking empty column space creates notes.
        /// </summary>
        /// <remarks>
        /// Also gates the hover feedback. Without that the beat lines and the placement preview would still be drawn
        /// while the composer's selection tool is active, making the columns read as a placement surface even though
        /// their blank space belongs to the playfield's selection box.
        /// </remarks>
        public Func<bool>? PlacementEnabled { get; set; }

        /// <summary>
        /// Whether the columns currently accept placement gestures.
        /// </summary>
        public bool CanPlace => PlacementEnabled?.Invoke() != false;

        /// <summary>
        /// Raised when the columns take a selection. The playfield and the columns never hold a selection at the same
        /// time, so the owner drops the playfield's in response.
        /// </summary>
        public Action? SelectionTaken { get; set; }

        /// <summary>
        /// The playfield column whose scroll mapping and geometry the columns mirror, if the playfield is laid out.
        /// </summary>
        public Column? ReferenceColumn { get; private set; }

        /// <summary>
        /// The column the cursor is over, or <c>null</c> when it is elsewhere.
        /// </summary>
        public ExtensionColumn? HoverColumn { get; private set; }

        /// <summary>
        /// Whether the cursor is over a note of the hovered column, in which case a placement preview would be misleading.
        /// </summary>
        public bool IsHoveringNote { get; private set; }

        /// <summary>
        /// The snapped time under the cursor, or <c>null</c> when the cursor is not over a column.
        /// </summary>
        public double? HoverTime { get; private set; }

        /// <summary>
        /// Whether the dock is currently being dragged, which takes over the hover affordances.
        /// </summary>
        public bool IsInteracting { get; set; }

        /// <summary>
        /// The beat divisor lines to draw on <paramref name="column"/>, or <c>null</c> when it should draw none.
        /// </summary>
        /// <remarks>
        /// A selection draws its lines on every column; otherwise only the hovered column shows the cursor's lines.
        /// </remarks>
        internal IReadOnlyList<ExtensionSnapLine>? LinesFor(ExtensionColumn column)
        {
            if (selectionTimeRange != null)
                return snapGrid.Lines;

            return HoverColumn == column ? snapGrid.Lines : null;
        }

        /// <summary>
        /// The time range whose beat lines the columns should show, when a selection owns them instead of the cursor.
        /// </summary>
        public (double start, double end)? SelectionTimeRange
        {
            set
            {
                if (value == selectionTimeRange)
                    return;

                selectionTimeRange = value;
                snapGrid.Clear();
            }
        }

        public ExtensionArea(ExtensionColumnSet columnSet, ManiaPlayfield playfield)
        {
            this.columnSet = columnSet;
            this.playfield = playfield;

            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnFlow = new FillFlowContainer<DrawableExtensionColumn>
            {
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Direction = FillDirection.Horizontal,
            };

            InternalChildren = new Drawable[]
            {
                columnFlow,
                placeholder = createPlaceholder(),
            };

            columnSet.Columns.BindCollectionChanged(onColumnsChanged, true);
            editorBeatmap.ControlPointInfo.ControlPointsChanged += onControlPointsChanged;
        }

        protected override void Update()
        {
            base.Update();

            ReferenceColumn = findReferenceColumn();
            updateHover();
        }

        /// <summary>
        /// The column whose column contains <paramref name="screenSpacePosition"/>, if any.
        /// </summary>
        public ExtensionColumn? ColumnAt(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.Column;

        /// <summary>
        /// The note under <paramref name="screenSpacePosition"/>, if any.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.NoteAt(screenSpacePosition);

        /// <summary>
        /// Whether <paramref name="screenSpacePosition"/> sits on a note rather than the column's blank space.
        /// </summary>
        public bool OwnsNoteAt(Vector2 screenSpacePosition) => NoteAt(screenSpacePosition) != null;

        /// <summary>
        /// The notes currently selected across all columns.
        /// </summary>
        public IReadOnlyCollection<ExtensionNote> SelectedNotes => columnSet.SelectedNotes.ToArray();

        /// <summary>
        /// Clears the note selection.
        /// </summary>
        public void ClearSelection() => columnSet.ClearSelection();

        /// <summary>
        /// Selects every note of <paramref name="column"/>.
        /// </summary>
        public void SelectAll(ExtensionColumn column)
        {
            NotifySelectionTaken();
            columnSet.SelectAll(column);
        }

        /// <summary>
        /// Selects every note across all columns.
        /// </summary>
        public void SelectAllNotes()
        {
            NotifySelectionTaken();
            columnSet.SelectAll();
        }

        /// <summary>
        /// Signals that the columns are taking a selection, so the owner can drop the playfield's.
        /// </summary>
        public void NotifySelectionTaken() => SelectionTaken?.Invoke();

        /// <summary>
        /// Removes every selected note.
        /// </summary>
        /// <returns>The number of notes removed.</returns>
        public int RemoveSelected() => columnSet.RemoveSelected();

        /// <summary>
        /// Selects the notes whose centre time lies in the given band, deselecting the rest unless they are preserved.
        /// </summary>
        public void UpdateBoxSelection(double minTime, double maxTime, HashSet<ExtensionNote>? preserve)
        {
            foreach (var column in columnFlow)
                column.UpdateBoxSelection(minTime, maxTime, preserve);
        }

        /// <summary>
        /// Whether <paramref name="screenSpacePosition"/> is over a per-column control rather than the note area.
        /// </summary>
        public bool IsOnControl(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.ContainsControl(screenSpacePosition) == true;

        /// <summary>
        /// The time at <paramref name="screenSpacePosition"/>, using the playfield's own scroll mapping.
        /// </summary>
        /// <remarks>
        /// Resolved through the playfield column rather than derived from the column's own geometry, so that placement and
        /// dragging in the columns land on the same times the playfield would report.
        /// </remarks>
        public double? TimeAt(Vector2 screenSpacePosition)
        {
            var column = ReferenceColumn;

            if (column == null || column.HitObjectContainer.DrawHeight <= 0)
                return null;

            return column.TimeAtScreenSpacePosition(screenSpacePosition);
        }

        /// <summary>
        /// The column whose area contains <paramref name="screenSpacePosition"/>, if any.
        /// </summary>
        public DrawableExtensionColumn? DrawableColumnAt(Vector2 screenSpacePosition)
        {
            foreach (var column in columnFlow)
            {
                if (column.Contains(screenSpacePosition))
                    return column;
            }

            return null;
        }

        /// <summary>
        /// The column containing <paramref name="screenSpacePosition"/>, or the horizontally nearest one.
        /// </summary>
        /// <remarks>
        /// Used by the hover controls so that the blank space beside the columns still targets the closest column.
        /// </remarks>
        public DrawableExtensionColumn? DrawableColumnAtOrNearest(Vector2 screenSpacePosition)
        {
            DrawableExtensionColumn? nearest = null;
            float bestDistance = float.MaxValue;

            foreach (var column in columnFlow)
            {
                if (column.Contains(screenSpacePosition))
                    return column;

                var quad = column.ScreenSpaceDrawQuad.AABBFloat;

                if (quad.Width <= 0)
                    continue;

                float distance = Math.Abs(screenSpacePosition.X - quad.Centre.X);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                nearest = column;
            }

            return nearest;
        }

        private Column? findReferenceColumn()
        {
            foreach (var stage in playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    if (column.HitObjectContainer.DrawHeight > 0)
                        return column;
                }
            }

            return null;
        }

        private void updateHover()
        {
            HoverColumn = null;
            IsHoveringNote = false;
            HoverTime = null;

            Vector2? mouse = currentMousePosition();
            var column = mouse == null ? null : DrawableColumnAt(mouse.Value);

            // The column's own controls (select-all) and the dock's scrollbar own their positions.
            if (mouse != null && column != null && !column.ContainsControl(mouse.Value) && TimeAt(mouse.Value) is double time)
            {
                HoverColumn = column.Column;
                IsHoveringNote = column.NoteAt(mouse.Value) != null;
                HoverTime = beatSnapProvider.SnapTime(time);
            }

            updateGrid();
        }

        /// <summary>
        /// Rebuilds the beat divisor lines, preferring a selection's band over the hovered time.
        /// </summary>
        /// <remarks>
        /// The selection band outranks the cursor because the composer keeps the selection tool active while the columns
        /// hold a selection, which already suppresses placement.
        /// </remarks>
        private void updateGrid()
        {
            if (selectionTimeRange is (double start, double end))
            {
                if (!snapGrid.IsValidForRange(start, end, beatDivisor.Value))
                    snapGrid.RebuildForRange(start, end, editorBeatmap.ControlPointInfo, beatDivisor.Value, editorBeatmap.FixedGridEnabled.Value, colours);

                return;
            }

            if (!CanPlace || HoverTime is not double snapped)
            {
                snapGrid.Clear();
                return;
            }

            if (!snapGrid.IsValidFor(snapped, beatDivisor.Value))
                snapGrid.Rebuild(snapped, editorBeatmap.ControlPointInfo, beatDivisor.Value, editorBeatmap.FixedGridEnabled.Value, colours);
        }

        private Vector2? currentMousePosition()
        {
            var inputManager = GetContainingInputManager();

            return inputManager == null ? null : inputManager.CurrentState.Mouse.Position;
        }

        private void onControlPointsChanged() => snapGrid.Clear();

        private void onColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (int i = 0; i < e.NewItems!.Count; i++)
                        columnFlow.Insert(e.NewStartingIndex + i, createColumn((ExtensionColumn)e.NewItems[i]!));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (int i = 0; i < e.OldItems!.Count; i++)
                        columnFlow.Remove(columnFlow[e.OldStartingIndex], true);
                    break;

                default:
                    columnFlow.Clear();

                    foreach (var column in columnSet.Columns)
                        columnFlow.Add(createColumn(column));

                    break;
            }

            placeholder.Alpha = columnSet.Columns.Count == 0 ? 1 : 0;
        }

        private DrawableExtensionColumn createColumn(ExtensionColumn column) => new DrawableExtensionColumn(column, this)
        {
            SelectAllRequested = () => SelectAll(column),
        };

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap.IsNotNull())
                editorBeatmap.ControlPointInfo.ControlPointsChanged -= onControlPointsChanged;

            base.Dispose(isDisposing);
        }

        private static Drawable createPlaceholder() => new Container
        {
            RelativeSizeAxes = Axes.Y,
            Width = placeholder_width,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.35f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Colour = Colour4.White.Opacity(0.15f),
                },
            },
        };
    }
}
