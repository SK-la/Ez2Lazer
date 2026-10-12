// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play;

namespace osu.Game.Screens.Ranking
{
    public partial class RetryButton : ScreenFooterButton
    {
        [Resolved]
        private Player? player { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            string label = player is ReplayPlayer ? @"replay" : @"retry";

            Icon = FontAwesome.Solid.Redo;
            AccentColour = colours.Lime1;
            Text = label;
            TooltipText = label;

            if (player != null)
                Action = () => player.Restart();
        }
    }
}
