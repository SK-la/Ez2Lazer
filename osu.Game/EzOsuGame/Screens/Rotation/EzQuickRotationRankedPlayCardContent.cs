// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;

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
