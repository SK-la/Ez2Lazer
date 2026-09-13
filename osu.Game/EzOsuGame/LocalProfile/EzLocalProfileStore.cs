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
using osu.Game.EzOsuGame.Skills;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Single shared local profile statistics store (not keyed by login username).
    /// </summary>
    public class EzLocalProfileStore : IDisposable
    {
        public const string DATABASE_FILENAME = "ez-local-profile.sqlite";

        /// <summary>
        /// v2: per-play SSR / Dan caches for incremental (backfill) compute.
        /// v3: <c>drill_scores</c> is the detail source of truth (a partition payload no longer carries its drills),
        /// <c>username_partitions.excluded</c> marks a player left out of the archive, and
        /// <c>online_score_contributions.username</c> attributes pulled scores to the account that pulled them.
        /// </summary>
        public const int SCHEMA_VERSION = 3;

        /// <summary>
        /// Logic version for aggregated stats (independent of table schema).
        /// Bump when recompute is required for correct numbers (e.g. playable-mod analysis).
        /// v4: ManiaSummary 列统计 dense 化（末尾空列补 0，键数 = 真实 N），修正 7k 被当 6k 等列数错误。
        /// </summary>
        public const int CONTENT_VERSION = 4;

        private const string meta_content_version = "content_version";

        private readonly Storage storage;
        private readonly Lock sync = new Lock();
        private bool initialised;
        private string dbPath = string.Empty;
        private bool isDisposed;

        /// <summary>Legacy unattributed online rows are attributed to the pulling account once per session.</summary>
        private bool onlineContributionsAdopted;

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
                        NeedsRecompute = readNeedsRecompute(connection),
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

        /// <summary>
        /// Load display snapshot for <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> (merged archive)
        /// or a single stored username partition (local stats only, no online merge).
        /// </summary>
        public EzLocalProfileSnapshot LoadSnapshotForUsername(string? usernameFilter)
        {
            if (EzLocalProfileConstants.IsAllPlayersFilter(usernameFilter))
                return LoadSnapshot();

            // Not All ⇒ non-blank concrete username (IsAllPlayersFilter already rejected null/whitespace).
            string username = usernameFilter!;

            lock (sync)
            {
                try
                {
                    ensureInitialised();

                    using var connection = openConnection();

                    string? json = tryReadPartitionJson(connection, username);

                    if (json == null)
                    {
                        return new EzLocalProfileSnapshot
                        {
                            HasData = false,
                            IncludedUsernames = new[] { username },
                            LastComputedAt = tryReadLastComputedAt(connection),
                            NeedsRecompute = readNeedsRecompute(connection),
                        };
                    }

                    EzLocalProfilePartitionPayload? payload;

                    try
                    {
                        payload = JsonSerializer.Deserialize<EzLocalProfilePartitionPayload>(json);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[EzLocalProfile] Bad partition for {username}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                        return new EzLocalProfileSnapshot { HasData = false, IncludedUsernames = new[] { username } };
                    }

                    if (payload == null)
                        return new EzLocalProfileSnapshot { HasData = false, IncludedUsernames = new[] { username } };

                    var merged = new EzLocalProfileAggregationResult
                    {
                        IncludedUsernames = new[] { username },
                        ComputedAt = tryReadLastComputedAt(connection) ?? DateTimeOffset.UtcNow,
                    };
                    payload.MergeInto(merged);

                    return snapshotFromAggregation(merged, tryReadLastComputedAt(connection), readNeedsRecompute(connection));
                }
                catch (Exception ex)
                {
                    Logger.Log($"[EzLocalProfile] Failed to load partition snapshot: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
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

        public IReadOnlyList<EzLocalProfileDrillScoreRow> LoadDrillScores(int rulesetId, string? usernameFilter = null)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                return readDrillScores(connection, rulesetId, usernameFilter);
            }
        }

        /// <summary>
        /// Per-second KPS series for one drill score. Kept out of <see cref="LoadDrillScores"/> (the widest column)
        /// and fetched only for the currently selected score.
        /// </summary>
        public IReadOnlyList<double> LoadKpsList(Guid scoreId)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT kps_list_json FROM drill_scores WHERE score_id = $score_id;";
                cmd.Parameters.AddWithValue("$score_id", scoreId.ToString("N"));

                if (cmd.ExecuteScalar() is not string json || string.IsNullOrEmpty(json))
                    return Array.Empty<double>();

                try
                {
                    return JsonSerializer.Deserialize<List<double>>(json) ?? (IReadOnlyList<double>)Array.Empty<double>();
                }
                catch (Exception ex)
                {
                    Logger.Log($"[EzLocalProfile] Failed to parse kps list for {scoreId}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                    return Array.Empty<double>();
                }
            }
        }

        /// <summary>
        /// Previously stored average abs hit offsets for drill rows (non-null only).
        /// Used to avoid re-running replay sessions during bulk score analysis.
        /// </summary>
        public Dictionary<Guid, double> LoadAvgAbsOffsets(IReadOnlyCollection<string>? usernames = null)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();

                var result = new Dictionary<Guid, double>();
                using var cmd = connection.CreateCommand();

                var names = usernames?
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(EzLocalProfileConstants.NormaliseUsername)
                            .Distinct(StringComparer.Ordinal)
                            .ToList();

                if (names is { Count: > 0 })
                {
                    string[] placeholders = new string[names.Count];

                    for (int i = 0; i < names.Count; i++)
                    {
                        string param = $"$u{i}";
                        placeholders[i] = param;
                        cmd.Parameters.AddWithValue(param, names[i]);
                    }

                    cmd.CommandText = $"""
                                       SELECT score_id, avg_abs_offset_ms
                                       FROM drill_scores
                                       WHERE avg_abs_offset_ms IS NOT NULL
                                         AND username IN ({string.Join(", ", placeholders)});
                                       """;
                }
                else
                {
                    cmd.CommandText = """
                                      SELECT score_id, avg_abs_offset_ms
                                      FROM drill_scores
                                      WHERE avg_abs_offset_ms IS NOT NULL;
                                      """;
                }

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!Guid.TryParse(reader.GetString(0), out var scoreId))
                        continue;

                    result[scoreId] = reader.GetDouble(1);
                }

                return result;
            }
        }

        /// <summary>
        /// Score ids already analysed for one player: the reconciliation ledger, shaped like
        /// <c>EzSkillStore.GetPersistedChartDanHashes</c>. The compute path diffs this against the live score ids
        /// and analyses only the difference, so an interrupted run resumes instead of starting over.
        /// </summary>
        public HashSet<Guid> GetAnalyzedScoreIds(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT score_id FROM drill_scores WHERE username = $username;";
                cmd.Parameters.AddWithValue("$username", username);

                var ids = new HashSet<Guid>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out var id))
                        ids.Add(id);
                }

                return ids;
            }
        }

        /// <summary>Idempotency gate for the per-play ingest: a score whose drill row already exists is counted.</summary>
        public bool ContainsDrillScore(Guid scoreId)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM drill_scores WHERE score_id = $score_id LIMIT 1;";
                cmd.Parameters.AddWithValue("$score_id", scoreId.ToString("N"));
                return cmd.ExecuteScalar() != null;
            }
        }

        /// <summary>
        /// Append drill detail rows without touching the aggregate tables (idempotent by score id).
        /// The compute path flushes these per chunk, so a cancelled run keeps everything it had analysed.
        /// </summary>
        public void AppendDrills(IReadOnlyList<EzLocalProfileDrillScoreRow> rows)
        {
            if (rows.Count == 0)
                return;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();
                writeDrillScores(connection, rows, transaction);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Drop one player's drill detail rows. Used when that player is recomputed from every live score
        /// (clear-and-rebuild, or a stored drill that no longer maps to a live score), so leftovers from
        /// deleted scores cannot survive the rewrite.
        /// </summary>
        public void DeleteUserDrillScores(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM drill_scores WHERE username = $username;";
                cmd.Parameters.AddWithValue("$username", username);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Fold one newly analysed score into the archive: append its drill row, add its counters to the player's
        /// partition, then re-derive the merged archive tables.
        /// </summary>
        /// <remarks>
        /// The rebuild is reused on purpose. A hand-written per-table delta would be a second implementation of the
        /// same arithmetic (notably the weighted-average <c>avg_kps</c>) and the two could drift; with drills no
        /// longer stored in the payload every partition is small, so re-merging them costs almost nothing.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> when the score was added; <see langword="false"/> when it was already stored or when
        /// the player was fenced out of the analysis (an excluded partition is never re-included by an append).
        /// </returns>
        public bool AppendScores(
            string username,
            EzLocalProfileAggregationResult delta,
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> onlineContributions,
            IReadOnlyCollection<long> localOnlineScoreIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(delta);
            ArgumentNullException.ThrowIfNull(onlineContributions);
            ArgumentNullException.ThrowIfNull(localOnlineScoreIds);

            lock (sync)
            {
                ensureInitialised();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                // An excluded player is not part of the analysis: a play settling for them must not pull them back in.
                using (var probeExcluded = connection.CreateCommand())
                {
                    probeExcluded.Transaction = transaction;
                    probeExcluded.CommandText = "SELECT excluded FROM username_partitions WHERE username = $username;";
                    probeExcluded.Parameters.AddWithValue("$username", username);

                    if (probeExcluded.ExecuteScalar() is long excludedFlag && excludedFlag != 0)
                        return false;
                }

                // drill_scores is the ledger, so a score that already has a row must not be counted a second time.
                foreach (var drill in delta.DrillScores)
                {
                    using var probe = connection.CreateCommand();
                    probe.Transaction = transaction;
                    probe.CommandText = "SELECT 1 FROM drill_scores WHERE score_id = $score_id LIMIT 1;";
                    probe.Parameters.AddWithValue("$score_id", drill.ScoreId.ToString("N"));

                    if (probe.ExecuteScalar() != null)
                        return false;
                }

                writeDrillScores(connection, delta.DrillScores, transaction);

                var merged = new EzLocalProfileAggregationResult();

                if (tryReadPartitionJson(connection, username) is { Length: > 0 } json)
                {
                    try
                    {
                        JsonSerializer.Deserialize<EzLocalProfilePartitionPayload>(json)?.MergeStatsInto(merged);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[EzLocalProfile] Bad partition for {username} while appending (totals rebuilt): {ex.Message}",
                            Ez2ConfigManager.LOGGER_NAME);
                        merged = new EzLocalProfileAggregationResult();
                    }
                }

                EzLocalProfilePartitionPayload.FromAggregation(delta).MergeStatsInto(merged);

                writePartition(connection, username, EzLocalProfilePartitionPayload.FromAggregation(merged),
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), reinclude: false);

                rebuildArchiveFromPartitions(
                    connection,
                    includePlayerEvidence: false,
                    markContentVersionCurrent: false,
                    onlineContributions,
                    localOnlineScoreIds);

                transaction.Commit();

                return true;
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

        /// <summary>
        /// Replace all dan clear evidence rows for <paramref name="username"/> (DATA-3 / DATA-H).
        /// </summary>
        public void ReplaceDanClears(string username, IReadOnlyList<EzDanClearEvidenceRow> rows)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(rows);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                replaceEvidenceRows(connection, "dan_clear_evidence", username, rows, (ins, row) =>
                {
                    ins.CommandText = """
                                      INSERT INTO dan_clear_evidence
                                          (username, key_count, side, beatmap_hash, rate, credited_dan, accuracy, scored_at_ms, algorithm_version)
                                      VALUES
                                          ($username, $key_count, $side, $beatmap_hash, $rate, $credited_dan, $accuracy, $scored_at_ms, $algorithm_version);
                                      """;
                    ins.Parameters.AddWithValue("$username", username);
                    ins.Parameters.AddWithValue("$key_count", row.KeyCount);
                    ins.Parameters.AddWithValue("$side", row.Side);
                    ins.Parameters.AddWithValue("$beatmap_hash", row.BeatmapHash);
                    ins.Parameters.AddWithValue("$rate", row.Rate);
                    ins.Parameters.AddWithValue("$credited_dan", row.CreditedDan);
                    ins.Parameters.AddWithValue("$accuracy", row.Accuracy);
                    ins.Parameters.AddWithValue("$scored_at_ms", row.ScoredAt.ToUnixTimeMilliseconds());
                    ins.Parameters.AddWithValue("$algorithm_version", row.AlgorithmVersion);
                });
            }
        }

        /// <summary>
        /// Replace all SSR axis play evidence rows for <paramref name="username"/> (DATA-3 / DATA-H).
        /// </summary>
        public void ReplaceAxisPlays(string username, IReadOnlyList<EzAxisPlayEvidenceRow> rows)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(rows);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                replaceEvidenceRows(connection, "axis_play_evidence", username, rows, (ins, row) =>
                {
                    ins.CommandText = """
                                      INSERT INTO axis_play_evidence
                                          (username, key_count, skill_id, beatmap_hash, axis_value, accuracy, rate, scored_at_ms, algorithm_version)
                                      VALUES
                                          ($username, $key_count, $skill_id, $beatmap_hash, $axis_value, $accuracy, $rate, $scored_at_ms, $algorithm_version);
                                      """;
                    ins.Parameters.AddWithValue("$username", username);
                    ins.Parameters.AddWithValue("$key_count", row.KeyCount);
                    ins.Parameters.AddWithValue("$skill_id", row.SkillId);
                    ins.Parameters.AddWithValue("$beatmap_hash", row.BeatmapHash);
                    ins.Parameters.AddWithValue("$axis_value", row.AxisValue);
                    ins.Parameters.AddWithValue("$accuracy", row.Accuracy);
                    ins.Parameters.AddWithValue("$rate", row.Rate);
                    ins.Parameters.AddWithValue("$scored_at_ms", row.ScoredAt.ToUnixTimeMilliseconds());
                    ins.Parameters.AddWithValue("$algorithm_version", row.AlgorithmVersion);
                });
            }
        }

        /// <summary>
        /// Dan clear evidence for <paramref name="username"/>.
        /// <see cref="EzLocalProfileConstants.IsAllPlayersFilter"/> reads all real-player rows (no username filter;
        /// excludes residual <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> keys).
        /// Defaults to current <see cref="EzDanAlgorithm.VERSION"/>.
        /// </summary>
        public IReadOnlyList<EzDanClearEvidenceRow> GetDanClears(string username, int? keyCount = null, string? side = null, int? algorithmVersion = null)
        {
            bool allPlayers = EzLocalProfileConstants.IsAllPlayersFilter(username);
            if (!allPlayers)
                ArgumentException.ThrowIfNullOrWhiteSpace(username);

            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = allPlayers
                    ? """
                      SELECT username, key_count, side, beatmap_hash, rate, credited_dan, accuracy, scored_at_ms, algorithm_version
                      FROM dan_clear_evidence
                      WHERE username != $all_sentinel
                        AND algorithm_version = $algorithm_version
                        AND ($key_count IS NULL OR key_count = $key_count)
                        AND ($side IS NULL OR side = $side)
                      ORDER BY credited_dan DESC, scored_at_ms DESC;
                      """
                    : """
                      SELECT username, key_count, side, beatmap_hash, rate, credited_dan, accuracy, scored_at_ms, algorithm_version
                      FROM dan_clear_evidence
                      WHERE username = $username
                        AND algorithm_version = $algorithm_version
                        AND ($key_count IS NULL OR key_count = $key_count)
                        AND ($side IS NULL OR side = $side)
                      ORDER BY credited_dan DESC, scored_at_ms DESC;
                      """;
                if (allPlayers)
                    cmd.Parameters.AddWithValue("$all_sentinel", EzLocalProfileConstants.ALL_PLAYERS);
                else
                    cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$algorithm_version", version);
                cmd.Parameters.AddWithValue("$key_count", (object?)keyCount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$side", (object?)side ?? DBNull.Value);

                var list = new List<EzDanClearEvidenceRow>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new EzDanClearEvidenceRow
                    {
                        Username = reader.GetString(0),
                        KeyCount = reader.GetInt32(1),
                        Side = reader.GetString(2),
                        BeatmapHash = reader.GetString(3),
                        Rate = reader.GetDouble(4),
                        CreditedDan = reader.GetDouble(5),
                        Accuracy = reader.GetDouble(6),
                        ScoredAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
                        AlgorithmVersion = reader.GetInt32(8),
                    });
                }

                return list;
            }
        }

        /// <summary>
        /// Axis play evidence for <paramref name="username"/>.
        /// <see cref="EzLocalProfileConstants.IsAllPlayersFilter"/> reads all real-player rows (no username filter;
        /// excludes residual <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> keys).
        /// Defaults to current <see cref="EzManiaSkillAlgorithm.VERSION"/>.
        /// </summary>
        public IReadOnlyList<EzAxisPlayEvidenceRow> GetAxisPlays(string username, int? keyCount = null, string? skillId = null, int? algorithmVersion = null)
        {
            bool allPlayers = EzLocalProfileConstants.IsAllPlayersFilter(username);
            if (!allPlayers)
                ArgumentException.ThrowIfNullOrWhiteSpace(username);

            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = allPlayers
                    ? """
                      SELECT username, key_count, skill_id, beatmap_hash, axis_value, accuracy, rate, scored_at_ms, algorithm_version
                      FROM axis_play_evidence
                      WHERE username != $all_sentinel
                        AND algorithm_version = $algorithm_version
                        AND ($key_count IS NULL OR key_count = $key_count)
                        AND ($skill_id IS NULL OR skill_id = $skill_id)
                      ORDER BY axis_value DESC, scored_at_ms DESC;
                      """
                    : """
                      SELECT username, key_count, skill_id, beatmap_hash, axis_value, accuracy, rate, scored_at_ms, algorithm_version
                      FROM axis_play_evidence
                      WHERE username = $username
                        AND algorithm_version = $algorithm_version
                        AND ($key_count IS NULL OR key_count = $key_count)
                        AND ($skill_id IS NULL OR skill_id = $skill_id)
                      ORDER BY axis_value DESC, scored_at_ms DESC;
                      """;
                if (allPlayers)
                    cmd.Parameters.AddWithValue("$all_sentinel", EzLocalProfileConstants.ALL_PLAYERS);
                else
                    cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$algorithm_version", version);
                cmd.Parameters.AddWithValue("$key_count", (object?)keyCount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$skill_id", (object?)skillId ?? DBNull.Value);

                var list = new List<EzAxisPlayEvidenceRow>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new EzAxisPlayEvidenceRow
                    {
                        Username = reader.GetString(0),
                        KeyCount = reader.GetInt32(1),
                        SkillId = reader.GetString(2),
                        BeatmapHash = reader.GetString(3),
                        AxisValue = reader.GetDouble(4),
                        Accuracy = reader.GetDouble(5),
                        Rate = reader.GetDouble(6),
                        ScoredAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
                        AlgorithmVersion = reader.GetInt32(8),
                    });
                }

                return list;
            }
        }

        private static void replaceEvidenceRows<T>(
            SqliteConnection connection,
            string table,
            string username,
            IReadOnlyList<T> rows,
            Action<SqliteCommand, T> bindInsert)
        {
            using var tx = connection.BeginTransaction();

            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = $"DELETE FROM {table} WHERE username = $username;";
                del.Parameters.AddWithValue("$username", username);
                del.ExecuteNonQuery();
            }

            foreach (var row in rows)
            {
                using var ins = connection.CreateCommand();
                ins.Transaction = tx;
                bindInsert(ins, row);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
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
                                      (online_id, username, ruleset_id, rank, star_rating, circle_size, approach_rate, key_count, pp, duration_ms)
                                  VALUES
                                      ($online_id, $username, $ruleset_id, $rank, $star_rating, $circle_size, $approach_rate, $key_count, $pp, $duration_ms)
                                  ON CONFLICT(online_id) DO UPDATE SET
                                      username = excluded.username,
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
                cmd.Parameters.AddWithValue("$username", contribution.Username);
                cmd.Parameters.AddWithValue("$ruleset_id", contribution.RulesetId);
                cmd.Parameters.AddWithValue("$rank", (int)contribution.Rank);
                cmd.Parameters.AddWithValue("$star_rating", contribution.StarRating);
                cmd.Parameters.AddWithValue("$circle_size", contribution.CircleSize);
                cmd.Parameters.AddWithValue("$approach_rate", contribution.ApproachRate);
                cmd.Parameters.AddWithValue("$key_count", contribution.KeyCount);
                cmd.Parameters.AddWithValue("$pp", contribution.Pp);
                cmd.Parameters.AddWithValue("$duration_ms", contribution.DurationMs);
                cmd.ExecuteNonQuery();

                adoptOrphanOnlineContributions(connection, contribution.Username);
            }
        }

        /// <summary>
        /// Attribute legacy unattributed rows (<c>username = ''</c>) to the account now pulling. They can only ever
        /// have come from the same login (a pull only fetches the logged-in user's scores), so the pulling account is
        /// the only defensible owner. Runs once per session, and only for a named contribution.
        /// </summary>
        private void adoptOrphanOnlineContributions(SqliteConnection connection, string username)
        {
            if (onlineContributionsAdopted || string.IsNullOrEmpty(username))
                return;

            onlineContributionsAdopted = true;

            using var adopt = connection.CreateCommand();
            adopt.CommandText = "UPDATE online_score_contributions SET username = $username WHERE username = '';";
            adopt.Parameters.AddWithValue("$username", username);
            int adopted = adopt.ExecuteNonQuery();

            if (adopted > 0)
            {
                Logger.Log($"[EzLocalProfile] Attributed {adopted} previously unattributed online score contribution(s) to {username}.",
                    Ez2ConfigManager.LOGGER_NAME);
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
                                  SELECT online_id, ruleset_id, rank, star_rating, circle_size, approach_rate, key_count, pp, duration_ms, username
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
                        reader.GetInt64(8),
                        reader.GetString(9)));
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

                // drill_scores is the detail source of truth and is not part of clearTables; a full replace owns its rows.
                using (var clearDrills = connection.CreateCommand())
                {
                    clearDrills.Transaction = transaction;
                    clearDrills.CommandText = "DELETE FROM drill_scores WHERE TRUE;";
                    clearDrills.ExecuteNonQuery();
                }

                // Recreate column table so the current single-version schema is always applied on recompute.
                recreateManiaColumnTable(connection);

                setMeta(connection, "schema_version", SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_content_version, CONTENT_VERSION.ToString(CultureInfo.InvariantCulture));
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

                        // A dropped slice takes its detail rows with it, so a removed player leaves no drills behind.
                        using var delDrills = connection.CreateCommand();
                        delDrills.CommandText = "DELETE FROM drill_scores WHERE username = $username;";
                        delDrills.Parameters.AddWithValue("$username", name);
                        delDrills.ExecuteNonQuery();
                    }
                }

                long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var (username, result) in recomputedByUsername)
                    writePartition(connection, username, EzLocalProfilePartitionPayload.FromAggregation(result), updatedAt);

                rebuildArchiveFromPartitions(
                    connection,
                    includePlayerEvidence: true,
                    markContentVersionCurrent: recomputedByUsername.Count > 0,
                    onlineContributions,
                    localOnlineScoreIds);

                transaction.Commit();
            }
        }

        /// <summary>
        /// Re-derive the merged archive tables from the stored partitions. Drill rows are not touched: they live in
        /// <c>drill_scores</c> only, which the compute path and the per-play ingest append to directly.
        /// Shared by incremental compute (<see cref="ApplyUsernamePartitions"/>) and player exclusion
        /// (<see cref="ExcludeUsernames"/>).
        /// </summary>
        /// <param name="includePlayerEvidence">
        /// When true, also wipes the per-player evidence tables (<c>dan_clear_evidence</c> / <c>axis_play_evidence</c>),
        /// which the caller is expected to repopulate in a full skills pass. Excluding a player must pass
        /// <see langword="false"/>, otherwise fencing one player would silently drop every other player's
        /// Dan / axis evidence (those tables are only written by <c>writePlayerSkills</c>).
        /// </param>
        /// <param name="markContentVersionCurrent">
        /// True only when the local partitions were just rebuilt with the current aggregator logic; an exclusion
        /// must not stamp the content version, or a later compute would trust slices it never re-analysed.
        /// </param>
        private static void rebuildArchiveFromPartitions(
            SqliteConnection connection,
            bool includePlayerEvidence,
            bool markContentVersionCurrent,
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> onlineContributions,
            IReadOnlyCollection<long> localOnlineScoreIds)
        {
            var merged = new EzLocalProfileAggregationResult
            {
                ComputedAt = DateTimeOffset.UtcNow,
            };

            var included = new List<string>();
            var partitionJsonByUser = new List<(string Username, string Json)>();

            // Excluded players keep their partition (and its per-play caches) but are left out of the archive totals.
            using (var read = connection.CreateCommand())
            {
                read.CommandText = "SELECT username, payload_json FROM username_partitions WHERE excluded = 0 ORDER BY username;";
                using var reader = read.ExecuteReader();

                while (reader.Read())
                {
                    string username = reader.GetString(0);
                    string json = reader.GetString(1);
                    included.Add(username);
                    partitionJsonByUser.Add((username, json));
                }
            }

            // Clear aggregation tables before streaming drills so we never hold every partition's drills in memory.
            clearTables(connection, includePlayerEvidence);
            recreateManiaColumnTable(connection);

            foreach (var (username, json) in partitionJsonByUser)
            {
                EzLocalProfilePartitionPayload payload;

                try
                {
                    payload = JsonSerializer.Deserialize<EzLocalProfilePartitionPayload>(json)
                              ?? throw new InvalidOperationException("Partition payload deserialized to null.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"[EzLocalProfile] Bad partition for {username}: {ex.Message}", ex);
                }

                payload.MergeStatsInto(merged);
            }

            merged.IncludedUsernames = included;

            var partitionScoreCounts = merged.RulesetStats.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ScoreCount);

            EzLocalProfileAggregator.MergeOnlineContributions(merged, filterOnlineContributions(onlineContributions, included), new HashSet<long>(localOnlineScoreIds));

            foreach (var (rulesetId, partitionCount) in partitionScoreCounts)
            {
                int allCount = merged.RulesetStats.TryGetValue(rulesetId, out var stats) ? stats.ScoreCount : 0;

                if (allCount < partitionCount)
                {
                    throw new InvalidOperationException(
                        $"[EzLocalProfile] All ScoreCount for ruleset {rulesetId} ({allCount}) is less than partition sum ({partitionCount}).");
                }
            }

            writeAggregationTables(connection, merged, writeDrills: false);

            setMeta(connection, "schema_version", SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
            // Only mark logic current when local partitions were rebuilt with the new aggregator.
            if (markContentVersionCurrent)
                setMeta(connection, meta_content_version, CONTENT_VERSION.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "last_computed_at", merged.ComputedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "included_usernames_json", JsonSerializer.Serialize(included));
        }

        /// <summary>
        /// Contributions whose owning player is not part of the archive (excluded, or never selected) do not count:
        /// <c>All</c> is the sum over the players in <paramref name="included"/>, nothing else. A legacy unattributed
        /// row stays counted, though — it has no owner to exclude, and dropping it would silently change existing
        /// numbers before the next pull attributes it.
        /// </summary>
        private static IReadOnlyList<EzLocalProfileOnlineScoreContribution> filterOnlineContributions(
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> contributions,
            IReadOnlyCollection<string> included)
        {
            if (contributions.Count == 0)
                return contributions;

            var includedSet = new HashSet<string>(
                included.Select(EzLocalProfileConstants.NormaliseUsername),
                StringComparer.Ordinal);

            return contributions
                   .Where(c => string.IsNullOrEmpty(c.Username)
                               || includedSet.Contains(EzLocalProfileConstants.NormaliseUsername(c.Username)))
                   .ToList();
        }

        /// <summary>
        /// Drop one online contribution. Used when the very same play is later imported locally: the detailed local
        /// score supersedes the pulled summary, and keeping both would count the play twice.
        /// </summary>
        /// <returns><see langword="true"/> when a row was removed.</returns>
        public bool RemoveOnlineScoreContribution(long onlineId)
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM online_score_contributions WHERE online_id = $online_id;";
                cmd.Parameters.AddWithValue("$online_id", onlineId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// Single partition payload used as the incremental compute cache, or <see langword="null"/> when absent/unreadable.
        /// </summary>
        public EzLocalProfilePartitionPayload? TryLoadPartitionPayload(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                string? json = tryReadPartitionJson(connection, username);

                if (string.IsNullOrEmpty(json))
                    return null;

                try
                {
                    return JsonSerializer.Deserialize<EzLocalProfilePartitionPayload>(json);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[EzLocalProfile] Failed to parse partition for {username}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                    return null;
                }
            }
        }

        /// <summary>
        /// Persist one partition without rebuilding archive totals.
        /// Used to flush progress during a long compute so cancelling keeps what was already analysed.
        /// </summary>
        public void SavePartitionPayload(string username, EzLocalProfilePartitionPayload payload)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(payload);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                writePartition(connection, username, payload, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        /// <summary>True when stored stats came from an older analysis content version and must be rebuilt from scratch.</summary>
        public bool NeedsRecompute()
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                return readNeedsRecompute(connection);
            }
        }

        /// <summary>Per-play SSR cache for one algorithm version, keyed by score id (score id is globally unique).</summary>
        public Dictionary<Guid, EzSsrPlayCacheRow> LoadSsrPlayCache(int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  SELECT score_id, username, beatmap_hash, key_count, rate, scored_at_ms, has_ssr, vector_json, algorithm_version
                                  FROM ssr_score_cache
                                  WHERE algorithm_version = $algorithm_version;
                                  """;
                cmd.Parameters.AddWithValue("$algorithm_version", version);

                var result = new Dictionary<Guid, EzSsrPlayCacheRow>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!Guid.TryParse(reader.GetString(0), out var scoreId))
                        continue;

                    result[scoreId] = new EzSsrPlayCacheRow(
                        scoreId,
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetDouble(4),
                        DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                        reader.GetInt32(6) != 0,
                        deserializeVector(reader.GetString(7)),
                        reader.GetInt32(8));
                }

                return result;
            }
        }

        public void UpsertSsrPlayCache(IReadOnlyList<EzSsrPlayCacheRow> rows)
        {
            if (rows.Count == 0)
                return;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                foreach (var row in rows)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO ssr_score_cache
                                          (score_id, username, beatmap_hash, key_count, rate, scored_at_ms, has_ssr, vector_json, algorithm_version)
                                      VALUES
                                          ($score_id, $username, $beatmap_hash, $key_count, $rate, $scored_at_ms, $has_ssr, $vector_json, $algorithm_version)
                                      ON CONFLICT(score_id) DO UPDATE SET
                                          username = excluded.username,
                                          beatmap_hash = excluded.beatmap_hash,
                                          key_count = excluded.key_count,
                                          rate = excluded.rate,
                                          scored_at_ms = excluded.scored_at_ms,
                                          has_ssr = excluded.has_ssr,
                                          vector_json = excluded.vector_json,
                                          algorithm_version = excluded.algorithm_version;
                                      """;
                    cmd.Parameters.AddWithValue("$score_id", row.ScoreId.ToString("N"));
                    cmd.Parameters.AddWithValue("$username", row.Username);
                    cmd.Parameters.AddWithValue("$beatmap_hash", row.BeatmapHash);
                    cmd.Parameters.AddWithValue("$key_count", row.KeyCount);
                    cmd.Parameters.AddWithValue("$rate", row.Rate);
                    cmd.Parameters.AddWithValue("$scored_at_ms", row.ScoredAt.ToUnixTimeMilliseconds());
                    cmd.Parameters.AddWithValue("$has_ssr", row.HasSsr ? 1 : 0);
                    cmd.Parameters.AddWithValue("$vector_json", serializeVector(row.Vector));
                    cmd.Parameters.AddWithValue("$algorithm_version", row.AlgorithmVersion);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        /// <summary>Per-play Dan cache for one algorithm version, keyed by score id.</summary>
        public Dictionary<Guid, EzDanPlayCacheRow> LoadDanPlayCache(int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  SELECT score_id, username, beatmap_hash, algorithm_version, credited, key_count, side, rate, credited_dan, accuracy, scored_at_ms
                                  FROM dan_score_cache
                                  WHERE algorithm_version = $algorithm_version;
                                  """;
                cmd.Parameters.AddWithValue("$algorithm_version", version);

                var result = new Dictionary<Guid, EzDanPlayCacheRow>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!Guid.TryParse(reader.GetString(0), out var scoreId))
                        continue;

                    result[scoreId] = new EzDanPlayCacheRow(
                        scoreId,
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetInt32(4) != 0,
                        reader.GetInt32(5),
                        reader.GetString(6),
                        reader.GetDouble(7),
                        reader.GetDouble(8),
                        reader.GetDouble(9),
                        DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10)));
                }

                return result;
            }
        }

        public void UpsertDanPlayCache(IReadOnlyList<EzDanPlayCacheRow> rows)
        {
            if (rows.Count == 0)
                return;

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                foreach (var row in rows)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                                      INSERT INTO dan_score_cache
                                          (score_id, username, beatmap_hash, credited, key_count, side, rate, credited_dan, accuracy, scored_at_ms, algorithm_version)
                                      VALUES
                                          ($score_id, $username, $beatmap_hash, $credited, $key_count, $side, $rate, $credited_dan, $accuracy, $scored_at_ms, $algorithm_version)
                                      ON CONFLICT(score_id) DO UPDATE SET
                                          username = excluded.username,
                                          beatmap_hash = excluded.beatmap_hash,
                                          credited = excluded.credited,
                                          key_count = excluded.key_count,
                                          side = excluded.side,
                                          rate = excluded.rate,
                                          credited_dan = excluded.credited_dan,
                                          accuracy = excluded.accuracy,
                                          scored_at_ms = excluded.scored_at_ms,
                                          algorithm_version = excluded.algorithm_version;
                                      """;
                    cmd.Parameters.AddWithValue("$score_id", row.ScoreId.ToString("N"));
                    cmd.Parameters.AddWithValue("$username", row.Username);
                    cmd.Parameters.AddWithValue("$beatmap_hash", row.BeatmapHash);
                    cmd.Parameters.AddWithValue("$credited", row.Credited ? 1 : 0);
                    cmd.Parameters.AddWithValue("$key_count", row.KeyCount);
                    cmd.Parameters.AddWithValue("$side", row.Side);
                    cmd.Parameters.AddWithValue("$rate", row.Rate);
                    cmd.Parameters.AddWithValue("$credited_dan", row.CreditedDan);
                    cmd.Parameters.AddWithValue("$accuracy", row.Accuracy);
                    cmd.Parameters.AddWithValue("$scored_at_ms", row.ScoredAt.ToUnixTimeMilliseconds());
                    cmd.Parameters.AddWithValue("$algorithm_version", row.AlgorithmVersion);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        /// <summary>Drop both per-play skill caches (clear-and-rebuild path).</summary>
        public void ClearSkillCaches()
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                // WHERE TRUE keeps the intentional full clear explicit (a bare DELETE FROM would be read as an oversight).
                cmd.CommandText = """
                                  DELETE FROM ssr_score_cache WHERE TRUE;
                                  DELETE FROM dan_score_cache WHERE TRUE;
                                  """;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Fences <paramref name="username"/> out of the merged archive: the partition (and with it every per-play
        /// skill cache) is kept, but the row is flagged excluded, so it stops feeding the aggregate tables and
        /// <see cref="LoadIncludedUsernames"/>. Re-selecting the player later only has to clear the flag and
        /// re-aggregate — the expensive MinaCalc per-play results survive.
        /// </summary>
        /// <remarks>
        /// Deliberately does not stamp <see cref="CONTENT_VERSION"/>: the remaining slices were not re-analysed,
        /// so marking the archive current would let a later incremental compute trust stale numbers.
        /// The player's own Realm skill rows are left alone; only the archive-wide <c>All</c> sentinel needs
        /// refreshing, which the caller does from the remaining included players.
        /// </remarks>
        /// <returns><see langword="true"/> when the player was newly excluded; false when absent or already excluded.</returns>
        public bool ExcludeUsernames(
            string username,
            IReadOnlyList<EzLocalProfileOnlineScoreContribution> onlineContributions,
            IReadOnlyCollection<long> localOnlineScoreIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(onlineContributions);
            ArgumentNullException.ThrowIfNull(localOnlineScoreIds);

            lock (sync)
            {
                ensureInitialised();

                using var connection = openConnection();

                using (var probe = connection.CreateCommand())
                {
                    probe.CommandText = "SELECT excluded FROM username_partitions WHERE username = $username;";
                    probe.Parameters.AddWithValue("$username", username);

                    if (probe.ExecuteScalar() is not long excluded)
                        return false;

                    if (excluded != 0)
                        return false;
                }

                using var transaction = connection.BeginTransaction();

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE username_partitions SET excluded = 1 WHERE username = $username;";
                    update.Parameters.AddWithValue("$username", username);
                    update.ExecuteNonQuery();
                }

                rebuildArchiveFromPartitions(
                    connection,
                    includePlayerEvidence: false,
                    markContentVersionCurrent: false,
                    onlineContributions,
                    localOnlineScoreIds);

                transaction.Commit();

                return true;
            }
        }

        /// <param name="reinclude">
        /// A full recompute (re-)includes the player: an excluded name that gets recomputed is back in the archive.
        /// The incremental single-play append passes <see langword="false"/>, because a play settling for a player who
        /// was fenced out must not silently pull them back into the totals.
        /// </param>
        private static void writePartition(SqliteConnection connection, string username, EzLocalProfilePartitionPayload payload, long updatedAt, bool reinclude = true)
        {
            string json = JsonSerializer.Serialize(payload);

            using var upsert = connection.CreateCommand();
            upsert.CommandText = reinclude
                ? """
                  INSERT INTO username_partitions (username, payload_json, updated_at, excluded)
                  VALUES ($username, $payload_json, $updated_at, 0)
                  ON CONFLICT(username) DO UPDATE SET
                      payload_json = excluded.payload_json,
                      updated_at = excluded.updated_at,
                      excluded = 0;
                  """
                : """
                  INSERT INTO username_partitions (username, payload_json, updated_at, excluded)
                  VALUES ($username, $payload_json, $updated_at, 0)
                  ON CONFLICT(username) DO UPDATE SET
                      payload_json = excluded.payload_json,
                      updated_at = excluded.updated_at;
                  """;
            upsert.Parameters.AddWithValue("$username", username);
            upsert.Parameters.AddWithValue("$payload_json", json);
            upsert.Parameters.AddWithValue("$updated_at", updatedAt);
            upsert.ExecuteNonQuery();
        }

        private static string serializeVector(EzSkillsetVector vector)
            => JsonSerializer.Serialize(new[]
            {
                vector.Overall,
                vector.Stream,
                vector.Jumpstream,
                vector.Handstream,
                vector.Stamina,
                vector.JackSpeed,
                vector.Chordjack,
                vector.Technical,
            });

        private static EzSkillsetVector deserializeVector(string json)
        {
            try
            {
                double[]? values = JsonSerializer.Deserialize<double[]>(json);

                if (values is { Length: >= EzSkillsetVector.RAW_LENGTH })
                {
                    return new EzSkillsetVector(
                        values[0], values[1], values[2], values[3],
                        values[4], values[5], values[6], values[7]);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalProfile] Failed to parse SSR vector cache: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
            }

            return default;
        }

        private static void writeAggregationTables(SqliteConnection connection, EzLocalProfileAggregationResult result, bool writeDrills = true)
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

            if (writeDrills)
                writeDrillScores(connection, result.DrillScores);
        }

        private static void writeDrillScores(SqliteConnection connection, IReadOnlyList<EzLocalProfileDrillScoreRow> rows, SqliteTransaction? transaction = null)
        {
            foreach (var row in rows)
            {
                using var cmd = connection.CreateCommand();

                // Only bind when a transaction is supplied: explicitly assigning null stops Microsoft.Data.Sqlite
                // from falling back to the connection's pending transaction, and the insert then throws.
                if (transaction != null)
                    cmd.Transaction = transaction;

                cmd.CommandText = """
                                  INSERT OR REPLACE INTO drill_scores (
                                      score_id, score_hash, username, ruleset_id, rank, pp_resolved, accuracy,
                                      max_combo, max_achievable_combo, total_score, mods_json, total_keys,
                                      beatmap_hash, beatmap_id, beatmap_set_id, title, artist, difficulty_name,
                                      mapper_username, beatmap_status, star_rating, xxy_star_rating, map_performance_points,
                                      kps_avg, kps_max, kps_list_json, column_counts_json, hold_counts_json,
                                      avg_abs_offset_ms, has_video, has_storyboard, date_ms,
                                      bpm, key_count, is_convert, rate, mod_acronyms_json)
                                  VALUES (
                                      $score_id, $score_hash, $username, $ruleset_id, $rank, $pp_resolved, $accuracy,
                                      $max_combo, $max_achievable_combo, $total_score, $mods_json, $total_keys,
                                      $beatmap_hash, $beatmap_id, $beatmap_set_id, $title, $artist, $difficulty_name,
                                      $mapper_username, $beatmap_status, $star_rating, $xxy_star_rating, $map_performance_points,
                                      $kps_avg, $kps_max, $kps_list_json, $column_counts_json, $hold_counts_json,
                                      $avg_abs_offset_ms, $has_video, $has_storyboard, $date_ms,
                                      $bpm, $key_count, $is_convert, $rate, $mod_acronyms_json);
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
                cmd.Parameters.AddWithValue("$bpm", row.Bpm);
                cmd.Parameters.AddWithValue("$key_count", row.KeyCount);
                cmd.Parameters.AddWithValue("$is_convert", row.IsConvert ? 1 : 0);
                cmd.Parameters.AddWithValue("$rate", row.Rate);
                cmd.Parameters.AddWithValue("$mod_acronyms_json", row.ModAcronymsJson);
                cmd.ExecuteNonQuery();
            }
        }

        private static string? tryReadPartitionJson(SqliteConnection connection, string username)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT payload_json FROM username_partitions WHERE username = $username;";
            cmd.Parameters.AddWithValue("$username", username);
            return cmd.ExecuteScalar() as string;
        }

        private static EzLocalProfileSnapshot snapshotFromAggregation(
            EzLocalProfileAggregationResult result,
            DateTimeOffset? lastComputedAt,
            bool needsRecompute)
        {
            bool hasData = result.RulesetStats.Count > 0 || result.DrillScores.Count > 0;

            return new EzLocalProfileSnapshot
            {
                HasData = hasData,
                NeedsRecompute = needsRecompute,
                LastComputedAt = lastComputedAt ?? result.ComputedAt,
                IncludedUsernames = result.IncludedUsernames,
                RulesetStats = result.RulesetStats
                                     .OrderBy(kv => kv.Key)
                                     .Select(kv => new EzLocalProfileRulesetStats(
                                         kv.Key,
                                         kv.Value.TotalKeys,
                                         kv.Value.KpsSampleCount > 0 ? kv.Value.KpsSum / kv.Value.KpsSampleCount : 0,
                                         kv.Value.MaxKps,
                                         kv.Value.ScoreCount,
                                         kv.Value.KpsSampleCount,
                                         kv.Value.TotalPp,
                                         kv.Value.TotalDurationMs))
                                     .ToList(),
                ManiaKeyStats = result.ManiaKeyStats
                                      .OrderBy(kv => kv.Key)
                                      .Select(kv => new EzLocalProfileManiaKeyStats(
                                          kv.Key,
                                          kv.Value.TotalKeys,
                                          kv.Value.KpsSampleCount > 0 ? kv.Value.KpsSum / kv.Value.KpsSampleCount : 0,
                                          kv.Value.MaxKps,
                                          kv.Value.ScoreCount,
                                          kv.Value.KpsSampleCount,
                                          kv.Value.TotalPp,
                                          kv.Value.TotalDurationMs))
                                      .ToList(),
                ManiaColumnStats = result.ManiaColumnStats
                                         .OrderBy(kv => kv.Key.KeyCount)
                                         .ThenBy(kv => kv.Key.Column)
                                         .Select(kv => new EzLocalProfileManiaColumnStats(
                                             kv.Key.KeyCount,
                                             kv.Key.Column,
                                             kv.Value.TotalKeys,
                                             kv.Value.KpsSampleCount > 0 ? kv.Value.KpsSum / kv.Value.KpsSampleCount : 0,
                                             kv.Value.MaxKps,
                                             kv.Value.ScoreCount,
                                             kv.Value.KpsSampleCount))
                                         .ToList(),
                GradeCounts = result.GradeCounts
                                    .OrderBy(kv => kv.Key.RulesetId)
                                    .ThenByDescending(kv => kv.Key.Rank)
                                    .Select(kv => new EzLocalProfileGradeCount(kv.Key.RulesetId, kv.Key.Rank, kv.Value))
                                    .ToList(),
                StarPlayCounts = result.StarPlayCounts
                                       .OrderBy(kv => kv.Key.RulesetId)
                                       .ThenBy(kv => kv.Key.StarBucket)
                                       .Select(kv => new EzLocalProfileStarPlayCount(kv.Key.RulesetId, kv.Key.StarBucket, kv.Value))
                                       .ToList(),
                XxyPlayCounts = result.XxyPlayCounts
                                      .OrderBy(kv => kv.Key.RulesetId)
                                      .ThenBy(kv => kv.Key.StarBucket)
                                      .Select(kv => new EzLocalProfileXxyPlayCount(kv.Key.RulesetId, kv.Key.StarBucket, kv.Value))
                                      .ToList(),
                StdAttrAffinities = result.StdAttrAffinities
                                          .OrderBy(kv => kv.Key.Attr)
                                          .ThenByDescending(kv => kv.Value.PlayCount)
                                          .Select(kv => new EzLocalProfileStdAttrAffinity(
                                              kv.Key.Attr,
                                              kv.Key.Value,
                                              kv.Value.PlayCount,
                                              kv.Value.HighGradeCount))
                                          .ToList(),
                DrillScores = result.DrillScores,
            };
        }

        private static IReadOnlyList<EzLocalProfileDrillScoreRow> readDrillScores(SqliteConnection connection, int rulesetId, string? usernameFilter)
        {
            var list = new List<EzLocalProfileDrillScoreRow>();
            using var cmd = connection.CreateCommand();

            bool filterUsername = !EzLocalProfileConstants.IsAllPlayersFilter(usernameFilter);

            cmd.CommandText = filterUsername
                ? """
                  SELECT score_id, score_hash, username, ruleset_id, rank, pp_resolved, accuracy,
                         max_combo, max_achievable_combo, total_score, mods_json, total_keys,
                         beatmap_hash, beatmap_id, beatmap_set_id, title, artist, difficulty_name,
                         mapper_username, beatmap_status, star_rating, xxy_star_rating, map_performance_points,
                         kps_avg, kps_max, '[]' AS kps_list_json, column_counts_json, hold_counts_json,
                         avg_abs_offset_ms, has_video, has_storyboard, date_ms,
                         bpm, key_count, is_convert, rate, mod_acronyms_json
                  FROM drill_scores
                  WHERE ruleset_id = $ruleset_id AND username = $username
                  ORDER BY pp_resolved DESC, date_ms DESC;
                  """
                : """
                  SELECT score_id, score_hash, username, ruleset_id, rank, pp_resolved, accuracy,
                         max_combo, max_achievable_combo, total_score, mods_json, total_keys,
                         beatmap_hash, beatmap_id, beatmap_set_id, title, artist, difficulty_name,
                         mapper_username, beatmap_status, star_rating, xxy_star_rating, map_performance_points,
                         kps_avg, kps_max, '[]' AS kps_list_json, column_counts_json, hold_counts_json,
                         avg_abs_offset_ms, has_video, has_storyboard, date_ms,
                         bpm, key_count, is_convert, rate, mod_acronyms_json
                  FROM drill_scores
                  WHERE ruleset_id = $ruleset_id
                  ORDER BY pp_resolved DESC, date_ms DESC;
                  """;
            cmd.Parameters.AddWithValue("$ruleset_id", rulesetId);
            if (filterUsername)
                cmd.Parameters.AddWithValue("$username", usernameFilter);
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
                    Bpm = reader.FieldCount > 32 && !reader.IsDBNull(32) ? reader.GetDouble(32) : 0,
                    KeyCount = reader.FieldCount > 33 && !reader.IsDBNull(33) ? reader.GetInt32(33) : 0,
                    IsConvert = reader.FieldCount > 34 && !reader.IsDBNull(34) && reader.GetInt32(34) != 0,
                    Rate = reader.FieldCount > 35 && !reader.IsDBNull(35) ? reader.GetDouble(35) : 1,
                    ModAcronymsJson = reader.FieldCount > 36 && !reader.IsDBNull(36) ? reader.GetString(36) : "[]",
                });
            }

            return list;
        }

        public EzLocalProfileInsights? TryLoadInsightsCache(string? usernameFilter)
        {
            string username = EzLocalProfileConstants.IsAllPlayersFilter(usernameFilter)
                ? EzLocalProfileConstants.ALL_PLAYERS
                : EzLocalProfileConstants.NormaliseUsername(usernameFilter);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT payload_json FROM insights_cache WHERE username = $username;";
                cmd.Parameters.AddWithValue("$username", username);
                string? json = cmd.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(json))
                    return null;

                try
                {
                    return JsonSerializer.Deserialize<EzLocalProfileInsights>(json);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[EzLocalProfile] Failed to deserialize insights cache: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                    return null;
                }
            }
        }

        public void SaveInsightsCache(string? usernameFilter, EzLocalProfileInsights insights)
        {
            string username = EzLocalProfileConstants.IsAllPlayersFilter(usernameFilter)
                ? EzLocalProfileConstants.ALL_PLAYERS
                : EzLocalProfileConstants.NormaliseUsername(usernameFilter);

            string json = JsonSerializer.Serialize(insights);

            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO insights_cache (username, payload_json, computed_at_ms)
                                  VALUES ($username, $payload_json, $computed_at_ms)
                                  ON CONFLICT(username) DO UPDATE SET
                                      payload_json = excluded.payload_json,
                                      computed_at_ms = excluded.computed_at_ms;
                                  """;
                cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$payload_json", json);
                cmd.Parameters.AddWithValue("$computed_at_ms", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                cmd.ExecuteNonQuery();
            }
        }

        public void ClearInsightsCache()
        {
            lock (sync)
            {
                ensureInitialised();
                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                // WHERE TRUE keeps the intentional full clear explicit (a bare DELETE FROM would be read as an oversight).
                cmd.CommandText = "DELETE FROM insights_cache WHERE TRUE;";
                cmd.ExecuteNonQuery();
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
                                      username TEXT NOT NULL DEFAULT '',
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
                                      updated_at INTEGER NOT NULL,
                                      excluded INTEGER NOT NULL DEFAULT 0
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
                                      date_ms INTEGER NOT NULL,
                                      bpm REAL NOT NULL DEFAULT 0,
                                      key_count INTEGER NOT NULL DEFAULT 0,
                                      is_convert INTEGER NOT NULL DEFAULT 0,
                                      rate REAL NOT NULL DEFAULT 1,
                                      mod_acronyms_json TEXT NOT NULL DEFAULT '[]'
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_drill_scores_ruleset_pp
                                      ON drill_scores(ruleset_id, pp_resolved DESC, date_ms DESC);
                                  CREATE INDEX IF NOT EXISTS idx_drill_scores_user
                                      ON drill_scores(username, score_id);
                                  CREATE TABLE IF NOT EXISTS dan_clear_evidence (
                                      id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      username TEXT NOT NULL,
                                      key_count INTEGER NOT NULL,
                                      side TEXT NOT NULL,
                                      beatmap_hash TEXT NOT NULL,
                                      rate REAL NOT NULL,
                                      credited_dan REAL NOT NULL,
                                      accuracy REAL NOT NULL,
                                      scored_at_ms INTEGER NOT NULL,
                                      algorithm_version INTEGER NOT NULL DEFAULT 1
                                  );
                                  CREATE TABLE IF NOT EXISTS axis_play_evidence (
                                      id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      username TEXT NOT NULL,
                                      key_count INTEGER NOT NULL,
                                      skill_id TEXT NOT NULL,
                                      beatmap_hash TEXT NOT NULL,
                                      axis_value REAL NOT NULL,
                                      accuracy REAL NOT NULL,
                                      rate REAL NOT NULL,
                                      scored_at_ms INTEGER NOT NULL,
                                      algorithm_version INTEGER NOT NULL DEFAULT 1
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_dan_clear_evidence_user
                                      ON dan_clear_evidence(username, key_count, side, credited_dan DESC);
                                  CREATE INDEX IF NOT EXISTS idx_axis_play_evidence_user
                                      ON axis_play_evidence(username, key_count, skill_id, axis_value DESC);
                                  CREATE TABLE IF NOT EXISTS ssr_score_cache (
                                      score_id TEXT PRIMARY KEY NOT NULL,
                                      username TEXT NOT NULL,
                                      beatmap_hash TEXT NOT NULL,
                                      key_count INTEGER NOT NULL,
                                      rate REAL NOT NULL,
                                      scored_at_ms INTEGER NOT NULL,
                                      has_ssr INTEGER NOT NULL,
                                      vector_json TEXT NOT NULL,
                                      algorithm_version INTEGER NOT NULL
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_ssr_score_cache_user
                                      ON ssr_score_cache(username, algorithm_version);
                                  CREATE TABLE IF NOT EXISTS dan_score_cache (
                                      score_id TEXT PRIMARY KEY NOT NULL,
                                      username TEXT NOT NULL,
                                      beatmap_hash TEXT NOT NULL,
                                      credited INTEGER NOT NULL,
                                      key_count INTEGER NOT NULL,
                                      side TEXT NOT NULL,
                                      rate REAL NOT NULL,
                                      credited_dan REAL NOT NULL,
                                      accuracy REAL NOT NULL,
                                      scored_at_ms INTEGER NOT NULL,
                                      algorithm_version INTEGER NOT NULL
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_dan_score_cache_user
                                      ON dan_score_cache(username, algorithm_version);
                                  """;
                cmd.ExecuteNonQuery();
            }

            ensureColumn(connection, "ruleset_stats", "total_pp", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "ruleset_stats", "total_duration_ms", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "mania_key_stats", "total_pp", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "mania_key_stats", "total_duration_ms", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "online_score_contributions", "pp", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "online_score_contributions", "duration_ms", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "online_score_contributions", "username", "TEXT NOT NULL DEFAULT ''");
            ensureColumn(connection, "username_partitions", "excluded", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "dan_clear_evidence", "algorithm_version", "INTEGER NOT NULL DEFAULT 1");
            ensureColumn(connection, "axis_play_evidence", "algorithm_version", "INTEGER NOT NULL DEFAULT 1");
            ensureColumn(connection, "drill_scores", "bpm", "REAL NOT NULL DEFAULT 0");
            ensureColumn(connection, "drill_scores", "key_count", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "drill_scores", "is_convert", "INTEGER NOT NULL DEFAULT 0");
            ensureColumn(connection, "drill_scores", "rate", "REAL NOT NULL DEFAULT 1");
            ensureColumn(connection, "drill_scores", "mod_acronyms_json", "TEXT NOT NULL DEFAULT '[]'");

            // Indexes over columns that a pre-existing file does not have must wait for ensureColumn above.
            // CREATE TABLE IF NOT EXISTS does not extend an existing table, so an index in the batch would run
            // against a table that is still missing the column and fail with "no such column".
            using (var lateIndexes = connection.CreateCommand())
            {
                lateIndexes.CommandText = """
                                          CREATE INDEX IF NOT EXISTS idx_online_score_contributions_user
                                              ON online_score_contributions(username);
                                          """;
                lateIndexes.ExecuteNonQuery();
            }

            using (var insightsTable = connection.CreateCommand())
            {
                insightsTable.CommandText = """
                                            CREATE TABLE IF NOT EXISTS insights_cache (
                                                username TEXT PRIMARY KEY NOT NULL,
                                                payload_json TEXT NOT NULL,
                                                computed_at_ms INTEGER NOT NULL
                                            );
                                            """;
                insightsTable.ExecuteNonQuery();
            }

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
        /// <param name="includePlayerEvidence">
        /// When true, also clear the per-player evidence tables, which a full skills pass then repopulates.
        /// An exclusion passes false so other players keep their evidence; nothing but the excluded flag changes
        /// for the player being fenced.
        /// </param>
        private static void clearTables(SqliteConnection connection, bool includePlayerEvidence = true)
        {
            // drill_scores is deliberately absent: it is the detail source of truth (the partition payload no longer
            // carries a copy), so a rebuild of the aggregate tables must not wipe it. ReplaceAll clears it explicitly.
            string sql = """
                         DELETE FROM ruleset_stats WHERE TRUE;
                         DELETE FROM mania_key_stats WHERE TRUE;
                         DELETE FROM mania_column_stats WHERE TRUE;
                         DELETE FROM grade_counts WHERE TRUE;
                         DELETE FROM star_play_counts WHERE TRUE;
                         DELETE FROM xxy_play_counts WHERE TRUE;
                         DELETE FROM std_attr_affinity WHERE TRUE;
                         DELETE FROM insights_cache WHERE TRUE;
                         """;

            if (includePlayerEvidence)
            {
                sql += """
                       DELETE FROM dan_clear_evidence WHERE TRUE;
                       DELETE FROM axis_play_evidence WHERE TRUE;
                       """;
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
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

        private static bool readNeedsRecompute(SqliteConnection connection)
        {
            string? raw = tryGetMeta(connection, meta_content_version);
            if (string.IsNullOrEmpty(raw)
                || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int stored)
                || stored < CONTENT_VERSION)
                return true;

            return false;
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
