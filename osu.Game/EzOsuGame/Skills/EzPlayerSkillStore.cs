// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.Pattern;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player aggregate values: one row per player slice and axis, stored next to the score ledger they are folded from.
    /// </summary>
    /// <remarks>
    /// These rows are written only by the manual score analysis, so they are not a cache the game can rebuild on a read
    /// path. Keeping them in the ledger's database is what lets a recompute replace them in the same transaction.
    /// </remarks>
    public sealed class EzPlayerSkillStore
    {
        private readonly EzLocalProfileDatabase database;

        /// <summary>
        /// The store installed by the host, used when a caller does not pass one explicitly.
        /// </summary>
        public static EzPlayerSkillStore? Current { get; private set; }

        public static void Configure(EzPlayerSkillStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            Current = store;
        }

        public EzPlayerSkillStore(EzLocalProfileDatabase database)
        {
            ArgumentNullException.ThrowIfNull(database);
            this.database = database;
        }

        /// <summary>Owns a database over <paramref name="storage"/>; tests and tools only.</summary>
        public EzPlayerSkillStore(Storage storage)
            : this(new EzLocalProfileDatabase(storage))
        {
        }

        public readonly record struct PlayerSystemSkillSlice(IReadOnlyDictionary<string, double> Values, int AnalyzedPlays, bool Provisional, bool Stale);

        public IReadOnlyDictionary<string, double> GetPlayerSkills(string username, int keyCount, string systemId, int? algorithmVersion = null, int rulesetOnlineId = -1)
        {
            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);

            return database.Read(c => readValues(c, new SliceQuery(username, keyCount, systemId, version, rulesetOnlineId)));
        }

        /// <summary>PPP player rows written at the bundle revision of <paramref name="rulesetOnlineId"/>.</summary>
        public IReadOnlyDictionary<string, double> GetPlayerSkillsAtCurrentVersions(string username, int keyCount, string systemId, int rulesetOnlineId)
            => GetPlayerSkills(username, keyCount, systemId, EzPPPSkillVersions.ForRuleset(rulesetOnlineId), rulesetOnlineId);

        /// <summary>Row metadata for a player system slice (analyzed plays / provisional / stale).</summary>
        public (int AnalyzedPlays, bool Provisional, bool Stale) GetPlayerSystemSkillMeta(
            string username,
            int keyCount,
            string systemId,
            int? algorithmVersion = null,
            int rulesetOnlineId = -1)
        {
            PlayerSystemSkillSlice slice = GetPlayerSystemSkillSlice(username, keyCount, systemId, algorithmVersion, rulesetOnlineId);
            return (slice.AnalyzedPlays, slice.Provisional, slice.Stale);
        }

        /// <summary>
        /// One player slice's values together with its metadata, in a single read.
        /// </summary>
        public PlayerSystemSkillSlice GetPlayerSystemSkillSlice(
            string username,
            int keyCount,
            string systemId,
            int? algorithmVersion = null,
            int rulesetOnlineId = -1)
        {
            int version = resolvePlayerVersion(systemId, algorithmVersion, rulesetOnlineId);

            return database.Read(c =>
            {
                var query = new SliceQuery(username, keyCount, systemId, version, rulesetOnlineId);
                var values = readValues(c, query);

                if (values.Count == 0)
                    return new PlayerSystemSkillSlice(values, 0, false, false);

                var (plays, provisional) = readMeta(c, query);
                return new PlayerSystemSkillSlice(values, plays, provisional, isStale(c, username, keyCount, rulesetOnlineId, plays));
            });
        }

        public EzPlayerRatingSnapshot GetPlayerSsrSnapshot(string username, int keyCount, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

            return database.Read(c =>
            {
                var query = new SliceQuery(username, keyCount, EzSkillSystems.PLAYER_MINA, version, EzLocalProfileConstants.MANIA_RULESET_ID);
                var values = readValues(c, query);

                if (values.Count == 0)
                    return new EzPlayerRatingSnapshot();

                var (plays, provisional) = readMeta(c, query);

                return new EzPlayerRatingSnapshot
                {
                    Values = values,
                    AnalyzedPlays = plays,
                    Provisional = provisional,
                    Stale = isStale(c, username, keyCount, EzLocalProfileConstants.MANIA_RULESET_ID, plays),
                    ComputedAt = readComputedAt(c, query),
                };
            });
        }

        public IReadOnlyList<int> GetPlayerSsrKeyCounts(string username, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

            return database.Read(c =>
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                                  SELECT DISTINCT key_count FROM player_skill_value
                                  WHERE username = $username AND system_id = $system AND algorithm_version = $version
                                  ORDER BY key_count;
                                  """;
                cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$system", EzSkillSystems.PLAYER_MINA);
                cmd.Parameters.AddWithValue("$version", version);

                var keys = new List<int>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                    keys.Add(reader.GetInt32(0));

                return (IReadOnlyList<int>)keys;
            });
        }

        public void WritePlayerSystemSkills(
            string username,
            int sliceKey,
            string systemId,
            IReadOnlyDictionary<string, double> skills,
            int analyzedPlays,
            bool provisional = false,
            DateTimeOffset? computedAt = null,
            int? algorithmVersion = null,
            int rulesetOnlineId = -1)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemId);
            ArgumentNullException.ThrowIfNull(skills);

            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;

            database.Write((c, tx) => replaceValues(c, tx, username, sliceKey, systemId, rulesetOnlineId,
                toRows(skills, analyzedPlays, provisional, version, at), version));
        }

        public void WritePlayerSsr(
            string username,
            int keyCount,
            EzMinaSkillAxisVector vector,
            int analyzedPlays,
            bool provisional = false,
            DateTimeOffset? computedAt = null,
            bool appendHistory = true)
        {
            int version = EzMinaSkillAlgorithm.VERSION;
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;

            database.Write((c, tx) =>
            {
                replaceValues(c, tx, username, keyCount, EzSkillSystems.PLAYER_MINA, EzLocalProfileConstants.MANIA_RULESET_ID,
                    toRows(ssrValues(vector), analyzedPlays, provisional, version, at), version);

                if (appendHistory)
                    insertHistory(c, tx, username, keyCount, ssrValues(vector), at, version);
            });
        }

        /// <summary>
        /// Write one player × keymode's SSR values, history samples and pattern ratings in a single transaction.
        /// </summary>
        public void WritePlayerSkillBundle(
            string username,
            int keyCount,
            EzMinaSkillAxisVector ssr,
            int analyzedPlays,
            bool provisional,
            IReadOnlyList<(DateTimeOffset RecordedAt, EzMinaSkillAxisVector Vector)> historySamples,
            IReadOnlyList<EzPatternRating> patternRatings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(historySamples);
            ArgumentNullException.ThrowIfNull(patternRatings);

            DateTimeOffset at = DateTimeOffset.UtcNow;
            int version = EzMinaSkillAlgorithm.VERSION;

            database.Write((c, tx) =>
            {
                replaceValues(c, tx, username, keyCount, EzSkillSystems.PLAYER_MINA, EzLocalProfileConstants.MANIA_RULESET_ID,
                    toRows(ssrValues(ssr), analyzedPlays, provisional, version, at), version);

                replaceHistory(c, tx, username, keyCount, historySamples, version);

                replaceValues(c, tx, username, keyCount, EzSkillSystems.PLAYER_PATTERN, -1,
                    toRows(patternValues(patternRatings), analyzedPlays, provisional, version, at), version);
            });
        }

        public IReadOnlyList<EzPatternRating> GetPlayerPatternRatings(string username, int keyCount, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

            return database.Read(c =>
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                                  SELECT skill_id, value, analyzed_plays FROM player_skill_value
                                  WHERE username = $username AND key_count = $key_count
                                    AND system_id = $system AND algorithm_version = $version
                                  ORDER BY value DESC;
                                  """;
                cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$key_count", keyCount);
                cmd.Parameters.AddWithValue("$system", EzSkillSystems.PLAYER_PATTERN);
                cmd.Parameters.AddWithValue("$version", version);

                var list = new List<EzPatternRating>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!EzPatternRatings.TryParseSkillId(reader.GetString(0), out string patternId))
                        continue;

                    list.Add(new EzPatternRating(patternId, reader.GetDouble(1), reader.GetInt32(2)));
                }

                return (IReadOnlyList<EzPatternRating>)list;
            });
        }

        public IReadOnlyList<EzPlayerSkillHistorySample> GetPlayerSkillHistory(string username, int keyCount, string skillId, int maxPoints = 64)
            => database.Read(c => readHistory(c, username, keyCount, skillId, maxPoints));

        public void ReplacePlayerSkillHistory(string username, int keyCount, IReadOnlyList<(DateTimeOffset RecordedAt, EzMinaSkillAxisVector Vector)> samples)
        {
            int version = EzMinaSkillAlgorithm.VERSION;

            database.Write((c, tx) => replaceHistory(c, tx, username, keyCount, samples, version));
        }

        public EzPlayerDanEstimateRow? GetDanEstimate(string username, int keyCount, string side, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            return database.Read(c =>
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                                  SELECT raw_dan, label, clears, beyond_table, course_name, course_accuracy,
                                         clear_window_have, clear_window_need, computed_at_ms
                                  FROM player_dan_estimate
                                  WHERE username = $username AND key_count = $key_count AND side = $side
                                    AND algorithm_version = $version;
                                  """;
                cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$key_count", keyCount);
                cmd.Parameters.AddWithValue("$side", side);
                cmd.Parameters.AddWithValue("$version", version);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return null;

                return (EzPlayerDanEstimateRow?)new EzPlayerDanEstimateRow(
                    username,
                    keyCount,
                    side,
                    reader.GetDouble(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3) != 0,
                    reader.GetString(4),
                    reader.GetDouble(5),
                    reader.GetInt32(6),
                    reader.GetInt32(7),
                    version,
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)));
            });
        }

        public void WriteDanEstimate(EzPlayerDanEstimateRow estimate)
            => database.Write((c, tx) => writeDanEstimate(c, tx, estimate));

        public bool HasDanSkillsetCache(string username, int keyCount, string side, int? algorithmVersion = null)
            => GetDanSkillsetValues(username, keyCount, side, algorithmVersion).Count > 0;

        public IReadOnlyList<EzPlayerDanSkillsetRow> GetDanSkillsetValues(string username, int keyCount, string side, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            return database.Read(c =>
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                                  SELECT skillset_id, raw_dan, label, clears, computed_at_ms
                                  FROM player_dan_skillset
                                  WHERE username = $username AND key_count = $key_count AND side = $side
                                    AND algorithm_version = $version
                                  ORDER BY raw_dan DESC;
                                  """;
                cmd.Parameters.AddWithValue("$username", username);
                cmd.Parameters.AddWithValue("$key_count", keyCount);
                cmd.Parameters.AddWithValue("$side", side);
                cmd.Parameters.AddWithValue("$version", version);

                var list = new List<EzPlayerDanSkillsetRow>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new EzPlayerDanSkillsetRow(
                        side,
                        reader.GetString(0),
                        reader.GetDouble(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        version,
                        DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))));
                }

                return (IReadOnlyList<EzPlayerDanSkillsetRow>)list;
            });
        }

        public void WriteDanSkillsetValues(string username, int keyCount, string side, IEnumerable<EzPlayerDanSkillsetRow> values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(values);

            var list = new List<EzPlayerDanSkillsetRow>(values);

            database.Write((c, tx) =>
            {
                deleteRows(c, tx, "player_dan_skillset",
                    "username = $username AND key_count = $key_count AND side = $side",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$username", username);
                        cmd.Parameters.AddWithValue("$key_count", keyCount);
                        cmd.Parameters.AddWithValue("$side", side);
                    });

                foreach (var value in list)
                {
                    using var ins = c.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = """
                                      INSERT OR REPLACE INTO player_dan_skillset
                                          (username, key_count, side, skillset_id, raw_dan, label, clears, algorithm_version, computed_at_ms)
                                      VALUES
                                          ($username, $key_count, $side, $skillset_id, $raw_dan, $label, $clears, $version, $computed_at);
                                      """;
                    ins.Parameters.AddWithValue("$username", username);
                    ins.Parameters.AddWithValue("$key_count", keyCount);
                    ins.Parameters.AddWithValue("$side", side);
                    ins.Parameters.AddWithValue("$skillset_id", value.SkillsetId);
                    ins.Parameters.AddWithValue("$raw_dan", value.RawDan);
                    ins.Parameters.AddWithValue("$label", value.Label);
                    ins.Parameters.AddWithValue("$clears", value.Clears);
                    ins.Parameters.AddWithValue("$version", value.AlgorithmVersion);
                    ins.Parameters.AddWithValue("$computed_at", value.ComputedAt.ToUnixTimeMilliseconds());
                    ins.ExecuteNonQuery();
                }
            });
        }

        /// <summary>Writes the skillset-dan tiles a dan refresh produced, dropping any row it no longer covers.</summary>
        public void WriteDanSkillsetVerdicts(string username, int keyCount, string side, IReadOnlyDictionary<string, EzDanSkillsetVerdict> verdicts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(verdicts);

            DateTimeOffset at = DateTimeOffset.UtcNow;
            var values = new List<EzPlayerDanSkillsetRow>(verdicts.Count);

            foreach (var (skillsetId, verdict) in verdicts)
            {
                values.Add(new EzPlayerDanSkillsetRow(
                    side,
                    skillsetId,
                    verdict.RawDan,
                    verdict.Label,
                    verdict.Clears,
                    EzDanAlgorithm.VERSION,
                    at));
            }

            WriteDanSkillsetValues(username, keyCount, side, values);
        }

        public void ClearPlayerSystemSkills(string username, string systemId)            => database.Write((c, tx) => deleteRows(c, tx, "player_skill_value",
                "username = $username AND system_id = $system",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$username", username);
                    cmd.Parameters.AddWithValue("$system", systemId);
                }));

        public void ClearDanSkillsetValues(string username)
            => database.Write((c, tx) => deleteRows(c, tx, "player_dan_skillset", "username = $username",
                cmd => cmd.Parameters.AddWithValue("$username", username)));

        public void ClearDanEstimates(string username)
            => database.Write((c, tx) => deleteRows(c, tx, "player_dan_estimate", "username = $username",
                cmd => cmd.Parameters.AddWithValue("$username", username)));

        public int DeletePlayerSkillData(string username)
            => database.Read(c =>
            {
                int removed = 0;

                foreach (string table in new[] { "player_skill_value", "player_skill_history", "player_dan_estimate", "player_dan_skillset" })
                {
                    using var cmd = c.CreateCommand();
                    cmd.CommandText = $"DELETE FROM {table} WHERE username = $username;";
                    cmd.Parameters.AddWithValue("$username", username);
                    removed += cmd.ExecuteNonQuery();
                }

                return removed;
            });

        /// <summary>
        /// Players whose stored rows were written before their ledger moved on.
        /// </summary>
        /// <remarks>
        /// Derived from the ledger instead of a stored flag: a row records how many plays it folded, so comparing that
        /// with the ledger's current count is the whole test, and it cannot outlive its cause.
        /// </remarks>
        public IReadOnlyList<string> GetStalePlayerSkillUsernames()
            => database.Read(c =>
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                                  SELECT DISTINCT v.username
                                  FROM player_skill_value v
                                  WHERE v.analyzed_plays < (
                                      SELECT COUNT(*) FROM drill_scores d
                                      WHERE d.username = v.username
                                        AND d.ruleset_id = v.ruleset_online_id)
                                  ORDER BY v.username;
                                  """;

                var names = new List<string>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                    names.Add(reader.GetString(0));

                return (IReadOnlyList<string>)names;
            });

        public IReadOnlyList<EzStalePlayerSkill> GetStalePlayerSkillDetails()
            => database.Read(c =>
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                                  SELECT username, COUNT(*), MAX(computed_at_ms)
                                  FROM player_skill_value
                                  WHERE analyzed_plays < (
                                      SELECT COUNT(*) FROM drill_scores d
                                      WHERE d.username = player_skill_value.username
                                        AND d.ruleset_id = player_skill_value.ruleset_online_id)
                                  GROUP BY username
                                  ORDER BY username;
                                  """;

                var list = new List<EzStalePlayerSkill>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new EzStalePlayerSkill(
                        reader.GetString(0),
                        reader.GetInt32(1),
                        DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2))));
                }

                return (IReadOnlyList<EzStalePlayerSkill>)list;
            });

        /// <summary>No stored flag remains; staleness is derived from the ledger.</summary>
        public int SetPlayerSkillStale(string username, bool stale) => 0;

        private readonly record struct SliceQuery(string Username, int KeyCount, string SystemId, int Version, int RulesetOnlineId);

        private static IReadOnlyDictionary<string, double> readValues(SqliteConnection connection, in SliceQuery query)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                               SELECT skill_id, value FROM player_skill_value
                               WHERE username = $username AND key_count = $key_count
                                 AND system_id = $system AND algorithm_version = $version
                                 {(query.RulesetOnlineId >= 0 ? "AND ruleset_online_id = $ruleset" : string.Empty)};
                               """;
            bindSlice(cmd, query);

            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                values[reader.GetString(0)] = reader.GetDouble(1);

            return values;
        }

        private static (int Plays, bool Provisional) readMeta(SqliteConnection connection, in SliceQuery query)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                               SELECT analyzed_plays, provisional FROM player_skill_value
                               WHERE username = $username AND key_count = $key_count
                                 AND system_id = $system AND algorithm_version = $version
                                 {(query.RulesetOnlineId >= 0 ? "AND ruleset_online_id = $ruleset" : string.Empty)}
                               LIMIT 1;
                               """;
            bindSlice(cmd, query);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? (reader.GetInt32(0), reader.GetInt32(1) != 0) : (0, false);
        }

        private static DateTimeOffset? readComputedAt(SqliteConnection connection, in SliceQuery query)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                               SELECT computed_at_ms FROM player_skill_value
                               WHERE username = $username AND key_count = $key_count
                                 AND system_id = $system AND algorithm_version = $version
                                 {(query.RulesetOnlineId >= 0 ? "AND ruleset_online_id = $ruleset" : string.Empty)}
                               LIMIT 1;
                               """;
            bindSlice(cmd, query);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)) : null;
        }

        /// <summary>
        /// Whether the ledger has moved past what these rows folded. A slice with no rows is not stale: there is
        /// nothing on screen to be behind.
        /// </summary>
        private static bool isStale(SqliteConnection connection, string username, int keyCount, int rulesetOnlineId, int analyzedPlays)
        {
            if (analyzedPlays <= 0)
                return false;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              SELECT COUNT(*) FROM drill_scores
                              WHERE username = $username AND ruleset_id = $ruleset AND key_count = $key_count;
                              """;
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$ruleset", rulesetOnlineId < 0 ? EzLocalProfileConstants.MANIA_RULESET_ID : rulesetOnlineId);
            cmd.Parameters.AddWithValue("$key_count", keyCount);

            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > analyzedPlays;
        }

        private static void bindSlice(SqliteCommand cmd, in SliceQuery query)
        {
            cmd.Parameters.AddWithValue("$username", query.Username);
            cmd.Parameters.AddWithValue("$key_count", query.KeyCount);
            cmd.Parameters.AddWithValue("$system", query.SystemId);
            cmd.Parameters.AddWithValue("$version", query.Version);

            if (query.RulesetOnlineId >= 0)
                cmd.Parameters.AddWithValue("$ruleset", query.RulesetOnlineId);
        }

        private static void replaceValues(SqliteConnection connection, SqliteTransaction transaction, string username, int keyCount,
                                          string systemId, int rulesetOnlineId, IReadOnlyList<PlayerRow> rows, int version)
        {
            deleteRows(connection, transaction, "player_skill_value",
                "username = $username AND key_count = $key_count AND system_id = $system AND ruleset_online_id = $ruleset AND algorithm_version = $version",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$username", username);
                    cmd.Parameters.AddWithValue("$key_count", keyCount);
                    cmd.Parameters.AddWithValue("$system", systemId);
                    cmd.Parameters.AddWithValue("$ruleset", rulesetOnlineId);
                    cmd.Parameters.AddWithValue("$version", version);
                });

            foreach (var row in rows)
            {
                using var ins = connection.CreateCommand();
                ins.Transaction = transaction;
                ins.CommandText = """
                                  INSERT OR REPLACE INTO player_skill_value
                                      (username, key_count, system_id, ruleset_online_id, skill_id, value, state,
                                       analyzed_plays, provisional, algorithm_version, computed_at_ms)
                                  VALUES
                                      ($username, $key_count, $system, $ruleset, $skill_id, $value, $state,
                                       $analyzed_plays, $provisional, $version, $computed_at);
                                  """;
                ins.Parameters.AddWithValue("$username", username);
                ins.Parameters.AddWithValue("$key_count", keyCount);
                ins.Parameters.AddWithValue("$system", systemId);
                ins.Parameters.AddWithValue("$ruleset", rulesetOnlineId);
                ins.Parameters.AddWithValue("$skill_id", row.SkillId);
                ins.Parameters.AddWithValue("$value", row.Value);
                ins.Parameters.AddWithValue("$state", (int)EzMetricState.Current);
                ins.Parameters.AddWithValue("$analyzed_plays", row.AnalyzedPlays);
                ins.Parameters.AddWithValue("$provisional", row.Provisional ? 1 : 0);
                ins.Parameters.AddWithValue("$version", version);
                ins.Parameters.AddWithValue("$computed_at", row.ComputedAt.ToUnixTimeMilliseconds());
                ins.ExecuteNonQuery();
            }
        }

        private static void insertHistory(SqliteConnection connection, SqliteTransaction transaction, string username, int keyCount,
                                          IReadOnlyDictionary<string, double> values, DateTimeOffset at, int version)
        {
            foreach (var (skillId, value) in values)
            {
                if (value <= 0)
                    continue;

                using var ins = connection.CreateCommand();
                ins.Transaction = transaction;
                ins.CommandText = """
                                  INSERT INTO player_skill_history (username, key_count, skill_id, value, recorded_at_ms, algorithm_version)
                                  VALUES ($username, $key_count, $skill_id, $value, $recorded_at, $version);
                                  """;
                ins.Parameters.AddWithValue("$username", username);
                ins.Parameters.AddWithValue("$key_count", keyCount);
                ins.Parameters.AddWithValue("$skill_id", skillId);
                ins.Parameters.AddWithValue("$value", value);
                ins.Parameters.AddWithValue("$recorded_at", at.ToUnixTimeMilliseconds());
                ins.Parameters.AddWithValue("$version", version);
                ins.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<EzPlayerSkillHistorySample> readHistory(SqliteConnection connection, string username, int keyCount, string skillId, int maxPoints)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              SELECT value, recorded_at_ms, algorithm_version FROM player_skill_history
                              WHERE username = $username AND key_count = $key_count AND skill_id = $skill_id
                              ORDER BY recorded_at_ms DESC
                              LIMIT $limit;
                              """;
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$key_count", keyCount);
            cmd.Parameters.AddWithValue("$skill_id", skillId);
            cmd.Parameters.AddWithValue("$limit", maxPoints);

            var points = new List<EzPlayerSkillHistorySample>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                points.Add(new EzPlayerSkillHistorySample(
                    skillId,
                    reader.GetDouble(0),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1))));
            }

            points.Reverse();
            return points;
        }

        private static void replaceHistory(SqliteConnection connection, SqliteTransaction transaction, string username, int keyCount,
                                           IReadOnlyList<(DateTimeOffset RecordedAt, EzMinaSkillAxisVector Vector)> samples, int version)
        {
            deleteRows(connection, transaction, "player_skill_history", "username = $username AND key_count = $key_count",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$username", username);
                    cmd.Parameters.AddWithValue("$key_count", keyCount);
                });

            foreach (var (recordedAt, vector) in samples)
                insertHistory(connection, transaction, username, keyCount, ssrValues(vector), recordedAt, version);
        }

        private static void writeDanEstimate(SqliteConnection connection, SqliteTransaction transaction, EzPlayerDanEstimateRow estimate)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                              INSERT OR REPLACE INTO player_dan_estimate
                                  (username, key_count, side, raw_dan, label, clears, beyond_table, course_name,
                                   course_accuracy, clear_window_have, clear_window_need, algorithm_version, computed_at_ms)
                              VALUES
                                  ($username, $key_count, $side, $raw_dan, $label, $clears, $beyond_table, $course_name,
                                   $course_accuracy, $clear_window_have, $clear_window_need, $version, $computed_at);
                              """;
            cmd.Parameters.AddWithValue("$username", estimate.Username);
            cmd.Parameters.AddWithValue("$key_count", estimate.KeyCount);
            cmd.Parameters.AddWithValue("$side", estimate.Side);
            cmd.Parameters.AddWithValue("$raw_dan", estimate.RawDan);
            cmd.Parameters.AddWithValue("$label", estimate.Label);
            cmd.Parameters.AddWithValue("$clears", estimate.Clears);
            cmd.Parameters.AddWithValue("$beyond_table", estimate.BeyondTable ? 1 : 0);
            cmd.Parameters.AddWithValue("$course_name", estimate.CourseName);
            cmd.Parameters.AddWithValue("$course_accuracy", estimate.CourseAccuracy);
            cmd.Parameters.AddWithValue("$clear_window_have", estimate.ClearWindowHave);
            cmd.Parameters.AddWithValue("$clear_window_need", estimate.ClearWindowNeed);
            cmd.Parameters.AddWithValue("$version", estimate.AlgorithmVersion);
            cmd.Parameters.AddWithValue("$computed_at", estimate.ComputedAt.ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
        }

        private static void deleteRows(SqliteConnection connection, SqliteTransaction transaction, string table, string where, Action<SqliteCommand> bind)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"DELETE FROM {table} WHERE {where};";
            bind(cmd);
            cmd.ExecuteNonQuery();
        }

        private readonly record struct PlayerRow(string SkillId, double Value, int AnalyzedPlays, bool Provisional, DateTimeOffset ComputedAt);

        private static IReadOnlyList<PlayerRow> toRows(IReadOnlyDictionary<string, double> values, int analyzedPlays, bool provisional, int version, DateTimeOffset at)
        {
            var rows = new List<PlayerRow>(values.Count);

            foreach (var (skillId, value) in values)
                rows.Add(new PlayerRow(skillId, value, analyzedPlays, provisional, at));

            return rows;
        }

        private static IReadOnlyDictionary<string, double> ssrValues(EzMinaSkillAxisVector vector)
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                values[axis.ToSsrSkillId()] = value;

            return values;
        }

        private static IReadOnlyDictionary<string, double> patternValues(IReadOnlyList<EzPatternRating> ratings)
        {
            var values = new Dictionary<string, double>(ratings.Count, StringComparer.Ordinal);

            foreach (var rating in ratings)
                values[EzPatternRatings.ToSkillId(rating.Id)] = rating.Rating;

            return values;
        }

        private static int resolvePlayerVersion(string systemId, int? algorithmVersion, int rulesetOnlineId)
        {
            if (algorithmVersion is int explicitVersion)
                return explicitVersion;

            if (rulesetOnlineId >= 0 && systemId is EzSkillSystems.BEATMAP_PPP or EzSkillSystems.PLAYER_PPP)
                return EzPPPSkillVersions.ForRuleset(rulesetOnlineId);

            return EzSkillSystems.ResolveAlgorithmVersion(systemId);
        }
    }
}
