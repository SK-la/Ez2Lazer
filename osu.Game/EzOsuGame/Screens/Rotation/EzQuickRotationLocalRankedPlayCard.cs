// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public sealed partial class EzQuickRotationLocalRankedPlayCard : RankedPlayCard
    {
        public BeatmapInfo SourceBeatmap { get; }

        private readonly APIBeatmap apiBeatmap;

        public EzQuickRotationLocalRankedPlayCard(BeatmapInfo beatmap, APIBeatmap apiBeatmap)
            : base(new RankedPlayCardWithPlaylistItem(new RankedPlayCardItem { ID = beatmap.ID }))
        {
            SourceBeatmap = beatmap;
            this.apiBeatmap = apiBeatmap;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            EzQuickRotationRankedPlayCardHelper.PresentLocalBeatmap(this, apiBeatmap, SourceBeatmap);
        }
    }
}
