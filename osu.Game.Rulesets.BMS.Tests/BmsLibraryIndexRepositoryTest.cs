// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                Assert.That(repository.ChartCount, Is.EqualTo(1));
                Assert.That(repository.GetRootPaths(), Contains.Item(@"D:\BMS"));
                Assert.That(repository.TryGetChartByPathKey("legacy-md5", out _), Is.True);
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
                const string folder_path = @"D:\BMS\song";
                string chartPath = Path.Combine(folder_path, "chart.bms");
                Guid beatmapId = Guid.NewGuid();
                const string path_key = "existing-path-key";

                createVersion1Database(dbPath, chartPath, folder_path, beatmapId, path_key);

                var repository = new BmsLibraryIndexRepository(dbPath);
                Assert.That(repository.ChartCount, Is.EqualTo(1));
                Assert.That(repository.SongCount, Is.EqualTo(0));
                Assert.That(repository.TryGetChart(beatmapId, out var chart), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(chart.Identity.BeatmapId, Is.EqualTo(beatmapId));
                    Assert.That(chart.Identity.PathKey, Is.EqualTo(path_key));
                    Assert.That(chart.Identity.SetId, Is.EqualTo(BmsChartIdentity.CreateSetId(folder_path)));
                    Assert.That(chart.SyncState, Is.EqualTo(BmsLibraryIndexRepository.SyncState.Pending));
                    Assert.That(chart.ParseVersion, Is.EqualTo(1));
                });

                var changes = repository.GetPendingSyncChanges(10);
                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].BeatmapId, Is.EqualTo(beatmapId));
                Assert.That(changes[0].SetId, Is.EqualTo(BmsChartIdentity.CreateSetId(folder_path)));
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

        [Test]
        public void TestGenerationWritesOnlyDeltasAndCompletesRevision()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-index-generation-{Guid.NewGuid():N}");
            string rootPath = Path.Combine(tempDir, "library");
            string songPath = Path.Combine(rootPath, "song");
            Directory.CreateDirectory(songPath);

            try
            {
                var repository = new BmsLibraryIndexRepository(Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE));
                BMSChartCache first = createChart(songPath, "a.bms", 100);

                long firstGeneration = repository.BeginScanGeneration();

                using (BmsLibraryIndexRepository.ScanWriter writer = repository.OpenScanWriter(firstGeneration))
                {
                    writer.WriteBatch(new[]
                    {
                        createChangedItem(first),
                    });
                }

                Assert.That(repository.CompleteScanGeneration(firstGeneration, new[] { rootPath }), Is.EqualTo(1));
                Assert.That(repository.GetPendingSyncChanges(20), Has.Count.EqualTo(1));

                long unchangedGeneration = repository.BeginScanGeneration();

                using (BmsLibraryIndexRepository.ScanWriter writer = repository.OpenScanWriter(unchangedGeneration))
                {
                    writer.WriteBatch(new[]
                    {
                        new BmsLibraryIndexRepository.ScanWriteItem(
                            first.FullPath,
                            first.FileSize,
                            first.LastModified.ToUniversalTime().Ticks,
                            null,
                            null),
                    });
                }

                Assert.That(repository.CompleteScanGeneration(unchangedGeneration, new[] { rootPath }), Is.EqualTo(2));
                Assert.That(repository.GetPendingSyncChanges(20), Has.Count.EqualTo(1));
                Assert.That(repository.TryGetChart(BmsChartIdentity.CreateBeatmapId(first.FullPath), out var unchanged), Is.True);
                Assert.That(unchanged.SeenGeneration, Is.EqualTo(unchangedGeneration));

                first.FileSize = 101;
                var added = createChart(songPath, "b.bms", 200);
                long deltaGeneration = repository.BeginScanGeneration();

                using (BmsLibraryIndexRepository.ScanWriter writer = repository.OpenScanWriter(deltaGeneration))
                {
                    writer.WriteBatch(new[]
                    {
                        createChangedItem(first),
                        createChangedItem(added),
                    });
                }

                Assert.That(repository.CompleteScanGeneration(deltaGeneration, new[] { rootPath }), Is.EqualTo(3));
                Assert.That(repository.ChartCount, Is.EqualTo(2));
                Assert.That(repository.GetPendingSyncChanges(20), Has.Count.EqualTo(3));

                long deleteGeneration = repository.BeginScanGeneration();

                using (BmsLibraryIndexRepository.ScanWriter writer = repository.OpenScanWriter(deleteGeneration))
                {
                    writer.WriteBatch(new[]
                    {
                        new BmsLibraryIndexRepository.ScanWriteItem(
                            first.FullPath,
                            first.FileSize,
                            first.LastModified.ToUniversalTime().Ticks,
                            null,
                            null),
                    });
                }

                Assert.That(repository.CompleteScanGeneration(deleteGeneration, new[] { rootPath }), Is.EqualTo(4));
                Assert.That(repository.ChartCount, Is.EqualTo(1));
                var changes = repository.GetPendingSyncChanges(20);
                Assert.That(changes, Has.Count.EqualTo(4));
                Assert.That(changes[^1].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Delete));

                long incompleteGeneration = repository.BeginScanGeneration();
                BMSChartCache partial = createChart(songPath, "partial.bms", 300);

                using (BmsLibraryIndexRepository.ScanWriter writer = repository.OpenScanWriter(incompleteGeneration))
                    writer.WriteBatch(new[] { createChangedItem(partial) });

                Assert.That(repository.ScanRevision, Is.EqualTo(4));
                Assert.That(repository.ChartCount, Is.EqualTo(2));
                Assert.That(repository.TryGetChart(BmsChartIdentity.CreateBeatmapId(first.FullPath), out _), Is.True);
            }
            finally
            {
                cleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public void TestAcknowledgeSyncChangesIsExactAndIdempotent()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-index-ack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var repository = new BmsLibraryIndexRepository(Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE));
                BMSChartCache first = createChart(tempDir, "a.bms", 100);
                BMSChartCache second = createChart(tempDir, "b.bms", 200);
                repository.UpsertChart(first, BmsChartIdentity.CreateBeatmapId(first.FullPath), BmsPathKeys.ComputeChartPathKey(first.FullPath));
                repository.UpsertChart(second, BmsChartIdentity.CreateBeatmapId(second.FullPath), BmsPathKeys.ComputeChartPathKey(second.FullPath));

                IReadOnlyList<BmsLibraryIndexRepository.SyncChange> changes = repository.GetPendingSyncChangesForSets(10);
                Assert.That(changes, Has.Count.EqualTo(2));

                repository.AcknowledgeSyncChanges(new[] { changes[1].Revision });
                Assert.That(repository.GetPendingSyncChanges(10).Single().Revision, Is.EqualTo(changes[0].Revision));
                Assert.That(repository.SyncCursor, Is.EqualTo(changes[0].Revision - 1));
                Assert.That(repository.TryGetChart(BmsChartIdentity.CreateBeatmapId(second.FullPath), out var acknowledged), Is.True);
                Assert.That(acknowledged.SyncState, Is.EqualTo(BmsLibraryIndexRepository.SyncState.Synchronized));

                repository.AcknowledgeSyncChanges(new[] { changes[0].Revision });
                repository.AcknowledgeSyncChanges(new[] { changes[0].Revision });

                Assert.That(repository.GetPendingSyncChanges(10), Is.Empty);
                Assert.That(repository.SyncCursor, Is.EqualTo(changes[1].Revision));
                Assert.That(repository.TryGetChart(BmsChartIdentity.CreateBeatmapId(first.FullPath), out acknowledged), Is.True);
                Assert.That(acknowledged.SyncState, Is.EqualTo(BmsLibraryIndexRepository.SyncState.Synchronized));
            }
            finally
            {
                cleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public void TestHundredThousandChartKeysetPagesStayBoundedAndStable()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-index-keyset-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string dbPath = Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE);
                var repository = new BmsLibraryIndexRepository(dbPath);
                Assert.That(repository.ChartCount, Is.Zero);

                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
