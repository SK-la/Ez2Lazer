// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    /// <summary>
    /// Persists offline BMS analytics onto <see cref="BeatmapInfo"/> in Realm for standard song-select panels.
    /// </summary>
    public static class BmsAnalyticsRealmWriteback
    {
        public static void TryApply(RealmAccess realm, BMSChartCache chart, BmsChartAnalyticsResult result)
            => TryApplyBatch(realm, new[] { (chart, string.Empty, result) });

        public static void TryApplyBatch(RealmAccess realm, IReadOnlyList<(BMSChartCache Chart, string PathKey, BmsChartAnalyticsResult Result)> batch)
        {
            if (batch.Count == 0)
                return;

            try
            {
                realm.Write(r =>
                {
                    foreach ((BMSChartCache chart, _, BmsChartAnalyticsResult result) in batch)
                    {
                        Guid beatmapId = GetDeterministicBeatmapId(chart.FullPath);
                        var beatmap = r.Find<BeatmapInfo>(beatmapId);

                        if (beatmap == null)
                            continue;

                        if (result.StarRating is double star && star >= 0)
                            beatmap.StarRating = star;

                        if (result.XxySr is double xxy && xxy >= 0)
                            beatmap.XxyStarRating = xxy;

                        if (result.Pp is double pp && pp >= 0)
                            beatmap.PerformancePoints = pp;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Realm analytics writeback batch failed: {ex.Message}", LoggingTarget.Database, LogLevel.Debug);
            }
        }

        internal static Guid GetDeterministicBeatmapId(string chartPath)
        {
            return BmsChartIdentity.CreateBeatmapId(chartPath);
        }
    }
}
