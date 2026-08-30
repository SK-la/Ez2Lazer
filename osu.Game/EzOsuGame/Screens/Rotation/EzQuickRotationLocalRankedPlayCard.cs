// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
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

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        public EzQuickRotationLocalRankedPlayCard(BeatmapInfo beatmap, APIBeatmap apiBeatmap)
            : base(new RankedPlayCardWithPlaylistItem(new RankedPlayCardItem { ID = beatmap.ID }))
        {
            SourceBeatmap = beatmap;
            this.apiBeatmap = apiBeatmap;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SongPreviewEnabled.Value = false;
            EzQuickRotationRankedPlayCardHelper.PresentLocalBeatmap(this, apiBeatmap, SourceBeatmap, beatmaps);
        }
    }
}
