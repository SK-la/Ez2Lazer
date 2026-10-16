// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mania.Edit.Blueprints;
using osu.Game.Rulesets.Mania.Edit.Extension;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Edit
{
    public partial class ManiaBlueprintContainer : ComposeBlueprintContainer
    {
        public new ManiaHitObjectComposer Composer => (ManiaHitObjectComposer)base.Composer;

        public ManiaBlueprintContainer(ManiaHitObjectComposer composer)
            : base(composer)
        {
        }

        public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(HitObject hitObject)
        {
            switch (hitObject)
            {
                case Note note:
                    return new NoteSelectionBlueprint(note);

                case HoldNote holdNote:
                    return new HoldNoteSelectionBlueprint(holdNote);
            }

            return base.CreateHitObjectBlueprintFor(hitObject);
        }

        protected override SelectionHandler<HitObject> CreateSelectionHandler() => new ManiaSelectionHandler();

        protected sealed override DragBox CreateDragBox() => new ScrollingDragBox(Composer.Playfield);

        private readonly HashSet<ExtensionNote> extensionSelectionBeforeDrag = new HashSet<ExtensionNote>();

        /// <summary>
        /// Whether the current box fills the extension columns instead of the playfield.
        /// </summary>
        /// <remarks>
        /// Decided by where the drag began, so a single box only ever selects one of the two regions.
        /// </remarks>
        private bool marqueeInExtensionColumns;

        private bool shouldDeferHoldHandleDrag(Vector2 screenSpacePos) =>
            CurrentTool is SelectTool
            && SelectionHandler.SelectedBlueprints.Any(b => b is HoldNoteSelectionBlueprint hold && hold.IsHandleAt(screenSpacePos));

        /// <summary>
        /// Whether this container takes positional input at <paramref name="screenSpacePos"/>.
        /// </summary>
        /// <remarks>
        /// The extension columns sit outside the playfield content's input area, so the blank space beside them is
        /// claimed here explicitly, which lets a selection box start there; their notes, their controls and placement
        /// tools keep the position for themselves.
        /// </remarks>
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => Composer.PlayfieldContent?.SelectionBoxStartsInExtensionColumns(screenSpacePos) ?? base.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Left && shouldDeferHoldHandleDrag(e.ScreenSpaceMouseDownPosition))
                return false;

            return base.OnMouseDown(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            extensionSelectionBeforeDrag.Clear();
            marqueeInExtensionColumns = false;

            if (e.ControlPressed)
                extensionSelectionBeforeDrag.UnionWith(Composer.PlayfieldContent?.SelectedExtensionNotes ?? Array.Empty<ExtensionNote>());

            if (shouldDeferHoldHandleDrag(e.ScreenSpaceMouseDownPosition))
                return false;

            if (!base.OnDragStart(e))
                return false;

            // A box belongs to the region it started in, so starting one drops the other region's selection.
            if (isExtensionColumnsSelection(e.ScreenSpaceMouseDownPosition))
            {
                marqueeInExtensionColumns = true;
                DeselectAll();
            }
            else
                Composer.PlayfieldContent?.ClearExtensionSelection();

            return true;
        }

        private bool isExtensionColumnsSelection(Vector2 screenSpacePos)
            => Composer.PlayfieldContent?.SelectionBoxStartsInExtensionColumns(screenSpacePos) == true;

        protected override bool OnClick(ClickEvent e)
        {
            // An empty click beside the columns clears their selection along with the playfield's, mirroring DeselectAll().
            if (!e.ControlPressed && isExtensionColumnsSelection(e.ScreenSpaceMousePosition))
                Composer.PlayfieldContent!.ClearExtensionSelection();

            return base.OnClick(e);
        }

        protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<HitObject> blueprint, Vector2[] originalSnapPositions)> blueprints)
        {
            Vector2 distanceTravelled = e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition;

            // The final movement position, relative to movementBlueprintOriginalPosition.
            Vector2 movePosition = blueprints.First().originalSnapPositions.First() + distanceTravelled;

            // Retrieve a snapped position.
            var result = Composer.FindSnappedPositionAndTime(movePosition);

            var referenceBlueprint = blueprints.First().blueprint;
            bool moved = SelectionHandler.HandleMovement(new MoveSelectionEvent<HitObject>(referenceBlueprint, result.ScreenSpacePosition - referenceBlueprint.ScreenSpaceSelectionPoint));
            if (moved)
                ApplySnapResultTime(result, referenceBlueprint.Item.StartTime);

            dragPosition = e.ScreenSpaceMousePosition;

            return moved;
        }

        private Vector2? dragPosition;

        /// <summary>
        /// Called whenever a drag operation completes, before any change transaction is committed.
        /// </summary>
        protected override void DragOperationCompleted()
        {
            base.DragOperationCompleted();

            var position = dragPosition;
            dragPosition = null;

            if (position == null)
                return;

            // A drop inside the extension columns crosses over to them; anything else stays a plain playfield move.
            Composer.TryDropSelectionIntoExtensionColumns(position.Value);
        }

        protected override void UpdateSelectionFromDragBox(HashSet<HitObject> selectionBeforeDrag)
        {
            // The box fills exactly one region: an extension box leaves the playfield selection alone (it was dropped
            // when the drag began) and never grows into the playfield, and the playfield box never reaches the columns.
            if (marqueeInExtensionColumns)
            {
                Composer.PlayfieldContent?.UpdateExtensionSelection(DragBox.Box.ScreenSpaceDrawQuad, extensionSelectionBeforeDrag);
                return;
            }

            base.UpdateSelectionFromDragBox(selectionBeforeDrag);
        }
    }
}