WITH RECURSIVE charts_to_add(value) AS (
    SELECT 1
    UNION ALL
    SELECT value + 1 FROM charts_to_add WHERE value < 100000
)
INSERT INTO charts (
    chart_path, folder_path, file_name, file_size, last_modified_ticks,
    beatmap_id, set_id, path_key, title, sub_title, artist, sub_artist, genre,
    play_level, rank, ln_type, key_count, total_notes, bpm, min_bpm, max_bpm,
    duration, total_gauge, preview_time, audio_file, preview_file,
    has_scratch, has_ln, has_stop, has_scroll, has_bga, keysound_files_json)
SELECT
    'E:\bms\bulk\' || printf('%06d.bms', value),
    'E:\bms\bulk',
    printf('%06d.bms', value),
    1, 0,
    printf('00000000-0000-0000-0000-%012d', value),
    '10000000-0000-0000-0000-000000000000',
    printf('%064d', value),
    CASE WHEN value % 1000 = 0 THEN 'needle ' ELSE 'title ' END || printf('%06d', value),
    '', 'artist', '', '',
    value % 20, 0, 0, CASE WHEN value % 2 = 0 THEN 7 ELSE 14 END,
    100, 140, 140, 140, 0, 0, -1, NULL, NULL, 0, 0, 0, 0, 0, '[]'
