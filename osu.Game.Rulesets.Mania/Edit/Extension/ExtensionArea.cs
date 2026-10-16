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
using osu.Framework.Graphics.Primitives;
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
        /// Beat divisor lines around <see cref="HoverTime"/>.
        /// </summary>
        internal IReadOnlyList<ExtensionSnapLine> SnapLines => snapGrid.Lines;

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
        /// Selects the notes inside <paramref name="region"/>, deselecting the rest unless they are preserved.
        /// </summary>
        public void UpdateBoxSelection(Quad region, HashSet<ExtensionNote>? preserve)
        {
            foreach (var column in columnFlow)
                column.UpdateBoxSelection(region, preserve);
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
            if (mouse == null || column == null || column.ContainsControl(mouse.Value) || TimeAt(mouse.Value) is not double time)
            {
                snapGrid.Clear();
                return;
            }

            HoverColumn = column.Column;
            IsHoveringNote = column.NoteAt(mouse.Value) != null;

            double snapped = beatSnapProvider.SnapTime(time);
            HoverTime = snapped;

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
            SelectAllRequested = () => columnSet.SelectAll(column),
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
