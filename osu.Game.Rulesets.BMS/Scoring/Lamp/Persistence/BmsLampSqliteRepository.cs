// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IBmsLampRepository"/>.
    /// Modelled after <c>osu.Game.EzOsuGame.Analysis.EzAnalysisPersistentStore</c>: every
    /// operation opens a fresh <see cref="SqliteConnection"/> (with shared cache + connection
    /// pooling) to avoid cross-thread reuse hazards, and the database is configured WAL +
    /// synchronous=NORMAL on first creation for high write throughput / cheap reader churn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Schema migration is intentional and self-contained — the file lives outside osu.Game's
    /// Realm and is keyed only by <c>BeatmapInfo.ID</c>, so loss of this file at most degrades
    /// to "no lamps" without breaking the main library. Versions are tracked in a tiny
    /// <c>meta</c> table; bumping <see cref="schema_version"/> recreates the lamp table from
    /// scratch (cheap — typical row count fits in low five digits even for a saturated library).
    /// </para>
    /// <para>
    /// Threading: SQLite handles intra-process concurrency for us via shared cache + WAL, so
    /// the only invariant this class enforces is "every command runs in a using block". Callers
    /// may invoke <see cref="Upsert"/> from arbitrary threads; <see cref="LoadAll"/> is meant
    /// for one synchronous call during store attach.
    /// </para>
    /// </remarks>
    public sealed class BmsLampSqliteRepository : IBmsLampRepository
    {
        // Bump this when the lamp table layout changes in a way that LoadAll cannot tolerate.
        // The migration policy is "drop and rebuild" — lamp history is regenerable from future
        // replays / external imports, so a hard reset is safer than partial-row migration code.
        private const int schema_version = 1;

        private const string table_lamp = "bms_lamp";
        private const string table_meta = "meta";

        private const string col_beatmap_id = "beatmap_id";
        private const string col_lamp = "lamp";
        private const string col_miss = "miss";
        private const string col_great = "great";
        private const string col_good = "good";
        private const string col_bad = "bad";
        private const string col_pgreat = "pgreat";
        private const string col_total = "total";
        private const string col_updated_at = "updated_at";

        private readonly string databasePath;
        private bool initialized;
        private bool disposed;

        public BmsLampSqliteRepository(string databasePath)
        {
            this.databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

            // Defer all schema work to first use so construction itself cannot throw — keeps the
            // attach call site at song-select free of try/catch noise. Init is idempotent.
            try
            {
                ensureInitialized();
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Lamp store init failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }
        }

        public IReadOnlyCollection<BmsLampRecord> LoadAll()
        {
            if (disposed)
                return Array.Empty<BmsLampRecord>();

            var rows = new List<BmsLampRecord>();

            try
            {
                ensureInitialized();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
SELECT {col_beatmap_id}, {col_lamp}, {col_miss}, {col_great}, {col_good}, {col_bad}, {col_pgreat}, {col_total}, {col_updated_at}
FROM {table_lamp};";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!Guid.TryParse(reader.GetString(0), out var id))
                        continue;

                    var lamp = (BmsClearLamp)reader.GetInt32(1);

                    rows.Add(new BmsLampRecord(
                        BeatmapId: id,
                        Lamp: lamp,
                        MissCount: reader.GetInt32(2),
                        GreatCount: reader.GetInt32(3),
                        GoodCount: reader.GetInt32(4),
                        BadCount: reader.GetInt32(5),
                        PerfectGreatCount: reader.GetInt32(6),
                        TotalNotes: reader.GetInt32(7),
                        UpdatedAtUnixMs: reader.GetInt64(8)));
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Lamp store LoadAll failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }

            return rows;
        }

        public void Upsert(BmsLampRecord record)
        {
            if (disposed)
                return;

            try
            {
                ensureInitialized();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
INSERT INTO {table_lamp} ({col_beatmap_id}, {col_lamp}, {col_miss}, {col_great}, {col_good}, {col_bad}, {col_pgreat}, {col_total}, {col_updated_at})
VALUES ($id, $lamp, $miss, $great, $good, $bad, $pgreat, $total, $updated_at)
ON CONFLICT({col_beatmap_id}) DO UPDATE SET
    {col_lamp}        = excluded.{col_lamp},
    {col_miss}        = excluded.{col_miss},
    {col_great}       = excluded.{col_great},
    {col_good}        = excluded.{col_good},
    {col_bad}         = excluded.{col_bad},
    {col_pgreat}      = excluded.{col_pgreat},
    {col_total}       = excluded.{col_total},
    {col_updated_at}  = excluded.{col_updated_at};";

                cmd.Parameters.AddWithValue("$id", record.BeatmapId.ToString("D", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$lamp", (int)record.Lamp);
                cmd.Parameters.AddWithValue("$miss", record.MissCount);
                cmd.Parameters.AddWithValue("$great", record.GreatCount);
                cmd.Parameters.AddWithValue("$good", record.GoodCount);
                cmd.Parameters.AddWithValue("$bad", record.BadCount);
                cmd.Parameters.AddWithValue("$pgreat", record.PerfectGreatCount);
                cmd.Parameters.AddWithValue("$total", record.TotalNotes);
                cmd.Parameters.AddWithValue("$updated_at", record.UpdatedAtUnixMs);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Lamp store Upsert failed for {record.BeatmapId}: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }
        }

        public void Delete(Guid beatmapId)
        {
            if (disposed)
                return;

            try
            {
                ensureInitialized();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"DELETE FROM {table_lamp} WHERE {col_beatmap_id} = $id;";
                cmd.Parameters.AddWithValue("$id", beatmapId.ToString("D", CultureInfo.InvariantCulture));
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Lamp store Delete failed for {beatmapId}: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            // Best effort — Microsoft.Data.Sqlite pools connections per database path. Clearing
            // the pool ensures Windows lets the WAL/SHM sidecar files release their locks so the
            // user can move/delete the storage directory while osu is alive (test scenarios,
            // "reset library" flows).
            try
            {
                SqliteConnection.ClearAllPools();
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Lamp store dispose ClearAllPools failed: {ex.Message}", LoggingTarget.Database, LogLevel.Debug);
            }
        }

        private void ensureInitialized()
        {
            if (initialized)
                return;

            string? directory = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var connection = openConnection();

            using (var pragma = connection.CreateCommand())
            {
                // Performance configuration mirrors EzAnalysisPersistentStore:
                //   - WAL gives concurrent reads during writes (no big locks on the carousel side).
                //   - synchronous=NORMAL is the canonical "fast enough, durable enough" SQLite knob;
                //     in WAL mode it's still crash-safe across normal process termination.
                //   - temp_store=MEMORY avoids touching disk for transient sort/hash space.
                pragma.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;";
                pragma.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_meta} (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {table_lamp} (
    {col_beatmap_id}   TEXT PRIMARY KEY,
    {col_lamp}         INTEGER NOT NULL,
    {col_miss}         INTEGER NOT NULL DEFAULT 0,
    {col_great}        INTEGER NOT NULL DEFAULT 0,
    {col_good}         INTEGER NOT NULL DEFAULT 0,
    {col_bad}          INTEGER NOT NULL DEFAULT 0,
    {col_pgreat}       INTEGER NOT NULL DEFAULT 0,
    {col_total}        INTEGER NOT NULL DEFAULT 0,
    {col_updated_at}   INTEGER NOT NULL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            int existingVersion = readSchemaVersion(connection);

            if (existingVersion != 0 && existingVersion != schema_version)
            {
                // Drop-and-rebuild policy: lamp data is fully regenerable from gameplay + future
                // beatoraja imports, so when the column set changes we prefer a clean table over
                // half-migrated rows that confuse the carousel.
                using var drop = connection.CreateCommand();
                drop.CommandText = $@"
DROP TABLE IF EXISTS {table_lamp};

CREATE TABLE {table_lamp} (
    {col_beatmap_id}   TEXT PRIMARY KEY,
    {col_lamp}         INTEGER NOT NULL,
    {col_miss}         INTEGER NOT NULL DEFAULT 0,
    {col_great}        INTEGER NOT NULL DEFAULT 0,
    {col_good}         INTEGER NOT NULL DEFAULT 0,
    {col_bad}          INTEGER NOT NULL DEFAULT 0,
    {col_pgreat}       INTEGER NOT NULL DEFAULT 0,
    {col_total}        INTEGER NOT NULL DEFAULT 0,
    {col_updated_at}   INTEGER NOT NULL DEFAULT 0
);";
                drop.ExecuteNonQuery();
                Logger.Log($"[BMS] Lamp store schema migrated from v{existingVersion} to v{schema_version} (rebuilt).", LoggingTarget.Database, LogLevel.Important);
            }

            writeSchemaVersion(connection, schema_version);

            initialized = true;
        }

        private SqliteConnection openConnection()
        {
            // Mode=ReadWriteCreate so a missing file is created in place; Cache=Shared lets multiple
            // short-lived connections from different threads share the page cache (cheaper reads).
            // Connection pooling is on by default in Microsoft.Data.Sqlite — the actual native
            // handle is reused across these `using` blocks.
            var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        private static int readSchemaVersion(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT value FROM {table_meta} WHERE key = 'schema_version';";
            object? result = cmd.ExecuteScalar();

            if (result is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;

            return 0;
        }

        private static void writeSchemaVersion(SqliteConnection connection, int version)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
INSERT INTO {table_meta} (key, value)
VALUES ('schema_version', $v)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            cmd.Parameters.AddWithValue("$v", version.ToString(CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }
    }
}
