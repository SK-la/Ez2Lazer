// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Edit;
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

        private FillFlowContainer<DrawableExtensionColumn> columnFlow = null!;
        private Drawable placeholder = null!;

        private readonly Dictionary<ExtensionColumn, ExtensionLane> lanes = new Dictionary<ExtensionColumn, ExtensionLane>();

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        /// <summary>
        /// Whether the columns accept placement gestures.
        /// </summary>
        /// <remarks>
        /// Gates the placement preview as well: while the composer's selection tool is active, the dock's blank space
        /// belongs to the playfield's selection box and must not read as a placement surface.
        /// </remarks>
        public Func<bool>? PlacementEnabled { get; set; }

        public bool CanPlace => PlacementEnabled?.Invoke() != false;

        /// <summary>
        /// The snapped time under the cursor while the columns accept placement, or <c>null</c>.
        /// </summary>
        internal double? PlacementHoverTime => CanPlace ? HoverTime : null;

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
        /// Raised when a column's scrolling surface becomes available.
        /// </summary>
        public event Action<ExtensionLane>? LaneAdded;

        /// <summary>
        /// Raised when a column's scrolling surface is about to be dropped.
        /// </summary>
        public event Action<ExtensionLane>? LaneRemoved;

        public ExtensionArea(ExtensionColumnSet columnSet)
        {
            this.columnSet = columnSet;

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
        }

        protected override void Update()
        {
            base.Update();

            updateHover();
        }

        /// <summary>
        /// The note under <paramref name="screenSpacePosition"/>, if any.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.NoteAt(screenSpacePosition);

        /// <summary>
        /// The time under <paramref name="screenSpacePosition"/>, or <c>null</c> outside the columns.
        /// </summary>
        public double? TimeAt(Vector2 screenSpacePosition) => DrawableColumnAt(screenSpacePosition)?.TimeAt(screenSpacePosition);

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
