// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Events;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Pets
{
    public partial class EzDesktopPetLayer
    {
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            // Only claim the pointer while LAlt is held; otherwise clicks pass through.
            return e.Button == MouseButton.Left && isLeftAlt(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left || !isLeftAlt(e))
                return false;

            dragging = true;
            motionDriver.Stop();
            stateMachine.HandleDrag();
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (!dragging)
                return;

            petBox.Position += e.Delta;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (!dragging)
                return;

            dragging = false;
            persistPosition();
            stateMachine.HandleDragEnd();
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == MouseButton.Left && dragging && !e.HasAnyButtonPressed)
            {
                dragging = false;
                persistPosition();
                stateMachine.HandleDragEnd();
            }

            base.OnMouseUp(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left || !isLeftAlt(e))
                return false;

            stateMachine.HandleClick();
            return true;
        }

        private static bool isLeftAlt(UIEvent e) => e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt);
    }
}
