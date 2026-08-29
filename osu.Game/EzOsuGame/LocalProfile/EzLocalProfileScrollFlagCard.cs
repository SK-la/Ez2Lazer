// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileScrollFlagCard : Container
    {
        public const float HEADER_HEIGHT = 32;
        public const float BODY_HEIGHT = 60;
        public const float CARD_HEIGHT = HEADER_HEIGHT + BODY_HEIGHT;

        public EzLocalProfileScrollFlagCard(LocalisableString title, string headerValue, string max, string min, string avg)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new Header(title, headerValue),
                    new Body(max, min, avg),
                },
            };
        }

        private partial class Header : Container
        {
            private readonly Box background;
            private readonly OsuTextFlowContainer headerText;

            public Header(LocalisableString title, string headerValue)
            {
                RelativeSizeAxes = Axes.X;
                Height = HEADER_HEIGHT;
                Masking = true;
                CornerRadius = 8;

                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    headerText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold))
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 8, Vertical = 6 },
                    },
                };

                headerText.AddText(title);
                headerText.AddText($" {headerValue}");
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                background.Colour = colours.Highlight1;
                headerText.Colour = colours.Content1;
            }
        }

        private partial class Body : Container
        {
            private readonly Box background;
            private readonly OsuSpriteText maxText;
            private readonly OsuSpriteText minText;
            private readonly OsuSpriteText avgText;

            public Body(string max, string min, string avg)
            {
                RelativeSizeAxes = Axes.X;
                Height = BODY_HEIGHT;
                Masking = true;
                CornerRadius = 8;

                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 8, Vertical = 8 },
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            maxText = new OsuSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = $"Max  {max}",
                                Font = OsuFont.GetFont(size: 13),
                            },
                            avgText = new OsuSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = $"Avg  {avg}",
                                Font = OsuFont.GetFont(size: 13),
                            },
                            minText = new OsuSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = $"Min  {min}",
                                Font = OsuFont.GetFont(size: 13),
                            },
                        },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                background.Colour = colours.Background5;
                maxText.Colour = colours.Content1;
                minText.Colour = colours.Content1;
                avgText.Colour = colours.Content1;
            }
        }
    }
}
