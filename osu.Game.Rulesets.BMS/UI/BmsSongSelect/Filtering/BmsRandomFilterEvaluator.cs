// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using Microsoft.Data.Sqlite;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    public static class BmsRandomFilterEvaluator
    {
        public static bool Matches(BmsSongBar song, IReadOnlyDictionary<string, JsonElement>? filter, BmsBarContext context)
        {
            if (filter == null || filter.Count == 0)
                return true;

            var scores = context.SqlQuery.Execute($"song.sha256 = '{song.PathKey}'");

            if (scores.Count == 0)
            {
                return filter.Values.Any(v => v.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(v.GetString()))
                       || filter.Values.Any(v => v.ValueKind == JsonValueKind.Number && v.GetInt32() != 0);
            }

            // Re-query score row via filter DB
            using var connection = new SqliteConnection($"Data Source={getDbPath(context)}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT clear, playcount FROM score WHERE sha256 = $sha LIMIT 1;";
            cmd.Parameters.AddWithValue("$sha", song.PathKey);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return false;

            int clear = reader.GetInt32(0);
            int playcount = reader.GetInt32(1);

            foreach (var (key, value) in filter)
            {
                object? actual = key.ToLowerInvariant() switch
                {
                    "clear" => clear,
                    "playcount" => playcount,
                    _ => null,
                };

                if (actual == null)
                    continue;

                if (value.ValueKind == JsonValueKind.Number && actual is int intActual && value.GetInt32() != intActual)
                    return false;
            }

            return true;
        }

        private static string getDbPath(BmsBarContext context) => context.FilterDatabasePath;
    }
}
