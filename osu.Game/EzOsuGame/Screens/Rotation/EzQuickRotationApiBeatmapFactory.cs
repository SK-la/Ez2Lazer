// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationApiBeatmapFactory
    {
        public static APIBeatmap Create(BeatmapInfo beatmap, RulesetInfo ruleset)
        {
            var beatmapSet = CreateBeatmapSet(beatmap);

            var apiBeatmap = new APIBeatmap
            {
                OnlineID = beatmap.OnlineID,
                OnlineBeatmapSetID = beatmap.BeatmapSet?.OnlineID ?? beatmapSet.OnlineID,
                Status = beatmap.Status,
                Checksum = beatmap.MD5Hash,
                AuthorID = beatmap.Metadata.Author.OnlineID,
                RulesetID = ruleset.OnlineID,
                StarRating = resolveStarRating(beatmap, ruleset),
                DifficultyName = beatmap.DifficultyName,
                CircleSize = beatmap.Difficulty.CircleSize,
                DrainRate = beatmap.Difficulty.DrainRate,
                OverallDifficulty = beatmap.Difficulty.OverallDifficulty,
                ApproachRate = beatmap.Difficulty.ApproachRate,
                Length = beatmap.Length,
                HitLength = beatmap.Length,
                BPM = beatmap.BPM,
                BeatmapSet = beatmapSet,
            };

            return apiBeatmap;
        }

        private static double resolveStarRating(BeatmapInfo beatmap, RulesetInfo ruleset)
        {
            if (EzQuickRotationDifficultyHelper.UsesXxyStarRating(ruleset))
                return beatmap.GetPersistedXxyStarRating() ?? beatmap.StarRating;

            return beatmap.StarRating;
        }

        private static APIBeatmapSet CreateBeatmapSet(BeatmapInfo beatmap)
        {
            var set = beatmap.BeatmapSet ?? throw new InvalidOperationException("Beatmap set must be populated.");

            return new APIBeatmapSet
            {
                OnlineID = set.OnlineID,
                Status = set.Status,
                Title = beatmap.Metadata.Title,
                TitleUnicode = beatmap.Metadata.TitleUnicode,
                Artist = beatmap.Metadata.Artist,
                ArtistUnicode = beatmap.Metadata.ArtistUnicode,
                Author = new APIUser
                {
                    Username = beatmap.Metadata.Author.Username,
                    Id = beatmap.Metadata.Author.OnlineID,
                },
                Source = beatmap.Metadata.Source,
                Tags = beatmap.Metadata.Tags,
                BPM = beatmap.BPM,
            };
        }

        public static APIBeatmapSet? TryCreateBeatmapSet(BeatmapInfo beatmap) => beatmap.BeatmapSet == null ? null : CreateBeatmapSet(beatmap);
    }
}
