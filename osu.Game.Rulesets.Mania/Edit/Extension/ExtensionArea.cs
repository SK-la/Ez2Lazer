// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using osu.Framework.Allocation;
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
    /// Renders the extension columns of an <see cref="ExtensionColumnSet"/> and reports what the cursor points at.
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

        private readonly Dictionary<ExtensionColumn, ExtensionLane> lanes = new Dictionary<ExtensionColumn, ExtensionLane>();

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        /// <summary>
        /// What the columns should create for a placement gesture, following the composer's active tool.
        /// </summary>
        public Func<ExtensionPlacementMode>? PlacementMode { get; set; }

        public ExtensionPlacementMode Mode => PlacementMode?.Invoke() ?? ExtensionPlacementMode.None;

        public bool CanPlace => Mode != ExtensionPlacementMode.None;

        /// <summary>
        /// The playfield column whose scrolling container the dock mirrors, if the playfield is laid out.
        /// </summary>
        /// <remarks>
        /// The playfield lives under its own scaling container while the dock does not, so the dock copies this column's
        /// screen geometry and time mapping instead of deriving its own. Without it the grid, the notes and their
        /// selection outlines drift further apart the further they sit from the hit line.
        /// </remarks>
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
        /// The hold being drawn by a placement drag, if any.
        /// </summary>
        public (ExtensionColumn column, double start, double end)? Draft { get; set; }

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

        /// <summary>
        /// Raised when a column's scrolling surface becomes available.
        /// </summary>
        public event Action<ExtensionLane>? LaneAdded;

        /// <summary>
        /// Raised when a column's scrolling surface is about to be dropped.
        /// </summary>
        public event Action<ExtensionLane>? LaneRemoved;

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

            InternalChildren = new[]
            {
                columnFlow,
                placeholder = createPlaceholder(),
            };

            columnSet.Columns.BindCollectionChanged(onColumnsChanged, true);
            editorBeatmap.ControlPointInfo.ControlPointsChanged += onControlPointsChanged;
        }

        private void onControlPointsChanged() => snapGrid.Clear();

        protected override void Update()
        {
            base.Update();

            ReferenceColumn = findReferenceColumn();

            updateHover();
        }

        /// <summary>
        /// The beat lines the given column should draw, or <c>null</c> when it should draw none.
        /// </summary>
        /// <remarks>
        /// A selection owns the lines across every column; otherwise only the hovered column shows the cursor's lines,
        /// matching the playfield's grid.
        /// </remarks>
        internal IReadOnlyList<ExtensionSnapLine>? LinesFor(ExtensionColumn column)
        {
            if (selectionTimeRange != null)
                return snapGrid.Lines;

            return HoverColumn == column && CanPlace ? snapGrid.Lines : null;
        }

        private Column? findReferenceColumn()
        {
            foreach (var stage in playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    // Any drawn column has the geometry every column shares; the first laid-out one is enough.
                    if (column.HitObjectContainer.DrawHeight > 0)
                        return column;
                }
            }

            return null;
        }

        /// <summary>
        /// The note under <paramref name="screenSpacePosition"/>, if any.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.NoteAt(screenSpacePosition);

        /// <summary>
        /// The time under <paramref name="screenSpacePosition"/>, or <c>null</c> outside the columns.
        /// </summary>
        public double? TimeAt(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.TimeAt(screenSpacePosition) ?? referenceTimeAt(screenSpacePosition);

        /// <summary>
        /// The time under <paramref name="screenSpacePosition"/> through the mirrored playfield column.
        /// </summary>
        /// <remarks>
        /// Solved against the playfield's own scrolling container, so a press or drag snaps to exactly the time the
        /// playable columns would report for that row.
        /// </remarks>
        private double? referenceTimeAt(Vector2 screenSpacePosition)
        {
            var column = ReferenceColumn;

            if (column == null || column.HitObjectContainer.DrawHeight <= 0)
                return null;

            return column.TimeAtScreenSpacePosition(screenSpacePosition);
        }

        /// <summary>
        /// Whether <paramref name="screenSpacePosition"/> is over a per-column control rather than the note area.
        /// </summary>
        public bool IsOnControl(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.ContainsControl(screenSpacePosition) == true;

        /// <summary>
        /// The scrolling surface of <paramref name="column"/>, if it is currently displayed.
        /// </summary>
        public ExtensionLane? LaneOf(ExtensionColumn column) => lanes.GetValueOrDefault(column);

        /// <summary>
        /// The scrolling surfaces of the currently displayed columns.
        /// </summary>
        public IEnumerable<ExtensionLane> Lanes => lanes.Values;

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

        private void updateHover()
        {
            HoverColumn = null;
            IsHoveringNote = false;
            HoverTime = null;

            Vector2? mouse = currentMousePosition();
            var column = mouse == null ? null : DrawableColumnAt(mouse.Value);

            // The column's own controls (select-all) own their positions.
            if (mouse != null && column != null && !column.ContainsControl(mouse.Value) && column.TimeAt(mouse.Value) is double time)
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
        private void updateGrid()
        {
            if (selectionTimeRange is var (start, end))
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

            return inputManager?.CurrentState.Mouse.Position;
        }

        private void onColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var lane in lanes.Values)
                LaneRemoved?.Invoke(lane);

            lanes.Clear();

            // Rebuilt wholesale rather than spliced. Column numbers are positional, so the drawables must mirror
            // Columns exactly; incremental adds/removes can leave the flow ordered by insertion history, which makes
            // the labels read out of order even though the numbers themselves are contiguous.
            columnFlow.Clear();

            foreach (var column in columnSet.Columns)
            {
                var drawable = createColumn(column);

                lanes[column] = drawable.Lane;
                columnFlow.Add(drawable);
            }

            foreach (var lane in lanes.Values)
                LaneAdded?.Invoke(lane);

            placeholder.Alpha = columnSet.Columns.Count == 0 ? 1 : 0;
        }

        private DrawableExtensionColumn createColumn(ExtensionColumn column)
            => new DrawableExtensionColumn(column, this);

        protected override void Dispose(bool isDisposing)
        {
            columnSet.Columns.CollectionChanged -= onColumnsChanged;
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
