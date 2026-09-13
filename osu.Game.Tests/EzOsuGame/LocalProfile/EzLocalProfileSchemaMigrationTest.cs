// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Opening an <c>ez-local-profile.sqlite</c> written by an older build must migrate in place. Regression cover
    /// for a schema batch that created an index over a column the file did not have yet: CREATE TABLE IF NOT EXISTS
    /// never extends an existing table, so the index ran before the column existed and aborted the whole batch with
    /// "no such column".
    /// </summary>
    [TestFixture]
    public class EzLocalProfileSchemaMigrationTest
    {
        [Test]
        public void Open_legacy_file_without_online_contribution_username()
        {
            using var storage = new TemporaryNativeStorage($"ez-migrate-online-{Guid.NewGuid():N}");

            writeLegacyFile(storage, @"CREATE TABLE online_score_contributions (
                                           online_id INTEGER PRIMARY KEY NOT NULL,
                                           ruleset_id INTEGER NOT NULL,
                                           rank INTEGER NOT NULL,
                                           star_rating REAL NOT NULL,
                                           circle_size REAL NOT NULL,
                                           approach_rate REAL NOT NULL,
                                           key_count INTEGER NOT NULL
                                       );");

            using var store = new EzLocalProfileStore(storage);

            // The legacy table has no `username`, so opening used to throw before the column was added.
            Assert.That(store.LoadOnlineScoreContributions(), Is.Empty);
        }

        [Test]
        public void Open_legacy_file_without_partition_excluded_flag()
        {
            using var storage = new TemporaryNativeStorage($"ez-migrate-partition-{Guid.NewGuid():N}");

            writeLegacyFile(storage, @"CREATE TABLE username_partitions (
                                           username TEXT PRIMARY KEY NOT NULL,
                                           payload_json TEXT NOT NULL,
                                           updated_at INTEGER NOT NULL
                                       );");

            using var store = new EzLocalProfileStore(storage);

            // Every partition written before the flag existed counts as included.
            Assert.That(store.LoadIncludedUsernames(), Is.Empty);
        }

        /// <summary>
        /// Creates a file holding just the given legacy table, i.e. the on-disk state a build before the migration
        /// would have left behind.
        /// </summary>
        private static void writeLegacyFile(TemporaryNativeStorage storage, string legacyTableSql)
        {
            string path = storage.GetFullPath(EzLocalProfileStore.DATABASE_FILENAME, true);

            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = legacyTableSql;
            cmd.ExecuteNonQuery();

            SqliteConnection.ClearAllPools();
        }
    }
}
