// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card
{
    public partial class RankedPlayCardContent
    {
        private BeatmapInfo? ezLocalCoverBeatmap;

        public RankedPlayCardContent(APIBeatmap beatmap, BeatmapInfo localCoverBeatmap)
        {
            Beatmap = beatmap;
            ezLocalCoverBeatmap = localCoverBeatmap;
            Size = RankedPlayCard.SIZE;
        }

        private Drawable createCardCover()
        {
            Drawable cover = ezLocalCoverBeatmap != null
                ? new EzQuickRotationLocalCardCoverAdapter(ezLocalCoverBeatmap)
                : new CardCover(Beatmap);

            cover.RelativeSizeAxes = Axes.Both;
            return cover;
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
