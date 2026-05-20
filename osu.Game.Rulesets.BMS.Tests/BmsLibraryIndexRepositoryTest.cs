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

                var snapshots = repository.GetChartSnapshots();
                Assert.That(snapshots.ContainsKey(chartPath), Is.True);
                Assert.That(repository.TryLoadChart(chartPath, out _), Is.True);
                Assert.That(repository.TryGetSourceReference(beatmapId, out var reference), Is.True);
                Assert.That(reference.ChartPath, Is.EqualTo(chartPath));

                int deleted = repository.DeleteChartsNotIn(new List<string>());
                Assert.That(deleted, Is.EqualTo(1));
                Assert.That(repository.TryLoadChart(chartPath, out _), Is.False);
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
