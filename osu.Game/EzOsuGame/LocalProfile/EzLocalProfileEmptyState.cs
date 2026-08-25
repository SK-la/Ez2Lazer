// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileEmptyState : FillFlowContainer
    {
        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 12);
            Padding = new MarginPadding { Vertical = 48 };
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;

            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Icon = FontAwesome.Solid.ChartLine,
                    Size = new Vector2(40),
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Colour = colours.Content2,
                },
                new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_EMPTY_HINT,
                    Font = OsuFont.GetFont(size: 14),
                    Colour = colours.Content2,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                }
            };
        }
    }
}
