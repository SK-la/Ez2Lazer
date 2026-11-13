// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Scoring;

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

        [Test]
        public void Open_legacy_drill_scores_without_ssr_columns_can_load_drills()
        {
            using var storage = new TemporaryNativeStorage($"ez-migrate-drill-{Guid.NewGuid():N}");

            writeLegacyFile(storage, """
                                   CREATE TABLE drill_scores (
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
                                       mods_json TEXT NOT NULL DEFAULT '[]',
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
                                       kps_avg REAL NOT NULL,
                                       kps_max REAL NOT NULL,
                                       kps_list_json TEXT NOT NULL,
                                       column_counts_json TEXT NOT NULL,
                                       hold_counts_json TEXT NOT NULL,
                                       avg_abs_offset_ms REAL,
                                       has_video INTEGER NOT NULL,
                                       has_storyboard INTEGER NOT NULL,
                                       date_ms INTEGER NOT NULL
                                   );
                                   """);

            using var store = new EzLocalProfileStore(storage);

            Assert.That(store.LoadDrillScores(EzLocalProfileConstants.MANIA_RULESET_ID, null), Is.Empty);
        }

        [Test]
        public void Legacy_xxy_sunny_column_is_renamed_before_append()
        {
            using var storage = new TemporaryNativeStorage($"ez-migrate-xxy-{Guid.NewGuid():N}");

            writeLegacyFile(storage, legacyDrillTableSql("xxy_star_rating REAL NOT NULL"));

            using var store = new EzLocalProfileStore(storage);

            store.AppendDrills(new[] { createMinimalDrill() });
            Assert.That(drillTableHasColumn(storage, "ssr_star_rating"), Is.True);
            Assert.That(drillTableHasColumn(storage, "xxy_star_rating"), Is.False);
        }

        [Test]
        public void Legacy_both_xxy_and_ssr_columns_allow_append()
        {
            using var storage = new TemporaryNativeStorage($"ez-migrate-xxy-ssr-{Guid.NewGuid():N}");

            writeLegacyFile(storage, legacyDrillTableSql("xxy_star_rating REAL NOT NULL, ssr_star_rating REAL NOT NULL DEFAULT -1"));

            using var store = new EzLocalProfileStore(storage);

            store.AppendDrills(new[] { createMinimalDrill() });
            Assert.That(drillTableHasColumn(storage, "xxy_star_rating"), Is.False);
        }

        private static string legacyDrillTableSql(string sunnyColumnSql) => $"""
            CREATE TABLE drill_scores (
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
                mods_json TEXT NOT NULL DEFAULT '[]',
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
                {sunnyColumnSql},
                map_performance_points REAL NOT NULL DEFAULT 0,
                kps_avg REAL NOT NULL,
                kps_max REAL NOT NULL,
                kps_list_json TEXT NOT NULL,
                column_counts_json TEXT NOT NULL,
                hold_counts_json TEXT NOT NULL,
                avg_abs_offset_ms REAL,
                has_video INTEGER NOT NULL,
                has_storyboard INTEGER NOT NULL,
                date_ms INTEGER NOT NULL
            );
            """;

        private static EzLocalProfileDrillScoreRow createMinimalDrill()
            => new EzLocalProfileDrillScoreRow
            {
                ScoreId = Guid.NewGuid(),
                Username = "legacy-user",
                RulesetId = EzLocalProfileConstants.MANIA_RULESET_ID,
                Rank = ScoreRank.S,
                PpResolved = 100,
                Accuracy = 0.99,
                MaxCombo = 50,
                MaxAchievableCombo = 50,
                BeatmapHash = "hash",
                BeatmapId = Guid.NewGuid(),
                Title = "t",
                Artist = "a",
                DifficultyName = "d",
                MapperUsername = "m",
                Date = DateTimeOffset.UtcNow,
            };

        private static bool drillTableHasColumn(TemporaryNativeStorage storage, string column)
        {
            string path = storage.GetFullPath(EzLocalProfileStore.DATABASE_FILENAME, true);

            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadWrite");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(drill_scores);";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
