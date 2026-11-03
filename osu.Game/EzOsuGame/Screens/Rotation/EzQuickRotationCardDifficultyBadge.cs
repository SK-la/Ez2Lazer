// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    /// <summary>
    /// Ranked-play card badge matching upstream layout; uses moon icons when displaying xxy SR.
    /// </summary>
    public partial class EzQuickRotationCardDifficultyBadge : CompositeDrawable
    {
        private readonly BeatmapInfo beatmap;

        public EzQuickRotationCardDifficultyBadge(BeatmapInfo beatmap)
        {
            this.beatmap = beatmap;
        }

        [BackgroundDependencyLoader]
        private void load(RankedPlayCardContent.CardColours colours)
        {
            double rating = EzQuickRotationCardDifficultyDisplay.ResolveBadgeRating(beatmap);
            var icon = EzQuickRotationCardDifficultyDisplay.ResolveBadgeIcon(beatmap);

            AutoSizeAxes = Axes.Y;
            Width = RankedPlayCard.SIZE.X - 20;

            Masking = true;
            CornerRadius = 3;

            InternalChildren =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Primary,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 3, Vertical = 1 },
                    ColumnDimensions =
                    [
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    ],
                    RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
                    Content = new Drawable[][]
                    {
                        [
                            new RatingIconsDisplay(rating, icon)
                            {
                                StarSize = 6,
                                Colour = colours.OnPrimary,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            },
                            new TruncatingSpriteText
                            {
                                Text = rating.FormatStarRating(),
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Font = OsuFont.GetFont(size: 9, weight: FontWeight.Bold),
                                Colour = colours.OnPrimary,
                            },
                        ]
                    }
                }
            ];
        }

        private partial class RatingIconsDisplay(double rating, IconUsage icon) : CompositeDrawable
        {
            public required float StarSize { get; init; }

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.Both;

                FillFlowContainer flow;

                InternalChild = flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(1),
                };

                int numStars = (int)rating - 1;

                for (int i = 0; i <= numStars; i++)
                {
                    flow.Add(new SpriteIcon
                    {
                        Size = new Vector2(StarSize),
                        Icon = icon,
                    });
                }

                float lastStarWidth = (int)((rating % 1) * 4) / 4f;

                if (lastStarWidth > 0)
                {
                    flow.Add(new Container
                    {
                        Size = new Vector2(StarSize * lastStarWidth, StarSize),
                        Masking = true,
                        Child = new SpriteIcon
                        {
                            Icon = icon,
                            Size = new Vector2(StarSize),
                        }
                    });
                }
            }
        }
    }
}
