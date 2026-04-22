// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;
using osu.Game.Database;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    internal readonly record struct BmsSongRow(
        string Md5,
        string Sha256,
        string Title,
        string Subtitle,
        string Genre,
        string Artist,
        string Subartist,
        string Path,
        string Folder,
        int Level,
        int Difficulty,
        int Mode,
        int Notes,
        int Favorite,
        int MaxBpm,
        int MinBpm,
        int Length);

    internal readonly record struct BmsScoreRow(
        string Sha256,
        int Mode,
        int Clear,
        int Playcount,
        int Clearcount,
        int Epg,
        int Lpg,
        int Egr,
        int Lgr,
        int Notes,
        int Combo,
        int Minbp);

    internal static class BmsScoreSchemaBuilder
    {
        public static (
            IReadOnlyList<BmsSongRow> songs,
            IReadOnlyList<BmsScoreRow> scores,
            IReadOnlyList<BmsInformationRow> informations)
            Build(
                IEnumerable<BMSChartCache> charts,
                IReadOnlyDictionary<Guid, BmsLampRecord> lampsByBeatmapId,
                RealmAccess realm,
                BmsAnalyticsSqliteRepository? analytics)
        {
            var songs = new List<BmsSongRow>();
            var scores = new List<BmsScoreRow>();
            var informations = new List<BmsInformationRow>();

            foreach (var chart in charts)
            {
                string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                    ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                    : chart.Md5Hash;

                string sha256 = pathKey;
                string md5 = pathKey.Length >= 32 ? pathKey[..32] : pathKey;

                songs.Add(new BmsSongRow(
                    md5,
                    sha256,
                    chart.Title,
                    chart.SubTitle,
                    chart.Genre,
                    chart.Artist,
                    chart.SubArtist,
                    chart.FileName,
                    chart.FolderPath,
                    chart.PlayLevel,
                    chart.Rank,
                    chart.KeyCount,
                    Math.Max(1, chart.TotalNotes),
                    0,
                    (int)Math.Round(chart.MaxBpm > 0 ? chart.MaxBpm : chart.Bpm),
                    (int)Math.Round(chart.MinBpm),
                    (int)Math.Round(chart.Duration)));

                scores.Add(buildScoreRow(chart, pathKey, lampsByBeatmapId, realm));
                informations.Add(BmsInformationBuilder.Build(chart, pathKey, realm, analytics));
            }

            return (songs, scores, informations);
        }

        private static BmsScoreRow buildScoreRow(
            BMSChartCache chart,
            string pathKey,
            IReadOnlyDictionary<Guid, BmsLampRecord> lampsByBeatmapId,
            RealmAccess realm)
        {
            int clear = (int)BmsClearLamp.NoPlay;
            int playcount = 0;
            int clearcount = 0;
            int epg = 0, lpg = 0, egr = 0, lgr = 0;
            int notes = Math.Max(1, chart.TotalNotes);
            int combo = 0;
            int minbp = 0;

            var best = BmsLocalScoreQueries.GetBestLocalScore(realm, pathKey);

            if (best != null)
            {
                playcount = 1;
                epg = best.Statistics.GetValueOrDefault(HitResult.Perfect);
                egr = best.Statistics.GetValueOrDefault(HitResult.Great);
                lgr = best.Statistics.GetValueOrDefault(HitResult.Good);
                lpg = epg;
                minbp = best.Statistics.GetValueOrDefault(HitResult.Miss);
                combo = notes - minbp;
                clear = best.Accuracy >= 0.95 ? (int)BmsClearLamp.Normal : (int)BmsClearLamp.Failed;
                if (clear >= (int)BmsClearLamp.Normal)
                    clearcount = 1;
            }

            Guid beatmapId = createDeterministicGuid($"bms:chart:{chart.FullPath}");

            if (lampsByBeatmapId.TryGetValue(beatmapId, out var lamp))
            {
                clear = (int)lamp.Lamp;
                playcount = Math.Max(playcount, 1);
                lpg = lamp.PerfectGreatCount;
                epg = lamp.PerfectGreatCount;
                egr = lamp.GreatCount;
                lgr = lamp.GoodCount;
                minbp = lamp.MissCount;
                combo = Math.Max(0, lamp.TotalNotes - lamp.MissCount);
                if (clear >= (int)BmsClearLamp.Normal)
                    clearcount = 1;
            }

            return new BmsScoreRow(pathKey, chart.KeyCount, clear, playcount, clearcount, epg, lpg, egr, lgr, notes, combo, minbp);
        }

        private static Guid createDeterministicGuid(string seed)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }
    }
}
