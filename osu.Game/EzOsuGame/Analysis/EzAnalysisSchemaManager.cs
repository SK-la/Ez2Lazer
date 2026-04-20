// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 统一维护主分析库的 schema 与 meta 读写。
    /// </summary>
    internal static class EzAnalysisSchemaManager
    {
        public const int ANALYSIS_VERSION = 6;
        public const int MAIN_SCHEMA_VERSION = 1;
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
        public const string COL_TAG_UPDATED_AT = "tag_updated_at";
        public const string COL_TAG_PAYLOAD_JSON = "tag_payload_json";

        public const string COL_UPDATED_AT = "updated_at";
        public const string COL_XXY_SR = "xxy_sr";
        public const string COL_COLUMN_COUNTS_JSON = "column_counts_json";
        public const string COL_HOLD_NOTE_COUNTS_JSON = "hold_note_counts_json";

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

            using (var create = connection.CreateCommand())
            {
                create.CommandText = $@"
CREATE TABLE IF NOT EXISTS {TABLE_ENTRY} (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_BEATMAP_HASH} TEXT NOT NULL,
    {COL_BEATMAP_MD5} TEXT NOT NULL,
    {COL_RULESET_ONLINE_ID} INTEGER NOT NULL,
    {COL_COMMON_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_AVERAGE_KPS} REAL NOT NULL DEFAULT 0,
    {COL_MAX_KPS} REAL NOT NULL DEFAULT 0,
    {COL_KPS_LIST_JSON} TEXT NOT NULL DEFAULT '[]',
    {COL_TAG_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_TAG_PAYLOAD_JSON} TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS {TABLE_MANIA} (
    {COL_BEATMAP_ID} TEXT PRIMARY KEY,
    {COL_UPDATED_AT} INTEGER NOT NULL DEFAULT 0,
    {COL_XXY_SR} REAL NULL,
    {COL_COLUMN_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    {COL_HOLD_NOTE_COUNTS_JSON} TEXT NOT NULL DEFAULT '{{}}',
    FOREIGN KEY({COL_BEATMAP_ID}) REFERENCES {TABLE_ENTRY}({COL_BEATMAP_ID}) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_ruleset ON {TABLE_ENTRY}({COL_RULESET_ONLINE_ID});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_common_updated ON {TABLE_ENTRY}({COL_COMMON_UPDATED_AT});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_entry_tag_updated ON {TABLE_ENTRY}({COL_TAG_UPDATED_AT});
CREATE INDEX IF NOT EXISTS idx_ez_analysis_mania_updated ON {TABLE_MANIA}({COL_UPDATED_AT});
";
                create.ExecuteNonQuery();
            }

            EnsureCollectionHideTables(connection);
            SetMeta(connection, META_KEY_KIND, MAIN_DATABASE_KIND);
            SetMeta(connection, META_KEY_SCHEMA_VERSION, MAIN_SCHEMA_VERSION.ToString(CultureInfo.InvariantCulture));
            SetMeta(connection, META_KEY_ANALYSIS_VERSION, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
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
    }
}
