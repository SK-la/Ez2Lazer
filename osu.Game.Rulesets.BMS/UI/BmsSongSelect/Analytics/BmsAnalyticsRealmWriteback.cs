// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;
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
        {
            Guid beatmapId = GetDeterministicBeatmapId(chart.FullPath);

            try
            {
                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(beatmapId);

                    if (beatmap == null)
                        return;

                    if (result.StarRating is double star && star >= 0)
                        beatmap.StarRating = star;

                    if (result.XxySr is double xxy && xxy >= 0)
                        beatmap.XxyStarRating = xxy;

                    if (result.Pp is double pp && pp >= 0)
                        beatmap.PerformancePoints = pp;
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Realm analytics writeback failed for {chart.FullPath}: {ex.Message}", LoggingTarget.Database, LogLevel.Debug);
            }
        }

        internal static Guid GetDeterministicBeatmapId(string chartPath)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes($"bms:chart:{chartPath}"));
            return new Guid(hash);
        }
    }
}
