// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Edit.Blueprints.Components;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Blueprints
{
    public partial class HoldNoteSelectionBlueprint : ManiaSelectionBlueprint<HoldNote>
    {
        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        [Resolved]
        private EditorBeatmap? editorBeatmap { get; set; }

        [Resolved]
        private ManiaHitObjectComposer? positionSnapProvider { get; set; }

        private EditBodyPiece body = null!;
        private EditHoldNoteEndPiece head = null!;
        private EditHoldNoteEndPiece tail = null!;

        protected new DrawableHoldNote DrawableObject => (DrawableHoldNote)base.DrawableObject;

        public HoldNoteSelectionBlueprint(HoldNote hold)
            : base(hold)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                body = new EditBodyPiece
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                },
                head = new EditHoldNoteEndPiece
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                },
                tail = new EditHoldNoteEndPiece
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            head.Height = EditHoldNoteEndPiece.GetInteractionHeight(DrawableObject.Head.DrawHeight);
            head.Y = HitObjectContainer.PositionAtTime(HitObject.Head.StartTime, HitObject.StartTime);

            float tailInteractionHeight = EditHoldNoteEndPiece.GetInteractionHeight(DrawableObject.Tail.DrawHeight);
            tail.Height = tailInteractionHeight;
            tail.Y = HitObjectContainer.PositionAtTime(HitObject.Tail.StartTime, HitObject.StartTime);
            Height = HitObjectContainer.LengthAtTime(HitObject.StartTime, HitObject.EndTime) + tailInteractionHeight;
        }

        protected override void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Origin = direction.NewValue == ScrollingDirection.Down ? Anchor.BottomCentre : Anchor.TopCentre;

            foreach (var child in InternalChildren)
                child.Anchor = Origin;

            head.Scale = tail.Scale = body.Scale = new Vector2(1, direction.NewValue == ScrollingDirection.Down ? 1 : -1);
        }

        public override Quad SelectionQuad => ScreenSpaceDrawQuad;

        public override Vector2 ScreenSpaceSelectionPoint => head.ScreenSpaceDrawQuad.Centre;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            IsHandleAt(screenSpacePos) || base.ReceivePositionalInputAt(screenSpacePos);

        internal bool IsHandleAt(Vector2 screenSpacePos) =>
            head.ReceivePositionalInputAt(screenSpacePos) || tail.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e) =>
            IsHandleAt(e.ScreenSpaceMouseDownPosition);

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!IsHandleAt(e.ScreenSpaceMouseDownPosition))
                return false;

            changeHandler?.BeginChange();
            return true;
        }

        protected override void OnDrag(DragEvent e) =>
            applyHandleDrag(e.ScreenSpaceMousePosition, isHeadAt(e.ScreenSpaceMouseDownPosition));

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (IsHandleAt(e.ScreenSpaceMouseDownPosition))
                changeHandler?.EndChange();

            base.OnDragEnd(e);
        }

        private bool isHeadAt(Vector2 screenSpacePos)
        {
            bool onHead = head.ReceivePositionalInputAt(screenSpacePos);
            bool onTail = tail.ReceivePositionalInputAt(screenSpacePos);

            if (onHead && onTail)
            {
                float headDist = Vector2.DistanceSquared(screenSpacePos, head.ScreenSpaceDrawQuad.Centre);
                float tailDist = Vector2.DistanceSquared(screenSpacePos, tail.ScreenSpaceDrawQuad.Centre);
                return headDist <= tailDist;
            }

            return onHead;
        }

        private void applyHandleDrag(Vector2 screenSpacePosition, bool dragHead)
        {
            if (dragHead)
            {
                double endTimeBeforeDrag = HitObject.EndTime;
                double proposedStartTime = positionSnapProvider?.FindSnappedPositionAndTime(screenSpacePosition).Time
                                           ?? HitObjectContainer.TimeAtScreenSpacePosition(screenSpacePosition);

                if (proposedStartTime >= endTimeBeforeDrag)
                    return;

                HitObject.StartTime = proposedStartTime;
                HitObject.EndTime = endTimeBeforeDrag;
            }
            else
            {
                double proposedEndTime = positionSnapProvider?.FindSnappedPositionAndTime(screenSpacePosition).Time
                                         ?? HitObjectContainer.TimeAtScreenSpacePosition(screenSpacePosition);

                if (HitObject.StartTime >= proposedEndTime)
                    return;

                HitObject.EndTime = proposedEndTime;
            }

            editorBeatmap?.Update(HitObject);
        }
    }
}
