// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Data.Sqlite;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    public sealed class BmsAnalyticsRecord
    {
        public string PathKey { get; init; } = string.Empty;
        public double? Pp { get; init; }
        public double? XxySr { get; init; }
        public double? AvgKps { get; init; }
        public double? MaxKps { get; init; }
        public double? StarRating { get; init; }
        public string? ColumnCountsJson { get; init; }
        public string? KpsListJson { get; init; }
    }

    public sealed class BmsAnalyticsSqliteRepository
    {
        private const int schema_version = 2;
        private readonly string databasePath;
        private readonly object writeLock = new object();

        public BmsAnalyticsSqliteRepository(string databasePath)
        {
            this.databasePath = databasePath;
            ensureInitialized();
        }

        public void Upsert(BmsAnalyticsRecord record)
        {
            ensureInitialized();

            lock (writeLock)
            {
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
INSERT INTO chart_analytics (
    path_key, pp, xxy_sr, avg_kps, max_kps, star_rating, column_counts_json, kps_list_json, file_size, last_modified_ticks, scanned_at
) VALUES (
    $path_key, $pp, $xxy_sr, $avg_kps, $max_kps, $star_rating, $column_counts_json, $kps_list_json, $file_size, $last_modified_ticks, $scanned_at
)
ON CONFLICT(path_key) DO UPDATE SET
    pp = excluded.pp,
    xxy_sr = excluded.xxy_sr,
    avg_kps = excluded.avg_kps,
    max_kps = excluded.max_kps,
    star_rating = excluded.star_rating,
    column_counts_json = excluded.column_counts_json,
    kps_list_json = excluded.kps_list_json,
    file_size = excluded.file_size,
    last_modified_ticks = excluded.last_modified_ticks,
    scanned_at = excluded.scanned_at;";

                cmd.Parameters.AddWithValue("$path_key", record.PathKey);
                cmd.Parameters.AddWithValue("$pp", (object?)record.Pp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$xxy_sr", (object?)record.XxySr ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$avg_kps", (object?)record.AvgKps ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$max_kps", (object?)record.MaxKps ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$star_rating", (object?)record.StarRating ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$column_counts_json", (object?)record.ColumnCountsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$kps_list_json", (object?)record.KpsListJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$file_size", 0L);
                cmd.Parameters.AddWithValue("$last_modified_ticks", 0L);
                cmd.Parameters.AddWithValue("$scanned_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                cmd.ExecuteNonQuery();
            }
        }

        public bool TryGet(string pathKey, out BmsAnalyticsRecord record)
        {
            record = null!;
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT path_key, pp, xxy_sr, avg_kps, max_kps, star_rating, column_counts_json, kps_list_json FROM chart_analytics WHERE path_key = $path_key LIMIT 1;";
            cmd.Parameters.AddWithValue("$path_key", pathKey);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return false;

            record = readRecord(reader);
            return true;
        }

        public IReadOnlyDictionary<string, BmsAnalyticsRecord> LoadAll()
        {
            ensureInitialized();
            var result = new Dictionary<string, BmsAnalyticsRecord>(StringComparer.OrdinalIgnoreCase);

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT path_key, pp, xxy_sr, avg_kps, max_kps, star_rating, column_counts_json, kps_list_json FROM chart_analytics;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var record = readRecord(reader);
                result[record.PathKey] = record;
            }

            return result;
        }

        private static BmsAnalyticsRecord readRecord(SqliteDataReader reader)
        {
            return new BmsAnalyticsRecord
            {
                PathKey = reader.GetString(0),
                Pp = reader.IsDBNull(1) ? null : reader.GetDouble(1),
                XxySr = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                AvgKps = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                MaxKps = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                StarRating = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                ColumnCountsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                KpsListJson = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null,
            };
        }

        private void ensureInitialized()
        {
            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS chart_analytics (
    path_key TEXT PRIMARY KEY,
    pp REAL,
    xxy_sr REAL,
    avg_kps REAL,
    max_kps REAL,
    star_rating REAL,
    column_counts_json TEXT,
    file_size INTEGER NOT NULL DEFAULT 0,
    last_modified_ticks INTEGER NOT NULL DEFAULT 0,
    scanned_at INTEGER NOT NULL DEFAULT 0
);";
            cmd.ExecuteNonQuery();

            migrateSchema(connection);

            using var versionCmd = connection.CreateCommand();
            versionCmd.CommandText = "INSERT OR REPLACE INTO meta (key, value) VALUES ('schema_version', $v);";
            versionCmd.Parameters.AddWithValue("$v", schema_version.ToString());
            versionCmd.ExecuteNonQuery();
        }

        private static void migrateSchema(SqliteConnection connection)
        {
            if (!columnExists(connection, "chart_analytics", "kps_list_json"))
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE chart_analytics ADD COLUMN kps_list_json TEXT;";
                alter.ExecuteNonQuery();
            }
        }

        private static bool columnExists(SqliteConnection connection, string table, string column)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            return connection;
        }
    }
}
