// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsLibraryIndexRepositoryTest
    {
        [Test]
        public void TestIncrementalSnapshotAndDeleteOrphans()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-index-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string dbPath = Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE);
                var repository = new BmsLibraryIndexRepository(dbPath);

                var chart = new BMSChartCache
                {
                    FolderPath = @"E:\bms\song-a",
                    FileName = "a.bms",
                    FileSize = 100,
                    LastModified = DateTime.UtcNow,
                    Title = "Chart",
                    Artist = "Artist",
                    KeyCount = 7,
                    TotalNotes = 10,
                    Bpm = 140,
                };

                string chartPath = chart.FullPath;
                string pathKey = BmsPathKeys.ComputeChartPathKey(chartPath);
                Guid beatmapId = Guid.NewGuid();

                repository.UpsertSong(new BMSSongCache
                {
                    FolderPath = chart.FolderPath,
                    Title = "Song",
                    Artist = "Artist",
                    LastModified = DateTime.UtcNow,
                });
                repository.UpsertChart(chart, beatmapId, pathKey);

                var secondChart = new BMSChartCache
                {
                    FolderPath = chart.FolderPath,
                    FileName = "b.bms",
                    FileSize = 200,
                    LastModified = chart.LastModified,
                    Title = "Second Chart",
                    Artist = chart.Artist,
                    KeyCount = 7,
                    TotalNotes = 20,
                    Bpm = 160,
                };
                BmsChartIdentity secondIdentity = BmsChartIdentity.Create(secondChart.FullPath, secondChart.FolderPath);
                repository.UpsertChart(secondChart, secondIdentity.BeatmapId, secondIdentity.PathKey);

                var snapshots = repository.GetChartSnapshots();
                Assert.That(snapshots.ContainsKey(chartPath), Is.True);
                Assert.That(repository.ChartCount, Is.EqualTo(2));
                Assert.That(repository.SongCount, Is.EqualTo(1));
                Assert.That(repository.TryLoadChart(chartPath, out _), Is.True);
                Assert.That(repository.TryGetSourceReference(beatmapId, out var reference), Is.True);
                Assert.That(reference.ChartPath, Is.EqualTo(chartPath));
                Assert.That(repository.TryGetChart(beatmapId, out var indexedById), Is.True);
                Assert.That(repository.TryGetChartByPathKey(pathKey, out var indexedByPathKey), Is.True);
                Assert.That(indexedById.Identity, Is.EqualTo(indexedByPathKey.Identity));
                Assert.That(indexedById.Chart.FullPath, Is.EqualTo(indexedByPathKey.Chart.FullPath));
                Assert.That(indexedById.Identity.SetId, Is.EqualTo(BmsChartIdentity.CreateSetId(chart.FolderPath)));
                Assert.That(indexedById.SyncState, Is.EqualTo(BmsLibraryIndexRepository.SyncState.Pending));

                var firstPage = repository.GetCharts(0, 1);
                var secondPage = repository.GetCharts(1, 1);
                Assert.That(firstPage, Has.Count.EqualTo(1));
                Assert.That(secondPage, Has.Count.EqualTo(1));
                Assert.That(firstPage[0].Identity.BeatmapId, Is.Not.EqualTo(secondPage[0].Identity.BeatmapId));

                var upsertChanges = repository.GetPendingSyncChanges(10);
                Assert.That(upsertChanges, Has.Count.EqualTo(2));
                Assert.That(upsertChanges[0].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Upsert));

                int deleted = repository.DeleteChartsNotIn(new List<string>());
                Assert.That(deleted, Is.EqualTo(2));
                Assert.That(repository.TryLoadChart(chartPath, out _), Is.False);

                var changes = repository.GetPendingSyncChanges(10);
                Assert.That(changes, Has.Count.EqualTo(4));
                Assert.That(changes[2].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Delete));
                Assert.That(changes[3].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Delete));
            }
            finally
            {
                cleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public void TestImportLegacyLibraryCache()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-index-migrate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string dbPath = Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE);
                var repository = new BmsLibraryIndexRepository(dbPath);

                var cache = new BMSLibraryCache
                {
                    RootPaths = { @"D:\BMS" },
                    Songs =
                    {
                        new BMSSongCache
                        {
                            FolderPath = @"D:\BMS\song",
                            Title = "Song",
                            Charts =
                            {
                                new BMSChartCache
                                {
                                    FolderPath = @"D:\BMS\song",
                                    FileName = "chart.bms",
                                    Md5Hash = "legacy-md5",
                                    KeyCount = 7,
                                }
                            }
                        }
                    }
                };

                repository.ImportFromLibraryCache(cache);

                var loaded = repository.LoadLibraryCache();
                Assert.That(loaded.TotalCharts, Is.EqualTo(1));
                Assert.That(loaded.RootPaths, Contains.Item(@"D:\BMS"));
            }
            finally
            {
                cleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public void TestMigratesVersion1WithoutChangingIdentity()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-index-v1-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string dbPath = Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE);
                const string folderPath = @"D:\BMS\song";
                string chartPath = Path.Combine(folderPath, "chart.bms");
                Guid beatmapId = Guid.NewGuid();
                const string pathKey = "existing-path-key";

                createVersion1Database(dbPath, chartPath, folderPath, beatmapId, pathKey);

                var repository = new BmsLibraryIndexRepository(dbPath);
                Assert.That(repository.ChartCount, Is.EqualTo(1));
                Assert.That(repository.SongCount, Is.EqualTo(0));
                Assert.That(repository.TryGetChart(beatmapId, out var chart), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(chart.Identity.BeatmapId, Is.EqualTo(beatmapId));
                    Assert.That(chart.Identity.PathKey, Is.EqualTo(pathKey));
                    Assert.That(chart.Identity.SetId, Is.EqualTo(BmsChartIdentity.CreateSetId(folderPath)));
                    Assert.That(chart.SyncState, Is.EqualTo(BmsLibraryIndexRepository.SyncState.Pending));
                    Assert.That(chart.ParseVersion, Is.EqualTo(1));
                });

                var changes = repository.GetPendingSyncChanges(10);
                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].BeatmapId, Is.EqualTo(beatmapId));
                Assert.That(changes[0].SetId, Is.EqualTo(BmsChartIdentity.CreateSetId(folderPath)));
                Assert.That(changes[0].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Upsert));

                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM meta WHERE key = 'schema_version';";
                Assert.That(command.ExecuteScalar(), Is.EqualTo("2"));
            }
            finally
            {
                cleanupTempDirectory(tempDir);
            }
        }

        private static void createVersion1Database(string dbPath, string chartPath, string folderPath, Guid beatmapId, string pathKey)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE charts (
    chart_path TEXT PRIMARY KEY,
    folder_path TEXT NOT NULL,
    file_name TEXT NOT NULL,
    file_size INTEGER NOT NULL,
    last_modified_ticks INTEGER NOT NULL,
    beatmap_id TEXT NOT NULL,
    path_key TEXT NOT NULL,
    title TEXT NOT NULL,
    sub_title TEXT NOT NULL,
    artist TEXT NOT NULL,
    sub_artist TEXT NOT NULL,
    genre TEXT NOT NULL,
    play_level INTEGER NOT NULL,
    rank INTEGER NOT NULL,
    ln_type INTEGER NOT NULL,
    key_count INTEGER NOT NULL,
    total_notes INTEGER NOT NULL,
    bpm REAL NOT NULL,
    min_bpm REAL NOT NULL,
    max_bpm REAL NOT NULL,
    duration REAL NOT NULL,
    total_gauge REAL NOT NULL,
    preview_time INTEGER NOT NULL,
    audio_file TEXT,
    preview_file TEXT,
    has_scratch INTEGER NOT NULL,
    has_ln INTEGER NOT NULL,
    has_stop INTEGER NOT NULL,
    has_scroll INTEGER NOT NULL,
    has_bga INTEGER NOT NULL,
    keysound_files_json TEXT NOT NULL
);

