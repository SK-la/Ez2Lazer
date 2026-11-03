// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static partial class EzQuickRotationResults
    {
        public static void TryAddContinueButton(IList<ScreenFooterButton> buttons, IScreen screen)
        {
            if (!EzQuickRotationCoordinator.Session.IsActive)
                return;

            buttons.Insert(0, new FooterButtonQuickRotationContinue(() => screen.Push(new EzQuickRotationPickScreen())));
        }

        public static bool TryHandleSelect(IScreen screen)
        {
            if (!EzQuickRotationCoordinator.Session.IsActive)
                return false;

            screen.Push(new EzQuickRotationPickScreen());
            return true;
        }

        private partial class FooterButtonQuickRotationContinue : ScreenFooterButton
        {
            public FooterButtonQuickRotationContinue(Action action)
            {
                Action = action;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Icon = FontAwesome.Solid.Sync;
                Text = EzQuickRotationStrings.CONTINUE_ROTATION;
                TooltipText = EzQuickRotationStrings.CONTINUE_ROTATION;
                AccentColour = colours.Purple1;
            }
        }
    }
}
