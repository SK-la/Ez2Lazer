// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    public sealed class BmsSqlSongQuery
    {
        private readonly string databasePath;
        private readonly BMSBeatmapManager beatmapManager;

        public BmsSqlSongQuery(string databasePath, BMSBeatmapManager beatmapManager)
        {
            this.databasePath = databasePath;
            this.beatmapManager = beatmapManager;
        }

        public IReadOnlyList<BMSChartCache> Execute(string whereClause)
        {
            var keys = new List<string>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = BmsFilterSchema.SELECT_DISTINCT_SHA256 + whereClause;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    keys.Add(reader.GetString(0));
            }
            catch (SqliteException ex)
            {
                Logger.Log($"[BMS] Raja SQL filter failed: {ex.Message} | WHERE {whereClause}", LoggingTarget.Runtime, LogLevel.Important);
                return Array.Empty<BMSChartCache>();
            }

            return resolveCharts(keys);
        }

        public IReadOnlyList<BMSChartCache> SearchByText(string text)
        {
            var keys = new List<string>();
            string pattern = $"%{text}%";

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT sha256 FROM song
WHERE rtrim(title||' '||subtitle||' '||artist||' '||subartist||' '||genre) LIKE $pattern
GROUP BY sha256;";
            cmd.Parameters.AddWithValue("$pattern", pattern);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                keys.Add(reader.GetString(0));

            return resolveCharts(keys);
        }

        public IReadOnlyList<BMSChartCache> GetByFolderCrc(string folderCrc)
        {
            var keys = new List<string>();

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT sha256 FROM song WHERE parent = $parent GROUP BY sha256;";
            cmd.Parameters.AddWithValue("$parent", folderCrc);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                keys.Add(reader.GetString(0));

            return resolveCharts(keys);
        }

        private List<BMSChartCache> resolveCharts(IReadOnlyList<string> pathKeys)
        {
            var result = new List<BMSChartCache>();
            var cache = beatmapManager.LibraryCache;
            if (cache == null)
                return result;

            var byKey = cache.Songs
                             .SelectMany(s => s.Charts)
                             .ToDictionary(c => string.IsNullOrEmpty(c.Md5Hash) ? BmsPathKeys.ComputeChartPathKey(c.FullPath) : c.Md5Hash, c => c, StringComparer.OrdinalIgnoreCase);

            foreach (string key in pathKeys)
            {
                if (byKey.TryGetValue(key, out var chart))
                    result.Add(chart);
            }

            return result;
        }
    }
}
