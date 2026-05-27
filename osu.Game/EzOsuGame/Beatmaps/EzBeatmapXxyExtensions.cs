// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Beatmaps
{
    public static class EzBeatmapXxyExtensions
    {
        public static bool SupportsXxyStarRating(this BeatmapInfo beatmap)
            => EzXxyStarRatingSupport.SupportsBeatmapInfo(beatmap);

        public static bool HasPersistedXxyStarRating(this BeatmapInfo beatmap) => beatmap.XxyStarRating >= 0;

        public static double? GetPersistedXxyStarRating(this BeatmapInfo beatmap)
            => beatmap.HasPersistedXxyStarRating() ? beatmap.XxyStarRating : null;

        public static EzManiaSummary ToEzManiaSummaryForDisplay(this BeatmapInfo beatmap, EzManiaSummary? analysisSummary = null)
        {
            double? xxy = beatmap.GetPersistedXxyStarRating() ?? analysisSummary?.XxySr;

            return new EzManiaSummary(
                analysisSummary?.ColumnCounts,
                analysisSummary?.HoldNoteCounts,
                xxy);
        }
    }
}
