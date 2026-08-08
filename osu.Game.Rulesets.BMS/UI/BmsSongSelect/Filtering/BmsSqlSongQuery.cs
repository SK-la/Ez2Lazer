// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
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

        public IReadOnlyList<BmsChartSummary> Execute(string whereClause)
            => ExecutePage(whereClause, null, 150, null);

        public IReadOnlyList<BmsChartSummary> ExecutePage(string whereClause, string? afterPathKey, int limit, IReadOnlyCollection<int>? keyCounts)
        {
            var keys = new List<string>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();

                using var cmd = connection.CreateCommand();
                var keyCondition = new List<string>();

                if (keyCounts is { Count: > 0 })
                {
                    int index = 0;

                    foreach (int keyCount in keyCounts)
                    {
                        string parameterName = $"$key{index++}";
                        keyCondition.Add(parameterName);
                        cmd.Parameters.AddWithValue(parameterName, keyCount);
                    }
                }

                string keySql = keyCondition.Count == 0 ? string.Empty : $" AND song.mode IN ({string.Join(", ", keyCondition)})";
                cmd.CommandText = $@"
WITH custom AS (
    {BmsFilterSchema.SELECT_DISTINCT_SHA256}{whereClause}
)
SELECT custom.sha256
FROM custom
INNER JOIN song ON custom.sha256 = song.sha256
WHERE ($after IS NULL OR custom.sha256 > $after)
{keySql}
ORDER BY custom.sha256
LIMIT $limit;";
                cmd.Parameters.AddWithValue("$after", (object?)afterPathKey ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    keys.Add(reader.GetString(0));
            }
            catch (SqliteException ex)
            {
                Logger.Log($"[BMS] Raja SQL filter failed: {ex.Message} | WHERE {whereClause}", LoggingTarget.Runtime, LogLevel.Important);
                return Array.Empty<BmsChartSummary>();
            }

            return resolveSummaries(keys);
        }

        public BmsChartSummaryPage SearchByText(string text, BmsChartPageCursor? after, int limit, BmsChartSort sort, IReadOnlyCollection<int>? keyCounts)
        {
            return beatmapManager.GetChartSummaryPage(new BmsChartQuery(SearchText: text, KeyCounts: keyCounts, Sort: sort), after, limit);
        }

        public BmsChartSummary? GetRandom(
            string whereClause,
            IReadOnlyCollection<int>? keyCounts,
            IReadOnlyDictionary<string, JsonElement>? filter,
            IReadOnlyDictionary<string, object?>? queryParameters = null,
            ulong? randomValue = null)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();
                string randomKey = BmsPathKeys.CreateRandomStartKey(randomValue);
                string? pathKey = tryGetPathKeyAtOrAfter(connection, whereClause, keyCounts, filter, queryParameters, randomKey)
                                  ?? tryGetPathKeyAtOrAfter(connection, whereClause, keyCounts, filter, queryParameters, null);
                return pathKey != null && beatmapManager.TryGetChartSummaryByPathKey(pathKey, out BmsChartSummary summary)
                    ? summary
                    : null;
            }
            catch (SqliteException ex)
            {
                Logger.Log($"[BMS] Raja random SQL filter failed: {ex.Message} | WHERE {whereClause}", LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }
        }

        private static string? tryGetPathKeyAtOrAfter(
            SqliteConnection connection,
            string whereClause,
            IReadOnlyCollection<int>? keyCounts,
            IReadOnlyDictionary<string, JsonElement>? filter,
            IReadOnlyDictionary<string, object?>? queryParameters,
            string? startKey)
        {
            using var cmd = connection.CreateCommand();
            var keyParameters = new List<string>();

            if (queryParameters != null)
            {
                foreach ((string name, object? value) in queryParameters)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            if (keyCounts is { Count: > 0 })
            {
                int index = 0;

                foreach (int keyCount in keyCounts)
                {
                    string parameterName = $"$key{index++}";
                    keyParameters.Add(parameterName);
                    cmd.Parameters.AddWithValue(parameterName, keyCount);
                }
            }

            string keySql = keyParameters.Count == 0 ? string.Empty : $" AND song.mode IN ({string.Join(", ", keyParameters)})";
            var randomConditions = new List<string>();

            if (filter != null)
            {
                foreach ((string key, JsonElement value) in filter)
                {
                    if (value.ValueKind != JsonValueKind.Number || key is not ("clear" or "playcount"))
                        continue;

                    string parameterName = $"$random{randomConditions.Count}";
                    randomConditions.Add($"COALESCE(score.{key}, 0) = {parameterName}");
                    cmd.Parameters.AddWithValue(parameterName, value.GetInt32());
                }
            }

            string randomSql = randomConditions.Count == 0 ? string.Empty : $" AND {string.Join(" AND ", randomConditions)}";
            string startSql = startKey == null ? string.Empty : " AND custom.sha256 >= $startKey";

            if (startKey != null)
                cmd.Parameters.AddWithValue("$startKey", startKey);

            cmd.CommandText = $@"
WITH custom AS (
    {BmsFilterSchema.SELECT_DISTINCT_SHA256}{whereClause}
)
SELECT DISTINCT custom.sha256
FROM custom
INNER JOIN song ON custom.sha256 = song.sha256
LEFT OUTER JOIN score ON custom.sha256 = score.sha256
WHERE 1 = 1
{keySql}
{randomSql}
{startSql}
ORDER BY custom.sha256
LIMIT 1;";
            return cmd.ExecuteScalar() as string;
        }

        private List<BmsChartSummary> resolveSummaries(IReadOnlyList<string> pathKeys)
        {
            var result = new List<BmsChartSummary>(pathKeys.Count);

            foreach (string key in pathKeys)
            {
                if (beatmapManager.TryGetChartSummaryByPathKey(key, out BmsChartSummary summary))
                    result.Add(summary);
            }

            return result;
        }
    }
}
