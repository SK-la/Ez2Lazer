// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationResults
    {
        public static void TryAddContinueButton(FillFlowContainer buttons, IScreen screen)
        {
            if (!EzQuickRotationCoordinator.Session.IsActive)
                return;

            buttons.Insert(0, new RoundedButton
            {
                Text = EzQuickRotationStrings.CONTINUE_ROTATION,
                Width = 300,
                Action = () => screen.Push(new EzQuickRotationPickScreen()),
            });
        }

        public static bool TryHandleSelect(IScreen screen)
        {
            if (!EzQuickRotationCoordinator.Session.IsActive)
                return false;

            screen.Push(new EzQuickRotationPickScreen());
            return true;
        }
    }
}
