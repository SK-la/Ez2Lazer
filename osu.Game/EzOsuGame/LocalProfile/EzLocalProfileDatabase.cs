// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using Microsoft.Data.Sqlite;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Connection, schema and lifetime of the local-profile database file.
    /// </summary>
    /// <remarks>
    /// Shared by the feature stores that keep their rows in this file, so there is one connection policy and one
    /// schema upgrade path rather than one per feature. Each feature only declares the tables it owns.
    /// </remarks>
    public sealed class EzLocalProfileDatabase : IDisposable
    {
        public const string DATABASE_FILENAME = EzLocalProfileStore.DATABASE_FILENAME;

        private readonly Storage storage;
        private bool initialised;
        private string dbPath = string.Empty;
        private bool isDisposed;

        public EzLocalProfileDatabase(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>Runs <paramref name="action"/> under the store's lock with an open connection.</summary>
        public T Read<T>(Func<SqliteConnection, T> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (Sync)
            {
                EnsureInitialised();
                using var connection = Open();
                return action(connection);
            }
        }

        /// <summary>Lock shared with every other feature store over this file.</summary>
        public Lock Sync { get; } = new Lock();

        public void EnsureInitialised()
        {
            if (initialised)
                return;

            dbPath = storage.GetFullPath(DATABASE_FILENAME, true);

            using var connection = Open();
            EzLocalProfileStore.EnsureSchema(connection);
            EzPlayerSkillSchema.Ensure(connection);
            initialised = true;
        }

        public SqliteConnection Open()
        {
            var connection = new SqliteConnection($"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        /// <summary>Runs <paramref name="action"/> under the store's lock, inside one transaction.</summary>
        public void Write(Action<SqliteConnection, SqliteTransaction> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (Sync)
            {
                EnsureInitialised();
                using var connection = Open();
                using var transaction = connection.BeginTransaction();
                action(connection, transaction);
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

        private void ensureInitialised() => EnsureInitialised();

        private SqliteConnection openConnection() => Open();
    }

    /// <summary>
    /// Player aggregate tables. They live here rather than in the realm file because their input is the score ledger
    /// in this same database, so a recompute and the rows it replaces commit together.
    /// </summary>
    internal static class EzPlayerSkillSchema
    {
        public static void Ensure(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();

            cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS player_skill_value (
                                  username TEXT NOT NULL,
                                  key_count INTEGER NOT NULL,
                                  system_id TEXT NOT NULL,
                                  ruleset_online_id INTEGER NOT NULL,
                                  skill_id TEXT NOT NULL,
                                  value REAL NOT NULL,
                                  state INTEGER NOT NULL,
                                  analyzed_plays INTEGER NOT NULL,
                                  provisional INTEGER NOT NULL,
                                  algorithm_version INTEGER NOT NULL,
                                  computed_at_ms INTEGER NOT NULL,
                                  PRIMARY KEY (username, key_count, system_id, ruleset_online_id, skill_id, algorithm_version)
                              );
                              CREATE INDEX IF NOT EXISTS idx_player_skill_value_user
                                  ON player_skill_value(username, system_id, ruleset_online_id);
                              CREATE TABLE IF NOT EXISTS player_skill_history (
                                  username TEXT NOT NULL,
                                  key_count INTEGER NOT NULL,
                                  skill_id TEXT NOT NULL,
                                  value REAL NOT NULL,
                                  recorded_at_ms INTEGER NOT NULL,
                                  algorithm_version INTEGER NOT NULL
                              );
                              CREATE INDEX IF NOT EXISTS idx_player_skill_history_user
                                  ON player_skill_history(username, key_count, skill_id, recorded_at_ms);
                              CREATE TABLE IF NOT EXISTS player_dan_estimate (
                                  username TEXT NOT NULL,
                                  key_count INTEGER NOT NULL,
                                  side TEXT NOT NULL,
                                  raw_dan REAL NOT NULL,
                                  label TEXT NOT NULL,
                                  clears INTEGER NOT NULL,
                                  beyond_table INTEGER NOT NULL,
                                  course_name TEXT NOT NULL,
                                  course_accuracy REAL NOT NULL,
                                  clear_window_have INTEGER NOT NULL,
                                  clear_window_need INTEGER NOT NULL,
                                  algorithm_version INTEGER NOT NULL,
                                  computed_at_ms INTEGER NOT NULL,
                                  PRIMARY KEY (username, key_count, side, algorithm_version)
                              );
                              CREATE TABLE IF NOT EXISTS player_dan_skillset (
                                  username TEXT NOT NULL,
                                  key_count INTEGER NOT NULL,
                                  side TEXT NOT NULL,
                                  skillset_id TEXT NOT NULL,
                                  raw_dan REAL NOT NULL,
                                  label TEXT NOT NULL,
                                  clears INTEGER NOT NULL,
                                  algorithm_version INTEGER NOT NULL,
                                  computed_at_ms INTEGER NOT NULL,
                                  PRIMARY KEY (username, key_count, side, skillset_id, algorithm_version)
                              );
                              """;

            cmd.ExecuteNonQuery();
        }
    }
}
