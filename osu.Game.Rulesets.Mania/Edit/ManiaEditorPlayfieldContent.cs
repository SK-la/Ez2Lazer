// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Mania.Edit.Extension;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Hosts the mania editor playfield alongside the extension columns.
    /// </summary>
    /// <remarks>
    /// The playfield keeps the exact size, scale and column placement it has in gameplay. The extension columns are
    /// docked in the space the playfield canvas leaves unused to the right of its columns, so both stay visible at once.
    /// </remarks>
    public partial class ManiaEditorPlayfieldContent : CompositeDrawable
    {
        private const float dock_gap = 8;

        private readonly ExtensionColumnSet columnSet = new ExtensionColumnSet();

        private readonly ManiaPlayfield playfield;
        private readonly float rightReserve;
        private readonly Func<bool>? selectToolActive;

        private readonly HoverActivationContainer playfieldRegion;
        private readonly HoverActivationContainer dockRegion;

        private readonly DockScrollContainer dockScrollContainer;
        private readonly ExtensionArea extensionArea;
        private readonly ExtensionColumnInsertOverlay insertOverlay;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        /// <param name="rightReserve">
        /// Screen-space width kept clear on the right for the composer's toolboxes.
        /// </param>
        /// <param name="selectToolActive">
        /// Whether the composer's selection tool is active. It gates placement in the columns and marks the columns'
        /// blank space as shared with the playfield's selection box.
        /// </param>
        /// <param name="columnEditEnabled">
        /// Whether column-edit mode is on. It gates the columns' insert and remove controls, so they stay hidden until
        /// the editor's column-edit switch is turned on.
        /// </param>
        public ManiaEditorPlayfieldContent(Drawable playfieldContent, ManiaPlayfield playfield, float rightReserve, Func<bool>? selectToolActive = null,
                                           Func<bool>? columnEditEnabled = null)
        {
            this.playfield = playfield;
            this.rightReserve = rightReserve;
            this.selectToolActive = selectToolActive;

            RelativeSizeAxes = Axes.Both;

            extensionArea = new ExtensionArea(columnSet, playfield)
            {
                PlacementEnabled = () => !(selectToolActive?.Invoke() ?? false),
                SelectionTaken = () => ExtensionSelectionTaken?.Invoke(),
            };

            InternalChildren = new Drawable[]
            {
                playfieldRegion = new HoverActivationContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Child = playfieldContent,
                },
                dockRegion = new HoverActivationContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        dockScrollContainer = new DockScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = extensionArea,
                        },
                        new ExtensionEditArea(columnSet, extensionArea, playfield)
                        {
                            RelativeSizeAxes = Axes.Both,
                            ExcludePosition = position => dockScrollContainer.IsOnScrollbar(position) || extensionArea.IsOnControl(position),
                        },
                        insertOverlay = new ExtensionColumnInsertOverlay(columnSet, extensionArea)
                        {
                            Enabled = columnEditEnabled,
                            InsertRequested = index => columnSet.Insert(index),
                            RemoveRequested = requestRemoveColumn,
                        },
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            // The playfield keeps occupying the full viewport; the extension columns are docked to the right of its columns.
            if (Parent != null)
                playfieldRegion.Width = Parent.DrawWidth;

            float left = columnsRightInLocalSpace() + dock_gap;

            dockRegion.X = left;
            dockRegion.Width = Math.Max(0, DrawWidth - rightReserve - left);
        }

        /// <summary>
        /// Whether <paramref name="screenSpacePosition"/> lies inside the extension column dock.
        /// </summary>
        public bool ContainsExtensionColumns(Vector2 screenSpacePosition) => dockRegion.Contains(screenSpacePosition);

        /// <summary>
        /// Raised when the extension columns take a selection, so the playfield's can be dropped.
        /// </summary>
        /// <remarks>
        /// The two regions keep a single selection between them; see <see cref="ExtensionArea.SelectionTaken"/>.
        /// </remarks>
        public event Action? ExtensionSelectionTaken;

        /// <summary>
        /// Whether a selection box starting at <paramref name="screenSpacePosition"/> belongs to the extension columns.
        /// </summary>
        /// <returns>
        /// <c>null</c> when the position is not over the dock at all and the caller's own answer applies.
        /// </returns>
        /// <remarks>
        /// The columns keep their notes and their own controls, and a placement tool owns the whole dock; what the
        /// playfield shares is the dock's blank space, so that a selection box may start there.
        /// </remarks>
        public bool? SelectionBoxStartsInExtensionColumns(Vector2 screenSpacePosition)
        {
            if (!dockRegion.Contains(screenSpacePosition))
                return null;

            if (extensionArea.OwnsNoteAt(screenSpacePosition)
                || extensionArea.IsOnControl(screenSpacePosition)
                || insertOverlay.OwnsInputAt(screenSpacePosition)
                || dockScrollContainer.IsOnScrollbar(screenSpacePosition))
                return false;

            return selectToolActive?.Invoke() ?? false;
        }

        /// <summary>
        /// The notes currently selected across the extension columns.
        /// </summary>
        public IReadOnlyCollection<ExtensionNote> SelectedExtensionNotes => extensionArea.SelectedNotes;

        /// <summary>
        /// Sets the time range whose beat lines the extension columns should show, or <c>null</c> to follow the cursor.
        /// </summary>
        public (double start, double end)? SelectionTimeRange
        {
            set => extensionArea.SelectionTimeRange = value;
        }

        /// <summary>
        /// The screen-space horizontal extent of the extension columns, used to widen their selection box.
        /// </summary>
        public (float left, float right)? ExtensionColumnsScreenSpaceBounds => extensionArea.ColumnScreenSpaceBounds;

        /// <summary>
        /// Clears the extension column selection.
        /// </summary>
        public void ClearExtensionSelection() => extensionArea.ClearSelection();

        /// <summary>
        /// Selects every note across the extension columns.
        /// </summary>
        public void SelectAllExtensionNotes() => extensionArea.SelectAllNotes();

        /// <summary>
        /// Removes the notes currently selected across the extension columns.
        /// </summary>
        /// <returns>Whether any note was removed.</returns>
        public bool DeleteExtensionSelection() => extensionArea.RemoveSelected() > 0;

        /// <summary>
        /// Applies a selection box to the extension notes, deselecting the rest.
        /// </summary>
        /// <param name="selectionQuad">The box's screen-space rectangle.</param>
        /// <param name="preserve">Notes which were already selected when the drag started, kept regardless of the box.</param>
        public void UpdateExtensionSelection(Quad selectionQuad, HashSet<ExtensionNote>? preserve) => extensionArea.UpdateBoxSelection(selectionQuad, preserve);

        /// <summary>
        /// Hands <paramref name="hitObjects"/> over to the extension columns, dropping them on the column under
        /// <paramref name="screenSpacePosition"/>.
        /// </summary>
        /// <returns>Whether the objects were accepted; <c>false</c> leaves them untouched.</returns>
        /// <remarks>
        /// Times and lengths are kept as they are, so a dropped object stays on the same row of the shared time axis.
        /// The source objects are not removed here: the caller owns the playfield side of the move.
        /// </remarks>
        public bool DropIntoExtensionColumns(IReadOnlyList<ManiaHitObject> hitObjects, Vector2 screenSpacePosition)
        {
            var column = extensionArea.DrawableColumnAtOrNearest(screenSpacePosition);

            if (column == null || hitObjects.Count == 0)
                return false;

            foreach (var hitObject in hitObjects)
                columnSet.Add(column.Column, ExtensionNoteConversion.ToExtensionNote(hitObject));

            return true;
        }

        /// <summary>
        /// Removes <paramref name="column"/>, confirming first when it still holds notes.
        /// </summary>
        private void requestRemoveColumn(ExtensionColumn column)
        {
            if (column.Notes.Count == 0 || dialogOverlay == null)
            {
                columnSet.Remove(column);
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(
                LocalisableString.Format(EzEditorStrings.EXTENSION_COLUMN_REMOVE_WITH_NOTES, column.Number.Value, column.Notes.Count),
                () => columnSet.Remove(column)));
        }

        /// <summary>
        /// The right edge of the playfield columns, in this container's space.
        /// </summary>
        /// <remarks>
        /// Measured rather than derived, so it follows skin scaling, dual stages and any stage placement offset.
        /// </remarks>
        private float columnsRightInLocalSpace()
        {
            float right = float.NegativeInfinity;

            foreach (var stage in playfield.Stages)
            {
                var quad = stage.ScreenSpaceDrawQuad.AABBFloat;

                if (quad.Width <= 0)
                    continue;

                right = Math.Max(right, ToLocalSpace(new Vector2(quad.Right, quad.Centre.Y)).X);
            }

            // Before the stages have been laid out there is nothing to measure; keep the columns clear of the centre.
            return float.IsNegativeInfinity(right) ? DrawWidth * 0.5f : right;
        }

        private partial class DockScrollContainer : OsuScrollContainer
        {
            public DockScrollContainer()
                : base(Direction.Horizontal)
            {
            }

            // The wheel stays reserved for the editor timeline; the dock scrolls by dragging its own scrollbar.
            protected override bool OnScroll(ScrollEvent e) => false;

            /// <summary>
            /// Whether <paramref name="screenSpacePosition"/> belongs to the scrollbar row rather than the scrollable content.
            /// </summary>
            /// <remarks>
            /// Content is padded by the scrollbar while it is visible, so excluding its row keeps note input off non-content space.
            /// </remarks>
            public bool IsOnScrollbar(Vector2 screenSpacePosition)
            {
                if (Scrollbar.Alpha <= 0)
                    return false;

                return screenSpacePosition.Y >= Scrollbar.ScreenSpaceDrawQuad.AABBFloat.Top - 2;
            }

            // Panning the columns is an explicit gesture so that a plain left-drag stays owned by selection and note
            // interactions (the base implementation would also swallow the follow-up click).
            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left || !e.ShiftPressed)
                    return false;

                return base.OnDragStart(e);
            }
        }
    }
}
