// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 统一维护主分析库的 schema 与 meta 读写。
    /// </summary>
    internal static class EzAnalysisSchemaManager
    {
        public const int ANALYSIS_VERSION = EzAnalysisPersistentStore.ANALYSIS_VERSION;
        public const int MAIN_SCHEMA_VERSION = 3;
        public const string MAIN_DATABASE_KIND = "ez_analysis";

        public const string TABLE_ENTRY = "ez_analysis_entry";
        public const string TABLE_MANIA = "ez_analysis_mania";

        public const string COL_BEATMAP_ID = "beatmap_id";
        public const string COL_BEATMAP_HASH = "beatmap_hash";
        public const string COL_BEATMAP_MD5 = "beatmap_md5";
        public const string COL_RULESET_ONLINE_ID = "ruleset_online_id";
        public const string COL_COMMON_UPDATED_AT = "common_updated_at";
        public const string COL_AVERAGE_KPS = "kps_avg";
        public const string COL_MAX_KPS = "kps_max";
        public const string COL_KPS_LIST_JSON = "kps_list_json";

        public const string COL_UPDATED_AT = "updated_at";
        public const string COL_COLUMN_COUNTS_JSON = "column_counts_json";
        public const string COL_HOLD_NOTE_COUNTS_JSON = "hold_note_counts_json";

        private const string legacy_col_pp = "pp";
        private const string legacy_col_tag_updated_at = "tag_updated_at";
        private const string legacy_col_tag_payload_json = "tag_payload_json";
        private const string legacy_col_xxy_sr = "xxy_sr";
        private const string legacy_index_tag_updated = "idx_ez_analysis_entry_tag_updated";

        public const string META_KEY_FORCE_RECOMPUTE = "force_recompute";
        public const string META_KEY_ANALYSIS_VERSION = "analysis_version";
        public const string META_KEY_KIND = "kind";
        public const string META_KEY_SCHEMA_VERSION = "schema_version";

        public static SqliteConnection OpenConnection(string databasePath)
        {
            var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        public static void InitializeMainDatabase(SqliteConnection connection)
        {
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;
";
                pragma.ExecuteNonQuery();
            }

            EnsureMetaTableExists(connection);
            createMainTablesIfMissing(connection);
            EnsureMainSchemaCurrent(connection);
            EnsureCollectionHideTables(connection);
            SetMeta(connection, META_KEY_KIND, MAIN_DATABASE_KIND);
            SetMeta(connection, META_KEY_SCHEMA_VERSION, MAIN_SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
            SetMeta(connection, META_KEY_ANALYSIS_VERSION, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 从旧版 <c>ez-analysis_v*.sqlite</c> 复制仍需要的 kps / mania 列统计到新的 v7 主库。
        /// </summary>
        public static bool TryMigrateFromPreviousMainDatabase(string targetDatabasePath)
        {
            if (File.Exists(targetDatabasePath))
                return false;

            string? directory = Path.GetDirectoryName(targetDatabasePath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            string? sourcePath = findLatestPreviousMainDatabase(directory, targetDatabasePath);

            if (sourcePath == null)
                return false;

            try
            {
                Directory.CreateDirectory(directory);

                using var source = OpenConnection(sourcePath);
                using var destination = OpenConnection(targetDatabasePath);

                createMainTablesIfMissing(destination);
                copyMainDatabaseData(source, destination);
                copyCollectionHideTables(source, destination);
                copyMetaExceptVersionKeys(source, destination);

                EnsureMainSchemaCurrent(destination);
                EnsureCollectionHideTables(destination);
                SetMeta(destination, META_KEY_KIND, MAIN_DATABASE_KIND);
                SetMeta(destination, META_KEY_SCHEMA_VERSION, MAIN_SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
                SetMeta(destination, META_KEY_ANALYSIS_VERSION, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

                using var vacuum = destination.CreateCommand();
                vacuum.CommandText = "VACUUM;";
                vacuum.ExecuteNonQuery();

                Logger.Log(
                    $"[EzAnalysisSchemaManager] Migrated main analysis database from {Path.GetFileName(sourcePath)} to {Path.GetFileName(targetDatabasePath)}.",
                    Ez2ConfigManager.LOGGER_NAME,
                    LogLevel.Important);

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e,
                    $"[EzAnalysisSchemaManager] Failed to migrate main analysis database from {Path.GetFileName(sourcePath)}; creating fresh v{ANALYSIS_VERSION} database.",
                    Ez2ConfigManager.LOGGER_NAME);

                try
                {
                    if (File.Exists(targetDatabasePath))
                        File.Delete(targetDatabasePath);
                }
                catch
                {
                }

                return false;
            }
        }

        public static void EnsureMainSchemaCurrent(SqliteConnection connection)
        {
            if (!tableExists(connection, TABLE_ENTRY))
                return;

            if (!needsMainSchemaRebuild(connection))
                return;

            rebuildMainDatabaseInPlace(connection);
        }

        public static void EnsureMetaTableExists(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
            cmd.ExecuteNonQuery();
        }

        public static void EnsureCollectionHideTables(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS collection_hidden_state (
    collection_id TEXT PRIMARY KEY,
    hidden_applied INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS collection_hidden_preexisting_beatmap (
    collection_id TEXT NOT NULL,
    beatmap_id TEXT NOT NULL,
    PRIMARY KEY(collection_id, beatmap_id)
);

CREATE TABLE IF NOT EXISTS collection_hidden_beatmap_md5 (
    collection_id TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    PRIMARY KEY(collection_id, beatmap_md5)
);
";
            cmd.ExecuteNonQuery();
        }

        public static void SetMeta(SqliteConnection connection, string key, string value)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO meta(key, value)
VALUES($k, $v)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;
";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }

        public static string? TryGetMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT value
FROM meta
WHERE key = $key
LIMIT 1;
";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        public static bool GetMetaBool(SqliteConnection connection, string key)
            => string.Equals(TryGetMeta(connection, key), "1", StringComparison.Ordinal);

        private static void createMainTablesIfMissing(SqliteConnection connection)
        {
            using var create = connection.CreateCommand();
            create.CommandText = $@"
CREATE TABLE IF NOT EXISTS {TABLE_ENTRY} (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_BEATMAP_HASH} TEXT NOT NULL,
    {COL_BEATMAP_MD5} TEXT NOT NULL,
    {COL_RULESET_ONLINE_ID} INTEGER NOT NULL,
    {COL_COMMON_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_AVERAGE_KPS} REAL NOT NULL DEFAULT 0,
    {COL_MAX_KPS} REAL NOT NULL DEFAULT 0,
    {COL_KPS_LIST_JSON} TEXT NOT NULL DEFAULT '[]'
);

CREATE TABLE IF NOT EXISTS {TABLE_MANIA} (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_COLUMN_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    {COL_HOLD_NOTE_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    FOREIGN KEY({COL_BEATMAP_ID}) REFERENCES {TABLE_ENTRY}({COL_BEATMAP_ID}) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_ruleset ON {TABLE_ENTRY}({COL_RULESET_ONLINE_ID});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_common_updated ON {TABLE_ENTRY}({COL_COMMON_UPDATED_AT});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_mania_updated ON {TABLE_MANIA}({COL_UPDATED_AT});
";
            create.ExecuteNonQuery();
        }

        private static bool needsMainSchemaRebuild(SqliteConnection connection)
        {
            if (hasColumn(connection, TABLE_ENTRY, legacy_col_pp)
                || hasColumn(connection, TABLE_ENTRY, legacy_col_tag_updated_at)
                || hasColumn(connection, TABLE_ENTRY, legacy_col_tag_payload_json))
                return true;

            if (tableExists(connection, TABLE_MANIA) && hasColumn(connection, TABLE_MANIA, legacy_col_xxy_sr))
                return true;

            if (!int.TryParse(TryGetMeta(connection, META_KEY_SCHEMA_VERSION), NumberStyles.Integer, CultureInfo.InvariantCulture, out int storedSchemaVersion))
                return true;

            return storedSchemaVersion < MAIN_SCHEMA_VERSION;
        }

        private static void rebuildMainDatabaseInPlace(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();

            using (var dropLegacyIndex = connection.CreateCommand())
            {
                dropLegacyIndex.Transaction = transaction;
                dropLegacyIndex.CommandText = $"DROP INDEX IF EXISTS {legacy_index_tag_updated};";
                dropLegacyIndex.ExecuteNonQuery();
            }

            using (var createEntry = connection.CreateCommand())
            {
                createEntry.Transaction = transaction;
                createEntry.CommandText = $@"
CREATE TABLE {TABLE_ENTRY}_new (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_BEATMAP_HASH} TEXT NOT NULL,
    {COL_BEATMAP_MD5} TEXT NOT NULL,
    {COL_RULESET_ONLINE_ID} INTEGER NOT NULL,
    {COL_COMMON_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_AVERAGE_KPS} REAL NOT NULL DEFAULT 0,
    {COL_MAX_KPS} REAL NOT NULL DEFAULT 0,
    {COL_KPS_LIST_JSON} TEXT NOT NULL DEFAULT '[]'
);
";
                createEntry.ExecuteNonQuery();
            }

            using (var copyEntry = connection.CreateCommand())
            {
                copyEntry.Transaction = transaction;
                copyEntry.CommandText = $@"
INSERT INTO {TABLE_ENTRY}_new (
    {COL_BEATMAP_ID},
    {COL_BEATMAP_HASH},
    {COL_BEATMAP_MD5},
    {COL_RULESET_ONLINE_ID},
    {COL_COMMON_UPDATED_AT},
    {COL_AVERAGE_KPS},
    {COL_MAX_KPS},
    {COL_KPS_LIST_JSON}
)
SELECT
    {COL_BEATMAP_ID},
    {COL_BEATMAP_HASH},
    {COL_BEATMAP_MD5},
    {COL_RULESET_ONLINE_ID},
    {COL_COMMON_UPDATED_AT},
    {COL_AVERAGE_KPS},
    {COL_MAX_KPS},
    {COL_KPS_LIST_JSON}
FROM {TABLE_ENTRY};
";
                copyEntry.ExecuteNonQuery();
            }

            using (var dropEntry = connection.CreateCommand())
            {
                dropEntry.Transaction = transaction;
                dropEntry.CommandText = $"DROP TABLE {TABLE_ENTRY};";
                dropEntry.ExecuteNonQuery();
            }

            using (var renameEntry = connection.CreateCommand())
            {
                renameEntry.Transaction = transaction;
                renameEntry.CommandText = $"ALTER TABLE {TABLE_ENTRY}_new RENAME TO {TABLE_ENTRY};";
                renameEntry.ExecuteNonQuery();
            }

            if (tableExists(connection, TABLE_MANIA))
            {
                using (var createMania = connection.CreateCommand())
                {
                    createMania.Transaction = transaction;
                    createMania.CommandText = $@"
CREATE TABLE {TABLE_MANIA}_new (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_COLUMN_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    {COL_HOLD_NOTE_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    FOREIGN KEY({COL_BEATMAP_ID}) REFERENCES {TABLE_ENTRY}({COL_BEATMAP_ID}) ON DELETE CASCADE
);
";
                    createMania.ExecuteNonQuery();
                }

                using (var copyMania = connection.CreateCommand())
                {
                    copyMania.Transaction = transaction;
                    copyMania.CommandText = $@"
INSERT INTO {TABLE_MANIA}_new (
    {COL_BEATMAP_ID},
    {COL_UPDATED_AT},
    {COL_COLUMN_COUNTS_JSON},
    {COL_HOLD_NOTE_COUNTS_JSON}
)
SELECT
    {COL_BEATMAP_ID},
    {COL_UPDATED_AT},
    {COL_COLUMN_COUNTS_JSON},
    {COL_HOLD_NOTE_COUNTS_JSON}
FROM {TABLE_MANIA};
";
                    copyMania.ExecuteNonQuery();
                }

                using (var dropMania = connection.CreateCommand())
                {
                    dropMania.Transaction = transaction;
                    dropMania.CommandText = $"DROP TABLE {TABLE_MANIA};";
                    dropMania.ExecuteNonQuery();
                }

                using (var renameMania = connection.CreateCommand())
                {
                    renameMania.Transaction = transaction;
                    renameMania.CommandText = $"ALTER TABLE {TABLE_MANIA}_new RENAME TO {TABLE_MANIA};";
                    renameMania.ExecuteNonQuery();
                }
            }
            else
            {
                using (var createMania = connection.CreateCommand())
                {
                    createMania.Transaction = transaction;
                    createMania.CommandText = $@"
CREATE TABLE {TABLE_MANIA} (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_COLUMN_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    {COL_HOLD_NOTE_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    FOREIGN KEY({COL_BEATMAP_ID}) REFERENCES {TABLE_ENTRY}({COL_BEATMAP_ID}) ON DELETE CASCADE
);
";
                    createMania.ExecuteNonQuery();
                }
            }

            using (var recreateIndexes = connection.CreateCommand())
            {
                recreateIndexes.Transaction = transaction;
                recreateIndexes.CommandText = $@"
CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_ruleset ON {TABLE_ENTRY}({COL_RULESET_ONLINE_ID});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_common_updated ON {TABLE_ENTRY}({COL_COMMON_UPDATED_AT});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_mania_updated ON {TABLE_MANIA}({COL_UPDATED_AT});
";
                recreateIndexes.ExecuteNonQuery();
            }

            transaction.Commit();

            using var vacuum = connection.CreateCommand();
            vacuum.CommandText = "VACUUM;";
            vacuum.ExecuteNonQuery();

            Logger.Log($"[EzAnalysisSchemaManager] Rebuilt main analysis schema to v{MAIN_SCHEMA_VERSION} (analysis v{ANALYSIS_VERSION}).",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);
        }

        private static void copyMainDatabaseData(SqliteConnection source, SqliteConnection destination)
        {
            if (!tableExists(source, TABLE_ENTRY))
                return;

            using (var copyEntry = destination.CreateCommand())
            {
                copyEntry.CommandText = $@"
ATTACH DATABASE $source AS legacy;
INSERT INTO main.{TABLE_ENTRY} (
    {COL_BEATMAP_ID},
    {COL_BEATMAP_HASH},
    {COL_BEATMAP_MD5},
    {COL_RULESET_ONLINE_ID},
    {COL_COMMON_UPDATED_AT},
    {COL_AVERAGE_KPS},
    {COL_MAX_KPS},
    {COL_KPS_LIST_JSON}
)
SELECT
    {COL_BEATMAP_ID},
    {COL_BEATMAP_HASH},
    {COL_BEATMAP_MD5},
    {COL_RULESET_ONLINE_ID},
    {COL_COMMON_UPDATED_AT},
    {COL_AVERAGE_KPS},
    {COL_MAX_KPS},
    {COL_KPS_LIST_JSON}
FROM legacy.{TABLE_ENTRY}
WHERE {COL_COMMON_UPDATED_AT} > 0;
DETACH DATABASE legacy;
";
                copyEntry.Parameters.AddWithValue("$source", source.DataSource);
                copyEntry.ExecuteNonQuery();
            }

            if (!tableExists(source, TABLE_MANIA))
                return;

            using (var copyMania = destination.CreateCommand())
            {
                copyMania.CommandText = $@"
ATTACH DATABASE $source AS legacy;
INSERT INTO main.{TABLE_MANIA} (
    {COL_BEATMAP_ID},
    {COL_UPDATED_AT},
    {COL_COLUMN_COUNTS_JSON},
    {COL_HOLD_NOTE_COUNTS_JSON}
)
SELECT
    {COL_BEATMAP_ID},
    {COL_UPDATED_AT},
    {COL_COLUMN_COUNTS_JSON},
    {COL_HOLD_NOTE_COUNTS_JSON}
FROM legacy.{TABLE_MANIA}
WHERE {COL_UPDATED_AT} > 0;
DETACH DATABASE legacy;
";
                copyMania.Parameters.AddWithValue("$source", source.DataSource);
                copyMania.ExecuteNonQuery();
            }
        }

        private static void copyCollectionHideTables(SqliteConnection source, SqliteConnection destination)
        {
            EnsureCollectionHideTables(destination);

            if (tableExists(source, "collection_hidden_state"))
            {
                using var cmd = destination.CreateCommand();
                cmd.CommandText = @"
ATTACH DATABASE $source AS legacy;
INSERT OR IGNORE INTO main.collection_hidden_state(collection_id, hidden_applied)
SELECT collection_id, hidden_applied FROM legacy.collection_hidden_state;
DETACH DATABASE legacy;
";
                cmd.Parameters.AddWithValue("$source", source.DataSource);
                cmd.ExecuteNonQuery();
            }

            if (tableExists(source, "collection_hidden_preexisting_beatmap"))
            {
                using var cmd = destination.CreateCommand();
                cmd.CommandText = @"
ATTACH DATABASE $source AS legacy;
INSERT OR IGNORE INTO main.collection_hidden_preexisting_beatmap(collection_id, beatmap_id)
SELECT collection_id, beatmap_id FROM legacy.collection_hidden_preexisting_beatmap;
DETACH DATABASE legacy;
";
                cmd.Parameters.AddWithValue("$source", source.DataSource);
                cmd.ExecuteNonQuery();
            }

            if (tableExists(source, "collection_hidden_beatmap_md5"))
            {
                using var cmd = destination.CreateCommand();
                cmd.CommandText = @"
ATTACH DATABASE $source AS legacy;
INSERT OR IGNORE INTO main.collection_hidden_beatmap_md5(collection_id, beatmap_md5)
SELECT collection_id, beatmap_md5 FROM legacy.collection_hidden_beatmap_md5;
DETACH DATABASE legacy;
";
                cmd.Parameters.AddWithValue("$source", source.DataSource);
                cmd.ExecuteNonQuery();
            }
        }

        private static void copyMetaExceptVersionKeys(SqliteConnection source, SqliteConnection destination)
        {
            EnsureMetaTableExists(destination);

            using var cmd = destination.CreateCommand();
            cmd.CommandText = @"
ATTACH DATABASE $source AS legacy;
INSERT INTO main.meta(key, value)
SELECT key, value
FROM legacy.meta
WHERE key NOT IN ($schema_version, $analysis_version, $kind)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;
DETACH DATABASE legacy;
";
            cmd.Parameters.AddWithValue("$source", source.DataSource);
            cmd.Parameters.AddWithValue("$schema_version", META_KEY_SCHEMA_VERSION);
            cmd.Parameters.AddWithValue("$analysis_version", META_KEY_ANALYSIS_VERSION);
            cmd.Parameters.AddWithValue("$kind", META_KEY_KIND);
            cmd.ExecuteNonQuery();
        }

        private static string? findLatestPreviousMainDatabase(string directory, string targetDatabasePath)
        {
            string? bestCandidate = null;
            int bestVersion = -1;

            foreach (string file in Directory.EnumerateFiles(directory, "ez-analysis_v*.sqlite", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(targetDatabasePath), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!tryParseMainDatabaseVersion(file, out int version))
                    continue;

                if (version >= ANALYSIS_VERSION)
                    continue;

                if (version > bestVersion)
                {
                    bestVersion = version;
                    bestCandidate = file;
                }
            }

            return bestCandidate;
        }

        private static bool tryParseMainDatabaseVersion(string filePath, out int version)
        {
            version = 0;

            string name = Path.GetFileName(filePath);
            const string prefix = "ez-analysis_v";
            const string suffix = ".sqlite";

            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            string number = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out version);
        }

        private static bool tableExists(SqliteConnection connection, string tableName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", tableName);
            return cmd.ExecuteScalar() != null;
        }

        private static bool hasColumn(SqliteConnection connection, string tableName, string columnName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"PRAGMA table_info({tableName});";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
