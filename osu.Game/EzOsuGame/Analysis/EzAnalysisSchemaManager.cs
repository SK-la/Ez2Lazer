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
        // Note: main sqlite v7 stores kps/KPC only (slim schema). Legacy pp/tag/xxy_sr columns removed at schema v3.
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

        public const string COL_TAG_UPDATED_AT = "tag_updated_at";
        public const string COL_TAG_PAYLOAD_JSON = "tag_payload_json";
        public const string COL_XXY_SR = "xxy_sr";
        public const string COL_PP = "pp";

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
            SetMeta(connection, META_KEY_FORCE_RECOMPUTE, "0");
        }

        /// <summary>
        /// Clears kps / mania column timestamps so startup warmup recomputes only those slices (维护/强制重算用).
        /// </summary>
        public static void InvalidateAllCachedAnalysisTimestamps(SqliteConnection connection)
        {
            if (!tableExists(connection, TABLE_ENTRY))
                return;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
UPDATE {TABLE_ENTRY}
SET {COL_COMMON_UPDATED_AT} = 0
WHERE {COL_COMMON_UPDATED_AT} <> 0;

UPDATE {TABLE_MANIA}
SET {COL_UPDATED_AT} = 0
WHERE {COL_UPDATED_AT} <> 0;
";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 从旧版 <c>ez-analysis_v*.sqlite</c>（或中间版 <c>ez-analysis.sqlite</c>）复制 kps / mania 列统计到当前版本文件。
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
                SetMeta(destination, META_KEY_FORCE_RECOMPUTE, "0");

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

        /// <summary>
        /// 当前版本主库文件已存在且 schema / analysis 版本与代码一致。
        /// </summary>
        public static bool IsMainDatabaseMatching(string databasePath)
        {
            if (!File.Exists(databasePath))
                return false;

            try
            {
                using var connection = OpenConnection(databasePath);

                if (!tableExists(connection, TABLE_ENTRY))
                    return false;

                if (!int.TryParse(TryGetMeta(connection, META_KEY_SCHEMA_VERSION), NumberStyles.Integer, CultureInfo.InvariantCulture, out int storedSchemaVersion)
                    || storedSchemaVersion != MAIN_SCHEMA_VERSION)
                    return false;

                if (!int.TryParse(TryGetMeta(connection, META_KEY_ANALYSIS_VERSION), NumberStyles.Integer, CultureInfo.InvariantCulture, out int storedAnalysisVersion)
                    || storedAnalysisVersion != ANALYSIS_VERSION)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 存在可迁移的旧版主库，或当前主库需要 schema / 版本升级。
        /// 已匹配的最新版文件不应触发启动预热。
        /// </summary>
        public static bool ShouldRunAutomaticSqliteWarmup(string databasePath)
        {
            if (IsMainDatabaseMatching(databasePath))
                return false;

            if (!File.Exists(databasePath))
            {
                string? directory = Path.GetDirectoryName(databasePath);
                return !string.IsNullOrEmpty(directory) && findLatestPreviousMainDatabase(directory, databasePath) != null;
            }

            return true;
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
            if (!int.TryParse(TryGetMeta(connection, META_KEY_SCHEMA_VERSION), NumberStyles.Integer, CultureInfo.InvariantCulture, out int storedSchemaVersion))
                return false;

            return storedSchemaVersion < MAIN_SCHEMA_VERSION;
        }

        private static void rebuildMainDatabaseInPlace(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();

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

            using (var selectEntry = source.CreateCommand())
            {
                selectEntry.CommandText = $@"
SELECT
    {COL_BEATMAP_ID},
    {COL_BEATMAP_HASH},
    {COL_BEATMAP_MD5},
    {COL_RULESET_ONLINE_ID},
    {COL_COMMON_UPDATED_AT},
    {COL_AVERAGE_KPS},
    {COL_MAX_KPS},
    {COL_KPS_LIST_JSON}
FROM {TABLE_ENTRY}
WHERE {COL_COMMON_UPDATED_AT} > 0;
";

                using var reader = selectEntry.ExecuteReader();
                using var insertEntry = destination.CreateCommand();
                insertEntry.CommandText = $@"
INSERT OR REPLACE INTO {TABLE_ENTRY} (
    {COL_BEATMAP_ID},
    {COL_BEATMAP_HASH},
    {COL_BEATMAP_MD5},
    {COL_RULESET_ONLINE_ID},
    {COL_COMMON_UPDATED_AT},
    {COL_AVERAGE_KPS},
    {COL_MAX_KPS},
    {COL_KPS_LIST_JSON}
)
VALUES (
    $id,
    $hash,
    $md5,
    $ruleset,
    $common_updated_at,
    $avg,
    $max,
    $kps
);
";

                while (reader.Read())
                {
                    insertEntry.Parameters.Clear();
                    insertEntry.Parameters.AddWithValue("$id", reader.GetString(0));
                    insertEntry.Parameters.AddWithValue("$hash", reader.GetString(1));
                    insertEntry.Parameters.AddWithValue("$md5", reader.GetString(2));
                    insertEntry.Parameters.AddWithValue("$ruleset", reader.GetInt32(3));
                    insertEntry.Parameters.AddWithValue("$common_updated_at", reader.GetInt64(4));
                    insertEntry.Parameters.AddWithValue("$avg", reader.GetDouble(5));
                    insertEntry.Parameters.AddWithValue("$max", reader.GetDouble(6));
                    insertEntry.Parameters.AddWithValue("$kps", reader.GetString(7));
                    insertEntry.ExecuteNonQuery();
                }
            }

            if (!tableExists(source, TABLE_MANIA))
                return;

            using (var selectMania = source.CreateCommand())
            {
                selectMania.CommandText = $@"
SELECT
    {COL_BEATMAP_ID},
    {COL_UPDATED_AT},
    {COL_COLUMN_COUNTS_JSON},
    {COL_HOLD_NOTE_COUNTS_JSON}
FROM {TABLE_MANIA}
WHERE {COL_UPDATED_AT} > 0;
";

                using var reader = selectMania.ExecuteReader();
                using var insertMania = destination.CreateCommand();
                insertMania.CommandText = $@"
INSERT OR REPLACE INTO {TABLE_MANIA} (
    {COL_BEATMAP_ID},
    {COL_UPDATED_AT},
    {COL_COLUMN_COUNTS_JSON},
    {COL_HOLD_NOTE_COUNTS_JSON}
)
VALUES (
    $id,
    $updated_at,
    $column_counts_json,
    $hold_note_counts_json
);
";

                while (reader.Read())
                {
                    insertMania.Parameters.Clear();
                    insertMania.Parameters.AddWithValue("$id", reader.GetString(0));
                    insertMania.Parameters.AddWithValue("$updated_at", reader.GetInt64(1));
                    insertMania.Parameters.AddWithValue("$column_counts_json", reader.GetString(2));
                    insertMania.Parameters.AddWithValue("$hold_note_counts_json", reader.GetString(3));
                    insertMania.ExecuteNonQuery();
                }
            }
        }

        private static void copyCollectionHideTables(SqliteConnection source, SqliteConnection destination)
        {
            EnsureCollectionHideTables(destination);

            if (tableExists(source, "collection_hidden_state"))
            {
                using var select = source.CreateCommand();
                select.CommandText = "SELECT collection_id, hidden_applied FROM collection_hidden_state;";
                using var reader = select.ExecuteReader();
                using var insert = destination.CreateCommand();
                insert.CommandText = "INSERT OR IGNORE INTO collection_hidden_state(collection_id, hidden_applied) VALUES($collection_id, $hidden_applied);";

                while (reader.Read())
                {
                    insert.Parameters.Clear();
                    insert.Parameters.AddWithValue("$collection_id", reader.GetString(0));
                    insert.Parameters.AddWithValue("$hidden_applied", reader.GetInt64(1));
                    insert.ExecuteNonQuery();
                }
            }

            if (tableExists(source, "collection_hidden_preexisting_beatmap"))
            {
                using var select = source.CreateCommand();
                select.CommandText = "SELECT collection_id, beatmap_id FROM collection_hidden_preexisting_beatmap;";
                using var reader = select.ExecuteReader();
                using var insert = destination.CreateCommand();
                insert.CommandText = "INSERT OR IGNORE INTO collection_hidden_preexisting_beatmap(collection_id, beatmap_id) VALUES($collection_id, $beatmap_id);";

                while (reader.Read())
                {
                    insert.Parameters.Clear();
                    insert.Parameters.AddWithValue("$collection_id", reader.GetString(0));
                    insert.Parameters.AddWithValue("$beatmap_id", reader.GetString(1));
                    insert.ExecuteNonQuery();
                }
            }

            if (tableExists(source, "collection_hidden_beatmap_md5"))
            {
                using var select = source.CreateCommand();
                select.CommandText = "SELECT collection_id, beatmap_md5 FROM collection_hidden_beatmap_md5;";
                using var reader = select.ExecuteReader();
                using var insert = destination.CreateCommand();
                insert.CommandText = "INSERT OR IGNORE INTO collection_hidden_beatmap_md5(collection_id, beatmap_md5) VALUES($collection_id, $beatmap_md5);";

                while (reader.Read())
                {
                    insert.Parameters.Clear();
                    insert.Parameters.AddWithValue("$collection_id", reader.GetString(0));
                    insert.Parameters.AddWithValue("$beatmap_md5", reader.GetString(1));
                    insert.ExecuteNonQuery();
                }
            }
        }

        private static void copyMetaExceptVersionKeys(SqliteConnection source, SqliteConnection destination)
        {
            EnsureMetaTableExists(destination);

            if (!tableExists(source, "meta"))
                return;

            using var select = source.CreateCommand();
            select.CommandText = "SELECT key, value FROM meta;";
            using var reader = select.ExecuteReader();
            using var insert = destination.CreateCommand();
            insert.CommandText = @"
INSERT INTO meta(key, value)
VALUES($key, $value)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;
";

            while (reader.Read())
            {
                string key = reader.GetString(0);

                if (key == META_KEY_SCHEMA_VERSION
                    || key == META_KEY_ANALYSIS_VERSION
                    || key == "kps_kpc_version"
                    || key == "computation_version"
                    || key == "pp_version"
                    || key == "xxy_sr_version"
                    || key == META_KEY_FORCE_RECOMPUTE
                    || key == META_KEY_KIND)
                    continue;

                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("$key", key);
                insert.Parameters.AddWithValue("$value", reader.GetString(1));
                insert.ExecuteNonQuery();
            }
        }

        private static string? findLatestPreviousMainDatabase(string directory, string targetDatabasePath)
        {
            string? bestCandidate = null;
            int bestVersion = -1;

            foreach (string file in Directory.EnumerateFiles(directory, $"{EzAnalysisPersistentStore.LEGACY_DATABASE_FILENAME_PREFIX}*.sqlite", SearchOption.TopDirectoryOnly))
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

            string stableLegacyPath = Path.Combine(directory, EzAnalysisPersistentStore.LEGACY_STABLE_DATABASE_FILENAME);

            if (File.Exists(stableLegacyPath)
                && !string.Equals(Path.GetFullPath(stableLegacyPath), Path.GetFullPath(targetDatabasePath), StringComparison.OrdinalIgnoreCase)
                && 6 > bestVersion)
            {
                bestCandidate = stableLegacyPath;
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
