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
        private readonly SpriteIcon icon;

        public EzModLayoutModeButton()
        {
            Width = 156;
            Height = 42;
            TextSize = 16;
            ButtonContent.Padding = new MarginPadding { Left = 20 };
            ButtonContent.Add(icon = new SpriteIcon
            {
                Size = new Vector2(16),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            });
        }

        protected override void UpdateActiveState()
        {
            base.UpdateActiveState();
            Text = Active.Value ? EzModLayoutStrings.VERTICAL : EzModLayoutStrings.HORIZONTAL;
            icon.Icon = Active.Value ? FontAwesome.Solid.Bars : FontAwesome.Solid.Columns;
        }
    }
}
