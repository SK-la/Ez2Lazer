// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osuTK;

namespace osu.Game.Screens.Footer
{
    public partial class ScreenBackButton : ScreenFooterButton
    {
        // The back button is wider than a regular footer button so that screens which place a button
        // directly above it (EzQuickRotationPickScreen) can align to the same width.
        public new const float BUTTON_WIDTH = 240;

        public ScreenBackButton()
        {
            Width = BUTTON_WIDTH;
        }

        public sealed override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            // Ensure clicks in the corner of the screen still trigger the back button.
            // Need to apply more than 1x inflation due to shear.
            var inputRectangle = DrawRectangle.Inflate(new MarginPadding
            {
                Left = OsuGame.SCREEN_EDGE_MARGIN,
                // Bottom = OsuGame.SCREEN_EDGE_MARGIN * 2,
            });

            return inputRectangle.Contains(ToLocalSpace(screenSpacePos));
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.ChevronLeft;
            Text = CommonStrings.Back;
            TooltipText = CommonStrings.Back;
            AccentColour = colours.Pink;
        }
    }
}
