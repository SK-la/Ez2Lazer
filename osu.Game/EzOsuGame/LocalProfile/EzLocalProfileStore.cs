// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
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
        private readonly Lock sync = new Lock();
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
                        XxyPlayCounts = readXxyPlayCounts(connection),
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

        public IReadOnlyList<EzLocalProfileDrillScoreRow> LoadDrillScores(int rulesetId)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                return readDrillScores(connection, rulesetId);
            }
        }

        public int GetMostPlayedOffset(int rulesetId) => GetPullOffset(EzLocalProfileOnlinePullKind.MostPlayed, rulesetId);

        public void SetMostPlayedOffset(int rulesetId, int offset) => SetPullOffset(EzLocalProfileOnlinePullKind.MostPlayed, rulesetId, offset);

        public int GetPullOffset(EzLocalProfileOnlinePullKind kind, int rulesetId)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                string? raw = tryGetMeta(connection, pullOffsetKey(kind, rulesetId));
                if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset))
                    return 0;

                return Math.Max(0, offset);
            }
        }

        public void SetPullOffset(EzLocalProfileOnlinePullKind kind, int rulesetId, int offset)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                setMeta(connection, pullOffsetKey(kind, rulesetId), Math.Max(0, offset).ToString(CultureInfo.InvariantCulture));
            }
        }

        public void UpsertOnlineScoreContribution(EzLocalProfileOnlineScoreContribution contribution)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO online_score_contributions
                                      (online_id, ruleset_id, rank, star_rating, circle_size, approach_rate, key_count, pp, duration_ms)
                                  VALUES
                                      ($online_id, $ruleset_id, $rank, $star_rating, $circle_size, $approach_rate, $key_count, $pp, $duration_ms)
                                  ON CONFLICT(online_id) DO UPDATE SET
                                      ruleset_id = excluded.ruleset_id,
                                      rank = excluded.rank,
                                      star_rating = excluded.star_rating,
                                      circle_size = excluded.circle_size,
                                      approach_rate = excluded.approach_rate,
                                      key_count = excluded.key_count,
                                      pp = excluded.pp,
                                      duration_ms = excluded.duration_ms;
                                  """;
                cmd.Parameters.AddWithValue("$online_id", contribution.OnlineId);
                cmd.Parameters.AddWithValue("$ruleset_id", contribution.RulesetId);
                cmd.Parameters.AddWithValue("$rank", (int)contribution.Rank);
                cmd.Parameters.AddWithValue("$star_rating", contribution.StarRating);
                cmd.Parameters.AddWithValue("$circle_size", contribution.CircleSize);
                cmd.Parameters.AddWithValue("$approach_rate", contribution.ApproachRate);
                cmd.Parameters.AddWithValue("$key_count", contribution.KeyCount);
                cmd.Parameters.AddWithValue("$pp", contribution.Pp);
                cmd.Parameters.AddWithValue("$duration_ms", contribution.DurationMs);
                cmd.ExecuteNonQuery();
            }
        }

        public IReadOnlyList<EzLocalProfileOnlineScoreContribution> LoadOnlineScoreContributions()
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                var list = new List<EzLocalProfileOnlineScoreContribution>();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  SELECT online_id, ruleset_id, rank, star_rating, circle_size, approach_rate, key_count, pp, duration_ms
                                  FROM online_score_contributions;
                                  """;
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new EzLocalProfileOnlineScoreContribution(
                        reader.GetInt64(0),
                        reader.GetInt32(1),
                        (ScoreRank)reader.GetInt32(2),
                        reader.GetDouble(3),
                        (float)reader.GetDouble(4),
                        (float)reader.GetDouble(5),
                        reader.GetInt64(6),
                        reader.GetDouble(7),
                        reader.GetInt64(8)));
                }

                return list;
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
                                      INSERT INTO ruleset_stats (ruleset_id, total_keys, avg_kps, max_kps, score_count, kps_sample_count, total_pp, total_duration_ms)
                                      VALUES ($ruleset_id, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count, $total_pp, $total_duration_ms);
                                      """;
                    cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
                    cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                    cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                    cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                    cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                    cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                    cmd.Parameters.AddWithValue("$total_pp", stats.TotalPp);
                    cmd.Parameters.AddWithValue("$total_duration_ms", stats.TotalDurationMs);
                    cmd.ExecuteNonQuery();
                }

                foreach (var (keyCount, stats) in result.ManiaKeyStats)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO mania_key_stats (key_count, total_keys, avg_kps, max_kps, score_count, kps_sample_count, total_pp, total_duration_ms)
                                      VALUES ($key_count, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count, $total_pp, $total_duration_ms);
                                      """;
                    cmd.Parameters.AddWithValue("$key_count", keyCount);
                    cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                    cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                    cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                    cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                    cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                    cmd.Parameters.AddWithValue("$total_pp", stats.TotalPp);
                    cmd.Parameters.AddWithValue("$total_duration_ms", stats.TotalDurationMs);
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

                foreach (var ((rulesetId, starBucket), count) in result.XxyPlayCounts)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO xxy_play_counts (ruleset_id, star_bucket, count)
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

                writeDrillScores(connection, result.DrillScores);

                transaction.Commit();
            }
        }

        /// <summary>
        /// Overwrite partitions for recomputed usernames, optionally drop other partitions, then rebuild display totals.
        /// </summary>
        public void ApplyUsernamePartitions(
            IReadOnlyDictionary<string, EzLocalProfileAggregationResult> recomputedByUsername,
            bool replaceOtherUsernames,
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> onlineContributions,
            HashSet<long> localOnlineScoreIds)
        {
            lock (sync)
            {
                ensureInitialised();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                if (replaceOtherUsernames)
                {
                    var keep = new HashSet<string>(recomputedByUsername.Keys, StringComparer.Ordinal);
                    using var listCmd = connection.CreateCommand();
                    listCmd.CommandText = "SELECT username FROM username_partitions;";
                    var toDelete = new List<string>();

                    using (var reader = listCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader.GetString(0);
                            if (!keep.Contains(name))
                                toDelete.Add(name);
                        }
                    }

                    foreach (string name in toDelete)
                    {
                        using var del = connection.CreateCommand();
                        del.CommandText = "DELETE FROM username_partitions WHERE username = $username;";
                        del.Parameters.AddWithValue("$username", name);
                        del.ExecuteNonQuery();
                    }
                }

                long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var (username, result) in recomputedByUsername)
                {
                    var payload = EzLocalProfilePartitionPayload.FromAggregation(result);
                    string json = JsonSerializer.Serialize(payload);

                    using var upsert = connection.CreateCommand();
                    upsert.CommandText = """
                                         INSERT INTO username_partitions (username, payload_json, updated_at)
                                         VALUES ($username, $payload_json, $updated_at)
                                         ON CONFLICT(username) DO UPDATE SET
                                             payload_json = excluded.payload_json,
                                             updated_at = excluded.updated_at;
                                         """;
                    upsert.Parameters.AddWithValue("$username", username);
                    upsert.Parameters.AddWithValue("$payload_json", json);
                    upsert.Parameters.AddWithValue("$updated_at", updatedAt);
                    upsert.ExecuteNonQuery();
                }

                var merged = new EzLocalProfileAggregationResult
                {
                    ComputedAt = DateTimeOffset.UtcNow,
                };

                var included = new List<string>();

                using (var read = connection.CreateCommand())
                {
                    read.CommandText = "SELECT username, payload_json FROM username_partitions ORDER BY username;";
                    using var reader = read.ExecuteReader();

                    while (reader.Read())
                    {
                        string username = reader.GetString(0);
                        string json = reader.GetString(1);
                        included.Add(username);

                        try
                        {
                            var payload = JsonSerializer.Deserialize<EzLocalProfilePartitionPayload>(json);
                            payload?.MergeInto(merged);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"[EzLocalProfile] Bad partition for {username}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                        }
                    }
                }

                merged.IncludedUsernames = included;
                EzLocalProfileAggregator.MergeOnlineContributions(merged, onlineContributions, localOnlineScoreIds);

                clearTables(connection);
                recreateManiaColumnTable(connection);
                writeAggregationTables(connection, merged);

                setMeta(connection, "schema_version", SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, "last_computed_at", merged.ComputedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                setMeta(connection, "included_usernames_json", JsonSerializer.Serialize(included));

                transaction.Commit();
            }
        }

        private static void writeAggregationTables(SqliteConnection connection, EzLocalProfileAggregationResult result)
        {
            foreach (var (rulesetId, stats) in result.RulesetStats)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO ruleset_stats (ruleset_id, total_keys, avg_kps, max_kps, score_count, kps_sample_count, total_pp, total_duration_ms)
                                  VALUES ($ruleset_id, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count, $total_pp, $total_duration_ms);
                                  """;
                cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
                cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                cmd.Parameters.AddWithValue("$total_pp", stats.TotalPp);
                cmd.Parameters.AddWithValue("$total_duration_ms", stats.TotalDurationMs);
                cmd.ExecuteNonQuery();
            }

            foreach (var (keyCount, stats) in result.ManiaKeyStats)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO mania_key_stats (key_count, total_keys, avg_kps, max_kps, score_count, kps_sample_count, total_pp, total_duration_ms)
                                  VALUES ($key_count, $total_keys, $avg_kps, $max_kps, $score_count, $kps_sample_count, $total_pp, $total_duration_ms);
                                  """;
                cmd.Parameters.AddWithValue("$key_count", keyCount);
                cmd.Parameters.AddWithValue("$total_keys", stats.TotalKeys);
                cmd.Parameters.AddWithValue("$avg_kps", stats.KpsSampleCount > 0 ? stats.KpsSum / stats.KpsSampleCount : 0);
                cmd.Parameters.AddWithValue("$max_kps", stats.MaxKps);
                cmd.Parameters.AddWithValue("$score_count", stats.ScoreCount);
                cmd.Parameters.AddWithValue("$kps_sample_count", stats.KpsSampleCount);
                cmd.Parameters.AddWithValue("$total_pp", stats.TotalPp);
                cmd.Parameters.AddWithValue("$total_duration_ms", stats.TotalDurationMs);
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

            foreach (var ((rulesetId, starBucket), count) in result.XxyPlayCounts)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO xxy_play_counts (ruleset_id, star_bucket, count)
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

            writeDrillScores(connection, result.DrillScores);
        }

        private static void writeDrillScores(SqliteConnection connection, IReadOnlyList<EzLocalProfileDrillScoreRow> rows)
        {
            foreach (var row in rows)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO drill_scores (
                                      score_id, score_hash, username, ruleset_id, rank, pp_resolved, accuracy,
                                      max_combo, max_achievable_combo, total_score, mods_json, total_keys,
                                      beatmap_hash, beatmap_id, beatmap_set_id, title, artist, difficulty_name,
                                      mapper_username, beatmap_status, star_rating, xxy_star_rating, map_performance_points,
                                      kps_avg, kps_max, kps_list_json, column_counts_json, hold_counts_json,
                                      avg_abs_offset_ms, has_video, has_storyboard, date_ms)
                                  VALUES (
                                      $score_id, $score_hash, $username, $ruleset_id, $rank, $pp_resolved, $accuracy,
                                      $max_combo, $max_achievable_combo, $total_score, $mods_json, $total_keys,
                                      $beatmap_hash, $beatmap_id, $beatmap_set_id, $title, $artist, $difficulty_name,
                                      $mapper_username, $beatmap_status, $star_rating, $xxy_star_rating, $map_performance_points,
                                      $kps_avg, $kps_max, $kps_list_json, $column_counts_json, $hold_counts_json,
                                      $avg_abs_offset_ms, $has_video, $has_storyboard, $date_ms);
                                  """;
                cmd.Parameters.AddWithValue("$score_id", row.ScoreId.ToString("N"));
                cmd.Parameters.AddWithValue("$score_hash", row.ScoreHash);
                cmd.Parameters.AddWithValue("$username", row.Username);
                cmd.Parameters.AddWithValue("$ruleset_id", row.RulesetId);
                cmd.Parameters.AddWithValue("$rank", (int)row.Rank);
                cmd.Parameters.AddWithValue("$pp_resolved", row.PpResolved);
                cmd.Parameters.AddWithValue("$accuracy", row.Accuracy);
                cmd.Parameters.AddWithValue("$max_combo", row.MaxCombo);
                cmd.Parameters.AddWithValue("$max_achievable_combo", row.MaxAchievableCombo);
                cmd.Parameters.AddWithValue("$total_score", row.TotalScore);
                cmd.Parameters.AddWithValue("$mods_json", row.ModsJson);
                cmd.Parameters.AddWithValue("$total_keys", row.TotalKeys);
                cmd.Parameters.AddWithValue("$beatmap_hash", row.BeatmapHash);
                cmd.Parameters.AddWithValue("$beatmap_id", row.BeatmapId.ToString("N"));
                cmd.Parameters.AddWithValue("$beatmap_set_id", row.BeatmapSetId?.ToString("N") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$title", row.Title);
                cmd.Parameters.AddWithValue("$artist", row.Artist);
                cmd.Parameters.AddWithValue("$difficulty_name", row.DifficultyName);
                cmd.Parameters.AddWithValue("$mapper_username", row.MapperUsername);
                cmd.Parameters.AddWithValue("$beatmap_status", (int)row.BeatmapStatus);
                cmd.Parameters.AddWithValue("$star_rating", row.StarRating);
                cmd.Parameters.AddWithValue("$xxy_star_rating", row.XxyStarRating);
                cmd.Parameters.AddWithValue("$map_performance_points", row.MapPerformancePoints);
                cmd.Parameters.AddWithValue("$kps_avg", row.KpsAvg);
                cmd.Parameters.AddWithValue("$kps_max", row.KpsMax);
                cmd.Parameters.AddWithValue("$kps_list_json", row.KpsListJson);
                cmd.Parameters.AddWithValue("$column_counts_json", row.ColumnCountsJson);
                cmd.Parameters.AddWithValue("$hold_counts_json", row.HoldCountsJson);
                cmd.Parameters.AddWithValue("$avg_abs_offset_ms", row.AvgAbsOffsetMs ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$has_video", row.HasVideo ? 1 : 0);
                cmd.Parameters.AddWithValue("$has_storyboard", row.HasStoryboard ? 1 : 0);
                cmd.Parameters.AddWithValue("$date_ms", row.Date.ToUnixTimeMilliseconds());
                cmd.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<EzLocalProfileDrillScoreRow> readDrillScores(SqliteConnection connection, int rulesetId)
        {
            var list = new List<EzLocalProfileDrillScoreRow>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              SELECT score_id, score_hash, username, ruleset_id, rank, pp_resolved, accuracy,
                                     max_combo, max_achievable_combo, total_score, mods_json, total_keys,
                                     beatmap_hash, beatmap_id, beatmap_set_id, title, artist, difficulty_name,
                                     mapper_username, beatmap_status, star_rating, xxy_star_rating, map_performance_points,
                                     kps_avg, kps_max, kps_list_json, column_counts_json, hold_counts_json,
                                     avg_abs_offset_ms, has_video, has_storyboard, date_ms
                              FROM drill_scores
                              WHERE ruleset_id = $ruleset_id
                              ORDER BY pp_resolved DESC, date_ms DESC;
                              """;
            cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileDrillScoreRow
                {
                    ScoreId = Guid.Parse(reader.GetString(0)),
                    ScoreHash = reader.GetString(1),
                    Username = reader.GetString(2),
                    RulesetId = reader.GetInt32(3),
                    Rank = (ScoreRank)reader.GetInt32(4),
                    PpResolved = reader.GetDouble(5),
                    Accuracy = reader.GetDouble(6),
                    MaxCombo = reader.GetInt32(7),
                    MaxAchievableCombo = reader.GetInt32(8),
                    TotalScore = reader.GetInt64(9),
                    ModsJson = reader.GetString(10),
                    TotalKeys = reader.GetInt64(11),
                    BeatmapHash = reader.GetString(12),
                    BeatmapId = Guid.Parse(reader.GetString(13)),
                    BeatmapSetId = reader.IsDBNull(14) ? null : Guid.Parse(reader.GetString(14)),
                    Title = reader.GetString(15),
                    Artist = reader.GetString(16),
                    DifficultyName = reader.GetString(17),
                    MapperUsername = reader.GetString(18),
                    BeatmapStatus = (BeatmapOnlineStatus)reader.GetInt32(19),
                    StarRating = reader.GetDouble(20),
                    XxyStarRating = reader.GetDouble(21),
                    MapPerformancePoints = reader.GetDouble(22),
                    KpsAvg = reader.GetDouble(23),
                    KpsMax = reader.GetDouble(24),
                    KpsListJson = reader.GetString(25),
                    ColumnCountsJson = reader.GetString(26),
                    HoldCountsJson = reader.GetString(27),
                    AvgAbsOffsetMs = reader.IsDBNull(28) ? null : reader.GetDouble(28),
                    HasVideo = reader.GetInt32(29) != 0,
                    HasStoryboard = reader.GetInt32(30) != 0,
                    Date = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(31)),
                });
            }

            return list;
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
                                      kps_sample_count INTEGER NOT NULL,
                                      total_pp REAL NOT NULL DEFAULT 0,
                                      total_duration_ms INTEGER NOT NULL DEFAULT 0
                                  );
                                  CREATE TABLE IF NOT EXISTS mania_key_stats (
                                      key_count INTEGER PRIMARY KEY NOT NULL,
                                      total_keys INTEGER NOT NULL,
                                      avg_kps REAL NOT NULL,
                                      max_kps REAL NOT NULL,
                                      score_count INTEGER NOT NULL,
                                      kps_sample_count INTEGER NOT NULL,
                                      total_pp REAL NOT NULL DEFAULT 0,
                                      total_duration_ms INTEGER NOT NULL DEFAULT 0
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
                                  CREATE TABLE IF NOT EXISTS xxy_play_counts (
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
                                  CREATE TABLE IF NOT EXISTS online_score_contributions (
                                      online_id INTEGER PRIMARY KEY NOT NULL,
                                      ruleset_id INTEGER NOT NULL,
                                      rank INTEGER NOT NULL,
                                      star_rating REAL NOT NULL,
                                      circle_size REAL NOT NULL,
                                      approach_rate REAL NOT NULL,
                                      key_count INTEGER NOT NULL,
                                      pp REAL NOT NULL DEFAULT 0,
                                      duration_ms INTEGER NOT NULL DEFAULT 0
                                  );
                                  CREATE TABLE IF NOT EXISTS username_partitions (
                                      username TEXT PRIMARY KEY NOT NULL,
                                      payload_json TEXT NOT NULL,
                                      updated_at INTEGER NOT NULL
                                  );
                                  CREATE TABLE IF NOT EXISTS drill_scores (
                                      score_id TEXT PRIMARY KEY NOT NULL,
                                      score_hash TEXT NOT NULL,
                                      username TEXT NOT NULL,
                                      ruleset_id INTEGER NOT NULL,
                                      rank INTEGER NOT NULL,
                                      pp_resolved REAL NOT NULL,
                                      accuracy REAL NOT NULL,
                                      max_combo INTEGER NOT NULL,
                                      max_achievable_combo INTEGER NOT NULL,
                                      total_score INTEGER NOT NULL,
                                      mods_json TEXT NOT NULL,
                                      total_keys INTEGER NOT NULL,
                                      beatmap_hash TEXT NOT NULL,
                                      beatmap_id TEXT NOT NULL,
                                      beatmap_set_id TEXT,
                                      title TEXT NOT NULL,
                                      artist TEXT NOT NULL,
                                      difficulty_name TEXT NOT NULL,
                                      mapper_username TEXT NOT NULL,
                                      beatmap_status INTEGER NOT NULL,
                                      star_rating REAL NOT NULL,
                                      xxy_star_rating REAL NOT NULL,
                                      map_performance_points REAL NOT NULL,
                                      kps_avg REAL NOT NULL,
                                      kps_max REAL NOT NULL,
                                      kps_list_json TEXT NOT NULL,
                                      column_counts_json TEXT NOT NULL,
                                      hold_counts_json TEXT NOT NULL,
                                      avg_abs_offset_ms REAL,
                                      has_video INTEGER NOT NULL,
                                      has_storyboard INTEGER NOT NULL,
                                      date_ms INTEGER NOT NULL
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_drill_scores_ruleset_pp
                                      ON drill_scores(ruleset_id, pp_resolved DESC, date_ms DESC);
                                  """;
                cmd.ExecuteNonQuery();
            }

            ensureColumn(connection, "ruleset_stats", "total_pp", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "ruleset_stats", "total_duration_ms", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "mania_key_stats", "total_pp", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "mania_key_stats", "total_duration_ms", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "online_score_contributions", "pp", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "online_score_contributions", "duration_ms", "INTEGER NOT NULL DEFAULT 0");

            setMeta(connection, "schema_version", SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
        }

        private static void ensureColumn(SqliteConnection connection, string table, string column, string typeSql)
        {
            using var check = connection.CreateCommand();
            check.CommandText = $"PRAGMA table_info({table});";
            using var reader = check.ExecuteReader();

            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            reader.Close();

            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeSql};";
            alter.ExecuteNonQuery();
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
                              DELETE FROM xxy_play_counts WHERE TRUE;
                              DELETE FROM std_attr_affinity WHERE TRUE;
                              DELETE FROM drill_scores WHERE TRUE;
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
            cmd.CommandText = "SELECT ruleset_id, total_keys, avg_kps, max_kps, score_count, kps_sample_count, total_pp, total_duration_ms FROM ruleset_stats ORDER BY ruleset_id;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileRulesetStats(
                    reader.GetInt32(0),
                    reader.GetInt64(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5),
                    reader.GetDouble(6),
                    reader.GetInt64(7)));
            }

            return list;
        }

        private static IReadOnlyList<EzLocalProfileManiaKeyStats> readManiaKeyStats(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileManiaKeyStats>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT key_count, total_keys, avg_kps, max_kps, score_count, kps_sample_count, total_pp, total_duration_ms FROM mania_key_stats ORDER BY key_count;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileManiaKeyStats(
                    reader.GetInt32(0),
                    reader.GetInt64(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5),
                    reader.GetDouble(6),
                    reader.GetInt64(7)));
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

        private static IReadOnlyList<EzLocalProfileXxyPlayCount> readXxyPlayCounts(SqliteConnection connection)
        {
            var list = new List<EzLocalProfileXxyPlayCount>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ruleset_id, star_bucket, count FROM xxy_play_counts ORDER BY ruleset_id, star_bucket;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new EzLocalProfileXxyPlayCount(
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

        private static string pullOffsetKey(EzLocalProfileOnlinePullKind kind, int rulesetId) => kind == EzLocalProfileOnlinePullKind.Best
            ? $"online_bp_offset_{rulesetId}"
            : $"online_mp_offset_{rulesetId}";

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
