// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class TestSongsBranchSchemaMigration
    {
        private string databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "SongsBranchMigrationTests", Guid.NewGuid().ToString("N") + ".sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                SqliteConnection.ClearAllPools();

                if (File.Exists(databasePath))
                    File.Delete(databasePath);
            }
            catch
            {
            }
        }

        [Test]
        public void Legacy_xxy_sr_branch_v1_migrates_to_songs_branch_without_data_loss()
        {
            using (var connection = openConnection())
            {
                createLegacyV1Branch(connection);
                insertLegacyRow(connection, xxySr: 4.56, pp: null);
            }

            using var migrated = openConnection();
            Assert.That(prepareBranch(migrated), Is.True);
            Assert.That(readMeta(migrated, "kind"), Is.EqualTo("songs_branch"));
            Assert.That(readMeta(migrated, "schema_version"), Is.EqualTo("3"));
            Assert.That(tableExists(migrated, "songs_branch_entry"), Is.True);
            Assert.That(tableExists(migrated, "xxy_sr_branch"), Is.False);
            Assert.That(readXxySr(migrated), Is.EqualTo(4.56).Within(0.001));
            Assert.That(readMeta(migrated, "xxy_sr_version"), Is.Null);
            Assert.That(readMeta(migrated, "requires_post_migration_refresh"), Is.EqualTo("1"));
        }

        [Test]
        public void Legacy_xxy_sr_branch_v2_with_pp_migrates_in_place()
        {
            using (var connection = openConnection())
            {
                createLegacyV2Branch(connection);
                insertLegacyRow(connection, xxySr: 7.89, pp: 123.4);
            }

            using var migrated = openConnection();
            Assert.That(prepareBranch(migrated), Is.True);
            Assert.That(readPp(migrated), Is.EqualTo(123.4).Within(0.001));
            Assert.That(readMeta(migrated, "pp_version"), Is.EqualTo("20241007"));
            Assert.That(readMeta(migrated, "requires_post_migration_refresh"), Is.Null);
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        private static bool prepareBranch(SqliteConnection connection)
        {
            var method = typeof(EzAnalysisPersistentStore).GetMethod("prepareSongsBranchConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method != null && (bool)method.Invoke(null, new object[] { connection })!;
        }

        private static void createLegacyV1Branch(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);

CREATE TABLE xxy_sr_branch (
    beatmap_id TEXT PRIMARY KEY,
    beatmap_hash TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    xxy_sr REAL NOT NULL
);

INSERT INTO meta(key, value) VALUES
    ('kind', 'xxy_sr_branch'),
    ('schema_version', '1'),
    ('analysis_version', '6'),
    ('ruleset_online_id', '3'),
    ('ruleset_short_name', 'mania'),
    ('mods_fingerprint', ''),
    ('mods_display', 'NoMod'),
    ('beatmap_count', '1'),
    ('created_at', '1'),
    ('display_name', 'legacy branch'),
    ('xxy_sr_version', '20250415');
";
            cmd.ExecuteNonQuery();
        }

        private static void createLegacyV2Branch(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);

CREATE TABLE xxy_sr_branch (
    beatmap_id TEXT PRIMARY KEY,
    beatmap_hash TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    xxy_sr REAL NOT NULL,
    pp REAL NULL
);

INSERT INTO meta(key, value) VALUES
    ('kind', 'xxy_sr_branch'),
    ('schema_version', '2'),
    ('analysis_version', '6'),
    ('ruleset_online_id', '3'),
    ('ruleset_short_name', 'mania'),
    ('mods_fingerprint', ''),
    ('mods_display', 'NoMod'),
    ('beatmap_count', '1'),
    ('created_at', '1'),
    ('display_name', 'legacy branch'),
    ('xxy_sr_version', '20250415'),
    ('pp_version', '20241007');
";
            cmd.ExecuteNonQuery();
        }

        private static void insertLegacyRow(SqliteConnection connection, double xxySr, double? pp)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = pp.HasValue
                ? @"INSERT INTO xxy_sr_branch(beatmap_id, beatmap_hash, beatmap_md5, xxy_sr, pp)
VALUES('00000000-0000-0000-0000-000000000001', 'hash', 'md5', $xxy, $pp);"
                : @"INSERT INTO xxy_sr_branch(beatmap_id, beatmap_hash, beatmap_md5, xxy_sr)
VALUES('00000000-0000-0000-0000-000000000001', 'hash', 'md5', $xxy);";
            cmd.Parameters.AddWithValue("$xxy", xxySr);

            if (pp.HasValue)
                cmd.Parameters.AddWithValue("$pp", pp.Value);

            cmd.ExecuteNonQuery();
        }

        private static string? readMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key = $key LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        private static bool tableExists(SqliteConnection connection, string tableName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", tableName);
            return cmd.ExecuteScalar() != null;
        }

        private static double readXxySr(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT xxy_sr FROM songs_branch_entry LIMIT 1;";
            return Convert.ToDouble(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static double readPp(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pp FROM songs_branch_entry LIMIT 1;";
            return Convert.ToDouble(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }
}
