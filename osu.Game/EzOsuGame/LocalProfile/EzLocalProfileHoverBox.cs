// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Shared Background5→6 hover/active fill for Local Profile clickables.
    /// Transparent-idle uses alpha fades (not FadeColour→Transparent) to avoid a white flash.
    /// </summary>
    public partial class EzLocalProfileHoverBox : Box
    {
        private Colour4 idleColour;
        private Colour4 hoverColour;
        private Colour4 activeColour;
        private bool selected;
        private bool interactive = true;
        private bool transparentIdle;

        public EzLocalProfileHoverBox()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void Configure(OverlayColourProvider colours, bool transparentIdle = false)
        {
            this.transparentIdle = transparentIdle;

            // Keep a solid idle colour even when transparent-idle: fade Alpha instead of Colour→Transparent
            // (RGB lerp into Transparent flashes white).
            idleColour = colours.Background5;
            hoverColour = transparentIdle ? colours.Background5 : colours.Background6;
            activeColour = colours.Background6;
            Colour = idleColour;
            Alpha = transparentIdle ? 0 : 1;
        }

        public void SetInteractive(bool value)
        {
            interactive = value;
            if (!interactive)
                apply(idleColour, transparentIdle ? 0 : 1);
        }

        public void SetSelected(bool value)
        {
            selected = value;
        }

        public void Refresh(bool hovered)
        {
            if (!interactive)
            {
                apply(idleColour, transparentIdle ? 0 : 1);
                return;
            }

            bool lit = selected || hovered;
            Colour4 targetColour = selected ? activeColour : hovered ? hoverColour : idleColour;
            float targetAlpha = transparentIdle ? (lit ? 1 : 0) : 1;
            apply(targetColour, targetAlpha);
        }

        private void apply(Colour4 colour, float alpha)
        {
            this.FadeColour(colour, 120, Easing.OutQuint);
            this.FadeTo(alpha, 120, Easing.OutQuint);
        }
    }
}
