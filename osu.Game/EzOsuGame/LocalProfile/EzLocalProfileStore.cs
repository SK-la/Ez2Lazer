// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Single shared local profile statistics store (not keyed by login username).
    /// </summary>
    public class EzLocalProfileStore : IDisposable
    {
        public const string DATABASE_FILENAME = "ez-local-profile.sqlite";
        public const int SCHEMA_VERSION = 1;

        private readonly Storage storage;
        private readonly object sync = new object();
        private bool initialised;
        private string dbPath = string.Empty;
        private bool isDisposed;

        public EzLocalProfileStore(Storage storage)
        {
            this.storage = storage;
        }

        public EzLocalProfileSnapshot LoadSnapshot()
        {
            lock (sync)
            {
                try
                {
                    ensureInitialised();

                    using var connection = openConnection();

                    if (!hasComputedData(connection))
                    {
                        return new EzLocalProfileSnapshot
                        {
                            HasData = false,
                            IncludedUsernames = readIncludedUsernames(connection),
                        };
                    }

                    return new EzLocalProfileSnapshot
                    {
                        HasData = true,
                        LastComputedAt = tryReadLastComputedAt(connection),
                        IncludedUsernames = readIncludedUsernames(connection),
                        RulesetStats = readRulesetStats(connection),
                        ManiaKeyStats = readManiaKeyStats(connection),
                        ManiaColumnStats = readManiaColumnStats(connection),
                        GradeCounts = readGradeCounts(connection),
                        StarPlayCounts = readStarPlayCounts(connection),
                        StdAttrAffinities = readStdAttrAffinities(connection),
                    };
                }
                catch (Exception ex)
                {
                    Logger.Log($"[EzLocalProfile] Failed to load snapshot (recompute required): {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                    return new EzLocalProfileSnapshot { HasData = false };
                }
            }
        }

        public IReadOnlyList<string> LoadIncludedUsernames()
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                return readIncludedUsernames(connection);
            }
        }

        public int GetMostPlayedOffset(int rulesetId)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                string? raw = tryGetMeta(connection, mostPlayedOffsetKey(rulesetId));
                if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset))
                    return 0;

                return Math.Max(0, offset);
            }
        }

        public void SetMostPlayedOffset(int rulesetId, int offset)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                setMeta(connection, mostPlayedOffsetKey(rulesetId), Math.Max(0, offset).ToString(CultureInfo.InvariantCulture));
            }
        }

        public void ReplaceAll(EzLocalProfileAggregationResult result)
        {
            lock (sync)
            {
                ensureInitialised();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                clearTables(connection);
                // Recreate column table so the current single-version schema is always applied on recompute.
                recreateManiaColumnTable(connection);

                setMeta(connection, "schema_version", SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, "last_computed_at", result.ComputedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                setMeta(connection, "included_usernames_json", JsonSerializer.Serialize(result.IncludedUsernames.ToList()));

                foreach (var (rulesetId, stats) in result.RulesetStats)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO ruleset_stats (ruleset_id, total_keys, avg_kps, max_kps, score_count, kps_sample_count)
                                      VALUES ($ruleset_id, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count);
                                      """;
                    cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
                    cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                    cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                    cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                    cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                    cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                    cmd.ExecuteNonQuery();
                }

                foreach (var (keyCount, stats) in result.ManiaKeyStats)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO mania_key_stats (key_count, total_keys, avg_kps, max_kps, score_count, kps_sample_count)
                                      VALUES ($key_count, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count);
                                      """;
                    cmd.Parameters.AddWithValue("$key_count", keyCount);
                    cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                    cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                    cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                    cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                    cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                    cmd.ExecuteNonQuery();
                }

                foreach (var ((keyCount, column), stats) in result.ManiaColumnStats)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO mania_column_stats (key_count, column_index, total_keys, avg_kps, max_kps, score_count, kps_sample_count)
                                      VALUES ($key_count, $column_index, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count);
                                      """;
                    cmd.Parameters.AddWithValue("$key_count", keyCount);
                    cmd.Parameters.AddWithValue("$column_index", column);
                    cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                    cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                    cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                    cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                    cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                    cmd.ExecuteNonQuery();
                }

                foreach (var ((rulesetId, rank), count) in result.GradeCounts)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO grade_counts (ruleset_id, rank, count)
                                      VALUES ($ruleset_id, $rank, $count);
                                      """;
                    cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
                    cmd.Parameters.AddWithValue("$rank", (int)rank);
                    cmd.Parameters.AddWithValue("$count", count);
                    cmd.ExecuteNonQuery();
                }

                foreach (var ((rulesetId, starBucket), count) in result.StarPlayCounts)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO star_play_counts (ruleset_id, star_bucket, count)
                                      VALUES ($ruleset_id, $star_bucket, $count);
                                      """;
                    cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
                    cmd.Parameters.AddWithValue("$star_bucket", starBucket);
                    cmd.Parameters.AddWithValue("$count", count);
                    cmd.ExecuteNonQuery();
                }

                foreach (var ((attr, value), stats) in result.StdAttrAffinities)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO std_attr_affinity (attr, value, play_count, high_grade_count)
                                      VALUES ($attr, $value, $play_count, $high_grade_count);
                                      """;
                    cmd.Parameters.AddWithValue("$attr", (int)attr);
                    cmd.Parameters.AddWithValue("$value", value);
                    cmd.Parameters.AddWithValue("$play_count", stats.PlayCount);
                    cmd.Parameters.AddWithValue("$high_grade_count", stats.HighGradeCount);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            SqliteConnection.ClearAllPools();
        }

        private void ensureInitialised()
        {
            if (initialised)
                return;

            dbPath = storage.GetFullPath(DATABASE_FILENAME, true);

            using var connection = openConnection();
            ensureSchema(connection);
            initialised = true;
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        private static void ensureSchema(SqliteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS meta (
                                      key TEXT PRIMARY KEY NOT NULL,
                                      value TEXT NOT NULL
                                  );
                                  CREATE TABLE IF NOT EXISTS ruleset_stats (
                                      ruleset_id INTEGER PRIMARY KEY NOT NULL,
                                      total_keys INTEGER NOT NULL,
                                      avg_kps REAL NOT NULL,
                                      max_kps REAL NOT NULL,
                                      score_count INTEGER NOT NULL,
                                      kps_sample_count INTEGER NOT NULL
                                  );
                                  CREATE TABLE IF NOT EXISTS mania_key_stats (
                                      key_count INTEGER PRIMARY KEY NOT NULL,
                                      total_keys INTEGER NOT NULL,
                                      avg_kps REAL NOT NULL,
                                      max_kps REAL NOT NULL,
                                      score_count INTEGER NOT NULL,
                                      kps_sample_count INTEGER NOT NULL
                                  );
                                  CREATE TABLE IF NOT EXISTS mania_column_stats (
                                      key_count INTEGER NOT NULL,
                                      column_index INTEGER NOT NULL,
                                      total_keys INTEGER NOT NULL,
                                      avg_kps REAL NOT NULL,
                                      max_kps REAL NOT NULL,
                                      score_count INTEGER NOT NULL,
                                      kps_sample_count INTEGER NOT NULL,
                                      PRIMARY KEY (key_count, column_index)
                                  );
                                  CREATE TABLE IF NOT EXISTS grade_counts (
                                      ruleset_id INTEGER NOT NULL,
                                      rank INTEGER NOT NULL,
                                      count INTEGER NOT NULL,
                                      PRIMARY KEY (ruleset_id, rank)
                                  );
                                  CREATE TABLE IF NOT EXISTS star_play_counts (
                                      ruleset_id INTEGER NOT NULL,
                                      star_bucket INTEGER NOT NULL,
                                      count INTEGER NOT NULL,
                                      PRIMARY KEY (ruleset_id, star_bucket)
                                  );
                                  CREATE TABLE IF NOT EXISTS std_attr_affinity (
                                      attr INTEGER NOT NULL,
                                      value REAL NOT NULL,
                                      play_count INTEGER NOT NULL,
                                      high_grade_count INTEGER NOT NULL,
                                      PRIMARY KEY (attr, value)
                                  );
                                  """;
                cmd.ExecuteNonQuery();
            }

            setMeta(connection, "schema_version", SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
        }

        private static void recreateManiaColumnTable(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              DROP TABLE IF EXISTS mania_column_stats;
                              CREATE TABLE mania_column_stats (
                                  key_count INTEGER NOT NULL,
                                  column_index INTEGER NOT NULL,
                                  total_keys INTEGER NOT NULL,
                                  avg_kps REAL NOT NULL,
                                  max_kps REAL NOT NULL,
                                  score_count INTEGER NOT NULL,
                                  kps_sample_count INTEGER NOT NULL,
                                  PRIMARY KEY (key_count, column_index)
                              );
                              """;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Full wipe before <see cref="ReplaceAll"/> rewrite. <c>WHERE TRUE</c> keeps the intentional clear explicit for SQL analyzers.
        /// </summary>
        private static void clearTables(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              DELETE FROM ruleset_stats WHERE TRUE;
                              DELETE FROM mania_key_stats WHERE TRUE;
                              DELETE FROM mania_column_stats WHERE TRUE;
                              DELETE FROM grade_counts WHERE TRUE;
                              DELETE FROM star_play_counts WHERE TRUE;
                              DELETE FROM std_attr_affinity WHERE TRUE;
                              """;
            cmd.ExecuteNonQuery();
        }

        private static bool hasComputedData(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM ruleset_stats;";
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }

        private static DateTimeOffset? tryReadLastComputedAt(SqliteConnection connection)
        {
            string? raw = tryGetMeta(connection, "last_computed_at");
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms))
                return null;

            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }

        private static IReadOnlyList<string> readIncludedUsernames(SqliteConnection connection)
        {
            string? json = tryGetMeta(connection, "included_usernames_json");
            if (string.IsNullOrEmpty(json))
                return Array.Empty<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalProfile] Failed to parse included usernames: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                return Array.Empty<string>();
            }
        }

        private static IReadOnlyList<EzLocalProfileRulesetStats> readRulesetStats(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileRulesetStats>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ruleset_id, total_keys, avg_kps, max_kps, score_count, kps_sample_count FROM ruleset_stats ORDER BY ruleset_id;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileRulesetStats(
                    reader.GetInt32(0),
                    reader.GetInt64(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5)));
            }

            return list;
        }

        private static IReadOnlyList<EzLocalProfileManiaKeyStats> readManiaKeyStats(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileManiaKeyStats>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT key_count, total_keys, avg_kps, max_kps, score_count, kps_sample_count FROM mania_key_stats ORDER BY key_count;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileManiaKeyStats(
                    reader.GetInt32(0),
                    reader.GetInt64(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5)));
            }

            return list;
        }

        private static IReadOnlyList<EzLocalProfileManiaColumnStats> readManiaColumnStats(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileManiaColumnStats>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT key_count, column_index, total_keys, avg_kps, max_kps, score_count, kps_sample_count FROM mania_column_stats ORDER BY key_count, column_index;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileManiaColumnStats(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt64(2),
                    reader.GetDouble(3),
                    reader.GetDouble(4),
                    reader.GetInt32(5),
                    reader.GetInt32(6)));
            }

            return list;
        }

        private static IReadOnlyList<EzLocalProfileGradeCount> readGradeCounts(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileGradeCount>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ruleset_id, rank, count FROM grade_counts ORDER BY ruleset_id, rank DESC;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileGradeCount(
                    reader.GetInt32(0),
                    (ScoreRank)reader.GetInt32(1),
                    reader.GetInt32(2)));
            }

            return list;
        }

        private static IReadOnlyList<EzLocalProfileStarPlayCount> readStarPlayCounts(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileStarPlayCount>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ruleset_id, star_bucket, count FROM star_play_counts ORDER BY ruleset_id, star_bucket;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileStarPlayCount(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2)));
            }

            return list;
        }

        private static IReadOnlyList<EzLocalProfileStdAttrAffinity> readStdAttrAffinities(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileStdAttrAffinity>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT attr, value, play_count, high_grade_count FROM std_attr_affinity ORDER BY attr, play_count DESC;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileStdAttrAffinity(
                    (EzLocalProfileStdAttr)reader.GetInt32(0),
                    reader.GetDouble(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3)));
            }

            return list;
        }

        private static string mostPlayedOffsetKey(int rulesetId) => $"online_mp_offset_{rulesetId}";

        private static void setMeta(SqliteConnection connection, string key, string value)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO meta (key, value) VALUES ($key, $value)
                              ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                              """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }

        private static string? tryGetMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }
    }
}
