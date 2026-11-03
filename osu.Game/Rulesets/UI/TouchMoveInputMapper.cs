// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// Translates touch and mouse input into direct positional movement for rulesets which normally move by holding
    /// buttons, and consumes the movement-related bindings so the two schemes do not fight.
    /// </summary>
    /// <remarks>
    /// Exactly one touch drives positioning at a time: the first touch to go down keeps ownership until it is lifted,
    /// so extra fingers on the screen cannot displace the target. Touch is reported as an absolute position, while
    /// mouse is reported as the distance travelled since the last event, since a mouse has no meaningful position
    /// within the playfield. Mouse tracking is abandoned as soon as a touch goes down, which keeps the two sources
    /// from cross-talking on touchscreens that also report their contacts through the mouse state.
    /// </remarks>
    /// <typeparam name="TAction">The ruleset action type handled by this mapper.</typeparam>
    public abstract partial class TouchMoveInputMapper<TAction> : Drawable, IKeyBindingHandler<TAction>
        where TAction : struct
    {
        private TouchSource? trackedTouch;
        private float touchScreenSpaceX;
        private bool mouseTracking = true;
        private bool wasMovementLocked;

        /// <summary>
        /// Whether the action classified as <see cref="TouchMoveAction.Dash"/> is currently held.
        /// </summary>
        protected bool DashHeld { get; private set; }

        /// <summary>
        /// Whether movement should be frozen, for example while an emergency stop is held.
        /// </summary>
        protected virtual bool MovementLocked => false;

        // Positioning must respond anywhere on screen, not only inside the mapper's own bounds.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        /// <summary>
        /// Invoked when the tracked touch changes its screen space X.
        /// </summary>
        protected abstract void OnTouchPositionChanged(float screenSpaceX);

        /// <summary>
        /// Invoked with the mouse travel distance in screen space units, which may be negative.
        /// </summary>
        protected abstract void OnMouseMoveBy(float screenSpaceDeltaX);

        /// <summary>
        /// Classifies a ruleset action into the movement role this mapper cares about.
        /// Actions classified as <see cref="TouchMoveAction.None"/> are left for the ruleset to handle.
        /// </summary>
        protected abstract TouchMoveAction ClassifyAction(TAction action);

        protected override void Update()
        {
            base.Update();

            // Releasing the emergency stop should snap back to the finger rather than resume from a stale position.
            if (wasMovementLocked && !MovementLocked && trackedTouch != null)
                OnTouchPositionChanged(touchScreenSpaceX);

            wasMovementLocked = MovementLocked;
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            if (trackedTouch != null)
                return true;

            trackedTouch = e.Touch.Source;
            mouseTracking = false;

            updateTouchPosition(e.ScreenSpaceTouch.Position.X);

            // Consume the event so any ruleset touch-to-button mapping does not see this drag.
            return true;
        }

        protected override void OnTouchMove(TouchMoveEvent e)
        {
            if (e.Touch.Source == trackedTouch)
                updateTouchPosition(e.ScreenSpaceTouch.Position.X);

            base.OnTouchMove(e);
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            if (e.Touch.Source == trackedTouch)
                trackedTouch = null;

            base.OnTouchUp(e);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!mouseTracking || MovementLocked)
                return false;

            float deltaX = e.ScreenSpaceMousePosition.X - e.ScreenSpaceLastMousePosition.X;

            if (deltaX != 0)
                OnMouseMoveBy(deltaX);

            return false;
        }

        public bool OnPressed(KeyBindingPressEvent<TAction> e)
        {
            TouchMoveAction action = ClassifyAction(e.Action);

            if (action == TouchMoveAction.Dash)
                DashHeld = true;

            // Movement actions are always consumed: direct positioning replaces button movement entirely.
            // Dash is consumed too, so its press and release stay paired here rather than reaching the ruleset.
            return action != TouchMoveAction.None;
        }

        public void OnReleased(KeyBindingReleaseEvent<TAction> e)
        {
            if (ClassifyAction(e.Action) == TouchMoveAction.Dash)
                DashHeld = false;
        }

        private void updateTouchPosition(float screenSpaceX)
        {
            touchScreenSpaceX = screenSpaceX;

            if (!MovementLocked)
                OnTouchPositionChanged(screenSpaceX);
        }
    }

    /// <summary>
    /// The movement role of a ruleset action, as far as touch-based movement is concerned.
    /// </summary>
    public enum TouchMoveAction
    {
        /// <summary>
        /// An action unrelated to movement, left for the ruleset to handle normally.
        /// </summary>
        None,

        /// <summary>
        /// An action which would move the target left.
        /// </summary>
        MoveLeft,

        /// <summary>
        /// An action which would move the target right.
        /// </summary>
        MoveRight,

        /// <summary>
        /// An action which would accelerate movement.
        /// </summary>
        Dash,
    }
}
