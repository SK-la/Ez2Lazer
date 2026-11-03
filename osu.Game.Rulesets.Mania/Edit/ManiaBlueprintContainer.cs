// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mania.Edit.Blueprints;
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

        private bool shouldDeferHoldHandleDrag(Vector2 screenSpacePos) =>
            CurrentTool is SelectTool
            && SelectionHandler.SelectedBlueprints.Any(b => b is HoldNoteSelectionBlueprint hold && hold.IsHandleAt(screenSpacePos));

        /// <summary>
        /// Whether the press that started the current gesture was handed to a hold's end handle rather than this container.
        /// </summary>
        /// <remarks>
        /// Decided once on press. Selecting a hold switches the tool to selection, so re-deciding on drag start would
        /// swallow the drag of a press that landed on the head of a not-yet-selected hold.
        /// </remarks>
        private bool holdHandleDragDeferred;

        /// <summary>
        /// Whether this container takes positional input at <paramref name="screenSpacePos"/>.
        /// </summary>
        /// <remarks>
        /// The dock answers for its own positions through its overlay, so this container declines everything inside it. That
        /// is the only place the two regions split input; neither duplicates the other's interaction logic.
        /// </remarks>
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => Composer.PlayfieldContent?.ContainsExtensionColumns(screenSpacePos) != true && base.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            holdHandleDragDeferred = e.Button == MouseButton.Left && shouldDeferHoldHandleDrag(e.ScreenSpaceMouseDownPosition);

            return !holdHandleDragDeferred && base.OnMouseDown(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (holdHandleDragDeferred)
            {
                holdHandleDragDeferred = false;
                return false;
            }

            return base.OnDragStart(e);
        }

        protected override void SelectAll()
        {
            var content = Composer.PlayfieldContent;

                Vector2? mouse = GetContainingInputManager()?.CurrentState.Mouse.Position;

                // Ctrl+A acts on the region the pointer is over, matching the playfield's own behaviour of acting on what is
                // in front of the user rather than on a hidden selection. A dock selection left behind by earlier work
                // must not redirect the playfield's Ctrl+A.
                if (content != null && mouse != null && content.ContainsExtensionColumns(mouse.Value))
                {
                    content.ExtensionOverlay.SelectAllNotes();
                    return;
                }

            base.SelectAll();
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
    }
}
