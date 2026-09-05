// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public sealed partial class EzQuickRotationRankedPlayCardContent : RankedPlayCardContent
    {
        private readonly BeatmapInfo localCoverBeatmap;

        public EzQuickRotationRankedPlayCardContent(APIBeatmap beatmap, BeatmapInfo localCoverBeatmap)
            : base(beatmap)
        {
            this.localCoverBeatmap = localCoverBeatmap;
        }

        protected override Drawable CreateCardCover() => new EzQuickRotationLocalCardCoverAdapter(localCoverBeatmap);

        protected override Drawable CreateCardMetadata() => new EzQuickRotationCardMetadata(Beatmap, localCoverBeatmap) { RelativeSizeAxes = Axes.Both };

        private partial class EzQuickRotationCardMetadata(APIBeatmap beatmap, BeatmapInfo localBeatmap) : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load(CardColours colours)
            {
                InternalChildren =
                [
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children =
                        [
                            new EzQuickRotationCardDifficultyBadge(localBeatmap)
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Margin = new MarginPadding { Top = 4 },
                            },
                        ]
                    },
                    new LinkFlowContainer(static s => s.ShadowOffset = new Vector2(0, 0.15f))
                    {
                        Name = "Beatmap Metadata",
                        RelativeSizeAxes = Axes.Both,
                        TextAnchor = Anchor.BottomLeft,
                        Padding = new MarginPadding(5) { Bottom = 10 },
                        ParagraphSpacing = 0.2f,
                    }.With(d =>
                    {
                        d.AddText(new RomanisableString(beatmap.Metadata.TitleUnicode, beatmap.Metadata.Title), static s => s.Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold));

                        d.NewLine();
                        d.AddText(new RomanisableString(beatmap.Metadata.ArtistUnicode, beatmap.Metadata.Artist), static s => s.Font = OsuFont.GetFont(size: 9, weight: FontWeight.SemiBold));

                        d.NewParagraph();
                        d.AddText("mapped by ", static s => s.Font = OsuFont.GetFont(size: 9, weight: FontWeight.SemiBold));
                        d.AddText(beatmap.Metadata.Author.Username, s =>
                        {
                            s.Font = OsuFont.GetFont(size: 9, weight: FontWeight.SemiBold);
                            s.Colour = colours.OnBackground;
                        });
                    }),
                ];
            }
        }

        private partial class EzQuickRotationLocalCardCoverAdapter(BeatmapInfo beatmap) : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load(CardColours colours)
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = new EzQuickRotationLocalCardCover(beatmap,
                    ColourInfo.GradientVertical(colours.Background.Opacity(0.2f), colours.Background.Opacity(0.65f)));
            }
        }
    }
}