FROM charts_to_add;";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = @"
INSERT INTO songs (folder_path, title, artist, genre, last_modified_ticks) VALUES
('E:\bms\bulk\a', 'a', '', '', 0),
('E:\bms\bulk\b', 'b', '', '', 0),
('E:\bms\bulk\c\nested', 'c', '', '', 0);";
                    cmd.ExecuteNonQuery();
                }

                var query = new BmsChartQuery(FolderPath: @"E:\bms\bulk", Sort: BmsChartSort.Title);
                BmsChartSummaryPage first = repository.GetChartSummaries(query, null, 150);
                BmsChartSummaryPage second = repository.GetChartSummaries(query, first.NextCursor, 150);

                Assert.Multiple(() =>
                {
                    Assert.That(repository.ChartCount, Is.EqualTo(100000));
                    Assert.That(first.Items, Has.Count.EqualTo(150));
                    Assert.That(second.Items, Has.Count.EqualTo(150));
                    Assert.That(first.Items.Select(item => item.BeatmapId).Intersect(second.Items.Select(item => item.BeatmapId)), Is.Empty);
                    Assert.That(first.Items[^1].Title, Is.LessThan(second.Items[0].Title));
                });

                BmsChartSummaryPage filtered = repository.GetChartSummaries(
                    new BmsChartQuery(SearchText: "needle", KeyCounts: new[] { 7 }, Sort: BmsChartSort.Title),
                    null,
                    25);
                BmsChartSummary? wrappedRandom = repository.GetRandomChartSummary(
                    new BmsChartQuery(SearchText: "needle", KeyCounts: new[] { 7 }),
                    ulong.MaxValue);

                Assert.Multiple(() =>
                {
                    Assert.That(filtered.Items, Has.Count.EqualTo(25));
                    Assert.That(filtered.Items, Has.All.Matches<BmsChartSummary>(item => item.Title.Contains("needle", StringComparison.OrdinalIgnoreCase)));
                    Assert.That(filtered.Items, Has.All.Matches<BmsChartSummary>(item => item.KeyCount == 7));
                    Assert.That(repository.GetChildFolders(@"E:\bms\bulk", null, 2), Has.Count.EqualTo(2));
                    Assert.That(wrappedRandom, Is.Not.Null);
                    Assert.That(wrappedRandom!.FileName, Is.EqualTo("001000.bms"));
                    Assert.That(wrappedRandom.Title, Does.Contain("needle"));
                    Assert.That(wrappedRandom.KeyCount, Is.EqualTo(7));
                    Assert.That(() => repository.GetChartSummaries(query, null, 201), Throws.TypeOf<ArgumentOutOfRangeException>());
                });
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup failures on CI.
                }
            }
        }

        private static BMSChartCache createChart(string folderPath, string fileName, long fileSize)
        {
            return new BMSChartCache
            {
                FolderPath = folderPath,
                FileName = fileName,
                FileSize = fileSize,
                LastModified = DateTime.UtcNow,
                Title = fileName,
                Artist = "Artist",
                KeyCount = 7,
                TotalNotes = 10,
                Bpm = 140,
            };
        }

        private static BmsLibraryIndexRepository.ScanWriteItem createChangedItem(BMSChartCache chart)
        {
            return new BmsLibraryIndexRepository.ScanWriteItem(
                chart.FullPath,
                chart.FileSize,
                chart.LastModified.ToUniversalTime().Ticks,
                chart,
                new BMSSongCache
                {
                    FolderPath = chart.FolderPath,
                    Title = chart.Title,
                    Artist = chart.Artist,
                    LastModified = chart.LastModified,
                });
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
