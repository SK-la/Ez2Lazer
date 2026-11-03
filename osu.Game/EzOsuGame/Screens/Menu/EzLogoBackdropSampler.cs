// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Screens.Menu
{
    /// <summary>
    /// Latest colour taken from the on-screen background for the logo's linked fill.
    /// Callers offer a colour only when the source image or video sample changes.
    /// </summary>
    public sealed class EzLogoBackdropSampler
    {
        public static readonly Colour4 DefaultColour = Colour4.FromHex("ff66ab");

        public Colour4 Colour { get; private set; } = DefaultColour;

        public void Offer(Colour4 colour)
        {
            if (colour == Colour)
                return;

            Colour = new Colour4(colour.R, colour.G, colour.B, 1);
        }
    }
}
