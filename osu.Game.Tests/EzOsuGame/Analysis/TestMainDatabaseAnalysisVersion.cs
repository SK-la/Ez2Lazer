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
    /// <summary>
    /// The main database carries one revision for the slices it stores. A file written by another revision is retired
    /// when it is opened, and the revision is stamped at the same time, so an interrupted warmup resumes on whatever
    /// is still empty instead of being read as complete.
    /// </summary>
    [TestFixture]
    public class TestMainDatabaseAnalysisVersion
    {
        private string databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "MainDatabaseVersionTests", Guid.NewGuid().ToString("N") + ".sqlite");
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
        public void A_file_from_another_revision_is_retired_and_stamped()
        {
            using (var connection = openConnection())
            {
                EzAnalysisSchemaManager.InitializeMainDatabase(connection);
                insertEntry(connection, commonUpdatedAt: 1234);
                insertMania(connection, updatedAt: 5678);

                // Pretend the file was written by an older build.
                EzAnalysisSchemaManager.SetMeta(connection, EzAnalysisSchemaManager.META_KEY_ANALYSIS_VERSION, "1");
            }

            using (var connection = openConnection())
                EzAnalysisSchemaManager.InitializeMainDatabase(connection);

            using (var connection = openConnection())
            {
                Assert.That(readCommonUpdatedAt(connection), Is.EqualTo(0), "the slice is retired, not recomputed in place");
                Assert.That(readManiaUpdatedAt(connection), Is.EqualTo(0));
                Assert.That(EzAnalysisSchemaManager.TryGetMeta(connection, EzAnalysisSchemaManager.META_KEY_ANALYSIS_VERSION),
                    Is.EqualTo(EzAnalysisSchemaManager.ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture)));
            }

            Assert.That(EzAnalysisSchemaManager.ShouldRunAutomaticSqliteWarmup(databasePath), Is.True,
                "the retired rows are what the warmup fills");
        }

        [Test]
        public void A_file_at_the_current_revision_keeps_its_rows()
        {
            using (var connection = openConnection())
            {
                EzAnalysisSchemaManager.InitializeMainDatabase(connection);
                insertEntry(connection, commonUpdatedAt: 1234);
                insertMania(connection, updatedAt: 5678);
            }

            using (var connection = openConnection())
                EzAnalysisSchemaManager.InitializeMainDatabase(connection);

            using (var connection = openConnection())
            {
                Assert.That(readCommonUpdatedAt(connection), Is.EqualTo(1234));
                Assert.That(readManiaUpdatedAt(connection), Is.EqualTo(5678));
            }

            Assert.That(EzAnalysisSchemaManager.ShouldRunAutomaticSqliteWarmup(databasePath), Is.False);
        }

        [Test]
        public void A_partially_filled_file_still_reports_work()
        {
            using (var connection = openConnection())
            {
                EzAnalysisSchemaManager.InitializeMainDatabase(connection);
                insertEntry(connection, commonUpdatedAt: 1234);
                insertMania(connection, updatedAt: 0);
            }

            Assert.That(EzAnalysisSchemaManager.ShouldRunAutomaticSqliteWarmup(databasePath), Is.True,
                "the revision matches, but the mania slice was never written");
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        private static void insertEntry(SqliteConnection connection, long commonUpdatedAt)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
INSERT INTO {EzAnalysisSchemaManager.TABLE_ENTRY}(
    {EzAnalysisSchemaManager.COL_BEATMAP_ID},
    {EzAnalysisSchemaManager.COL_BEATMAP_HASH},
    {EzAnalysisSchemaManager.COL_BEATMAP_MD5},
    {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
    {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT})
VALUES('00000000-0000-0000-0000-000000000001', 'hash', 'md5', 3, $updated);";
            cmd.Parameters.AddWithValue("$updated", commonUpdatedAt);
            cmd.ExecuteNonQuery();
        }

        private static void insertMania(SqliteConnection connection, long updatedAt)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
INSERT INTO {EzAnalysisSchemaManager.TABLE_MANIA}(
    {EzAnalysisSchemaManager.COL_BEATMAP_ID},
    {EzAnalysisSchemaManager.COL_UPDATED_AT})
VALUES('00000000-0000-0000-0000-000000000001', $updated);";
            cmd.Parameters.AddWithValue("$updated", updatedAt);
            cmd.ExecuteNonQuery();
        }

        private static long readCommonUpdatedAt(SqliteConnection connection) => readLong(connection, EzAnalysisSchemaManager.TABLE_ENTRY, EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT);

        private static long readManiaUpdatedAt(SqliteConnection connection) => readLong(connection, EzAnalysisSchemaManager.TABLE_MANIA, EzAnalysisSchemaManager.COL_UPDATED_AT);

        private static long readLong(SqliteConnection connection, string table, string column)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT {column} FROM {table} LIMIT 1;";
            return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }
}