INSERT INTO meta (key, value) VALUES ('schema_version', '1');

INSERT INTO charts (
    chart_path, folder_path, file_name, file_size, last_modified_ticks,
    beatmap_id, path_key, title, sub_title, artist, sub_artist, genre,
    play_level, rank, ln_type, key_count, total_notes,
    bpm, min_bpm, max_bpm, duration, total_gauge,
    preview_time, audio_file, preview_file,
    has_scratch, has_ln, has_stop, has_scroll, has_bga, keysound_files_json
) VALUES (
    $chartPath, $folderPath, 'chart.bms', 100, 0,
    $beatmapId, $pathKey, 'Title', '', 'Artist', '', '',
    1, 2, 1, 7, 100,
    120, 120, 120, 60, 100,
    0, NULL, NULL,
    0, 0, 0, 0, 0, '[]'
);";
            command.Parameters.AddWithValue("$chartPath", chartPath);
            command.Parameters.AddWithValue("$folderPath", folderPath);
            command.Parameters.AddWithValue("$beatmapId", beatmapId.ToString());
            command.Parameters.AddWithValue("$pathKey", pathKey);
            command.ExecuteNonQuery();
        }

        private static void cleanupTempDirectory(string tempDir)
        {
            try
            {
                SqliteConnection.ClearAllPools();
            }
            catch
            {
                // Best effort.
            }

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
