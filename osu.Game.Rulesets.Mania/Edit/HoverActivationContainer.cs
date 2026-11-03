// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Fades its content between full and reduced opacity depending on whether the cursor is inside its bounds.
    /// </summary>
    /// <remarks>
    /// Used so adjacent editor regions (playfield, background tracks) each indicate whether they are the active one.
    /// </remarks>
    public partial class HoverActivationContainer : Container
    {
        private const float inactive_alpha = 0.8f;
        private const double fade_duration = 100;

        private InputManager? inputManager;
        private bool? isActive;

        protected override void Update()
        {
            base.Update();

            inputManager ??= GetContainingInputManager();

            if (inputManager == null)
                return;

            bool active = Contains(inputManager.CurrentState.Mouse.Position);

            if (active == isActive)
                return;

            isActive = active;
            this.FadeTo(active ? 1 : inactive_alpha, fade_duration);
        }
    }
}
