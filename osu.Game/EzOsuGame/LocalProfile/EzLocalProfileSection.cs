// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Profile-section style card (rounded, shadowed, title underline).
    /// </summary>
    public partial class EzLocalProfileSection : Container
    {
        private const float outer_gutter_width = 10;

        private readonly LocalisableString title;
        private readonly Drawable body;
        private Box background = null!;
        private Box underscore = null!;

        public EzLocalProfileSection(LocalisableString title, Drawable body)
        {
            this.title = title;
            this.body = body;

            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 10,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Offset = new Vector2(0, 1),
                        Radius = 3,
                        Colour = Colour4.Black.Opacity(0.25f),
                    },
                    Child = background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
                new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding
                            {
                                Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING - outer_gutter_width,
                                Top = 20,
                                Bottom = 12,
                            },
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = title,
                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                },
                                underscore = new Box
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.TopCentre,
                                    Margin = new MarginPadding { Top = 4 },
                                    RelativeSizeAxes = Axes.X,
                                    Height = 2,
                                }
                            }
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding
                            {
                                Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING - outer_gutter_width,
                                Bottom = 20
                            },
                            Child = body
                        }
                    }
                }
            };

            background.Colour = colourProvider.Background4;
            underscore.Colour = colourProvider.Highlight1;
        }
    }
}
