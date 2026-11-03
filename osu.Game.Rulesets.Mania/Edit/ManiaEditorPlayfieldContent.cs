// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Mania.Edit.Extension;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
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

        /// <summary>
        /// The columns this content hosts. Their notes are session state, contributing their own block to undo snapshots.
        /// </summary>
        internal ExtensionColumnSet ColumnSet { get; } = new ExtensionColumnSet();

        /// <summary>
        /// The dock's interaction overlay, which owns placement and selection of extension notes.
        /// </summary>
        internal ExtensionBlueprintContainer ExtensionOverlay { get; }

        private readonly ManiaPlayfield playfield;
        private readonly IScrollingInfo scrollingInfo;
        private readonly float rightReserve;

        private readonly HoverActivationContainer playfieldRegion;
        private readonly HoverActivationContainer dockRegion;

        private readonly DockScrollContainer dockScrollContainer;
        private readonly ExtensionArea extensionArea;
        private readonly ExtensionColumnInsertOverlay insertOverlay;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private IEditorChangeHandler? changeHandler { get; set; }

        /// <param name="rightReserve">
        /// Screen-space width kept clear on the right for the composer's toolboxes.
        /// </param>
        /// <param name="scrollingInfo">
        /// The editor ruleset's scroll mapping. The dock sits beside the ruleset rather than inside it, so the mapping is
        /// re-provided here for the columns to use.
        /// </param>
        /// <param name="placementMode">
        /// What a placement gesture in the columns should create, following the composer's active tool.
        /// </param>
        /// <param name="columnEditEnabled">
        /// Whether column-edit mode is on. It gates the columns' insert and remove controls, so they stay hidden until
        /// the editor's column-edit switch is turned on.
        /// </param>
        public ManiaEditorPlayfieldContent(Drawable playfieldContent, ManiaPlayfield playfield, IScrollingInfo scrollingInfo, float rightReserve,
                                           Func<ExtensionPlacementMode>? placementMode = null, Func<bool>? columnEditEnabled = null)
        {
            this.playfield = playfield;
            this.scrollingInfo = scrollingInfo;
            this.rightReserve = rightReserve;

            RelativeSizeAxes = Axes.Both;

            extensionArea = new ExtensionArea(ColumnSet, playfield)
            {
                PlacementMode = placementMode == null ? null : () => placementMode.Invoke(),
            };

            ExtensionOverlay = new ExtensionBlueprintContainer(ColumnSet, extensionArea, playfield)
            {
                OwnsInputAt = ownsExtensionInput,
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
                        ExtensionOverlay,
                        insertOverlay = new ExtensionColumnInsertOverlay(ColumnSet, extensionArea)
                        {
                            Enabled = columnEditEnabled,
                            InsertRequested = requestInsertColumn,
                            RemoveRequested = requestRemoveColumn,
                        },
                    },
                },
            };
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IScrollingInfo>(scrollingInfo);
            return dependencies;
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
        /// What a placement gesture in the columns creates right now.
        /// </summary>
        internal ExtensionPlacementMode PlacementMode => extensionArea.Mode;

        /// <summary>
        /// The time range whose beat lines the columns should show, when their selection owns them.
        /// </summary>
        internal (double start, double end)? ExtensionSelectionTimeRange
        {
            set => extensionArea.SelectionTimeRange = value;
        }

        /// <summary>
        /// Whether <paramref name="screenSpacePosition"/> lies inside the extension column dock.
        /// </summary>
        public bool ContainsExtensionColumns(Vector2 screenSpacePosition) => dockRegion.Contains(screenSpacePosition);

        /// <summary>
        /// Whether the dock's interaction overlay should claim <paramref name="screenSpacePosition"/>.
        /// </summary>
        /// <remarks>
        /// The single place where dock input is split between interaction, the column controls and the dock's scrollbar.
        /// The playfield's container asks the same question, so only one region ever answers for a position.
        /// </remarks>
        private bool ownsExtensionInput(Vector2 screenSpacePosition)
        {
            if (!dockRegion.Contains(screenSpacePosition))
                return false;

            if (extensionArea.IsOnControl(screenSpacePosition))
                return false;

            if (insertOverlay.OwnsInputAt(screenSpacePosition))
                return false;

            return !dockScrollContainer.IsOnScrollbar(screenSpacePosition);
        }

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
                ColumnSet.Add(column.Column, ExtensionNoteConversion.ToExtensionNote(hitObject));

            return true;
        }

        private void requestInsertColumn(int index)
        {
            changeHandler?.BeginChange();
            ColumnSet.Insert(index);
            changeHandler?.EndChange();
        }

        /// <summary>
        /// Removes <paramref name="column"/>, confirming first when it still holds notes.
        /// </summary>
        private void requestRemoveColumn(ExtensionColumn column)
        {
            if (column.Notes.Count == 0 || dialogOverlay == null)
            {
                removeColumn(column);
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(
                LocalisableString.Format(EzEditorStrings.EXTENSION_COLUMN_REMOVE_WITH_NOTES, column.Number.Value, column.Notes.Count),
                () => removeColumn(column)));
        }

        private void removeColumn(ExtensionColumn column)
        {
            changeHandler?.BeginChange();
            ColumnSet.Remove(column);
            changeHandler?.EndChange();
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
