// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    internal partial class EzModLayoutModeButton : ShearedToggleButton
    {
        protected override bool PlayToggleSamples => false;

        private readonly SpriteIcon icon;

        public EzModLayoutModeButton()
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
            TextSize = 16;
            // Padding = new MarginPadding { Left = 20, Right = 10 };
            ButtonContent.Padding = new MarginPadding { Left = 34, Right = 10 };
            ButtonContent.Add(icon = new SpriteIcon
            {
                Size = new Vector2(16),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = -10,
            });
        }

        protected override void UpdateActiveState()
        {
            // DarkerColour = Active.Value ? ColourProvider.Highlight1 : ColourProvider.Background3;
            // LighterColour = Active.Value ? ColourProvider.Colour0 : ColourProvider.Background1;
            // TextColour = Active.Value ? ColourProvider.Background6 : ColourProvider.Content1;
            // BackgroundLayer.TransformTo(nameof(BorderColour), ColourInfo.GradientVertical(DarkerColour, colourLight), 150, Easing.OutQuint);

            Text = Active.Value ? EzModLayoutStrings.VERTICAL : EzModLayoutStrings.HORIZONTAL;
            icon.Icon = Active.Value ? FontAwesome.Solid.Bars : FontAwesome.Solid.Columns;
        }
    }
}
