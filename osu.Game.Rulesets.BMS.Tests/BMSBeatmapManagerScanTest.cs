// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSBeatmapManagerScanTest
    {
        [Test]
        public async Task TestStreamingScanDeltasAndIncompleteRoots()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-manager-scan-{Guid.NewGuid():N}");
            string storagePath = Path.Combine(tempDir, "storage");
            string firstRoot = Path.Combine(tempDir, "first");
            string secondRoot = Path.Combine(tempDir, "second");
            Directory.CreateDirectory(storagePath);
            Directory.CreateDirectory(firstRoot);
            Directory.CreateDirectory(secondRoot);

            try
            {
                string firstChart = createBmsFile(firstRoot, "song-a", "a.bms", "First");
                string secondChart = createBmsFile(secondRoot, "song-b", "b.bms", "Second");
                var manager = new BMSBeatmapManager(storagePath);
                var repository = new BmsLibraryIndexRepository(Path.Combine(storagePath, BmsStoragePaths.INDEX_DATABASE_FILE));

                await manager.ScanLibraryAsync(new[] { firstRoot, secondRoot }).ConfigureAwait(false);
                Assert.That(repository.ScanRevision, Is.EqualTo(1));
                Assert.That(repository.ChartCount, Is.EqualTo(2));
                Assert.That(repository.GetPendingSyncChanges(20), Has.Count.EqualTo(2));

                await manager.ScanLibraryAsync(new[] { firstRoot, secondRoot }).ConfigureAwait(false);
                Assert.That(repository.ScanRevision, Is.EqualTo(2));
                Assert.That(repository.GetPendingSyncChanges(20), Has.Count.EqualTo(2));

                File.AppendAllText(firstChart, Environment.NewLine + "#GENRE Changed");
                File.Delete(secondChart);
                await manager.ScanLibraryAsync(new[] { firstRoot, secondRoot }).ConfigureAwait(false);

                Assert.That(repository.ScanRevision, Is.EqualTo(3));
                Assert.That(repository.ChartCount, Is.EqualTo(1));
                var deltaChanges = repository.GetPendingSyncChanges(20);
                Assert.That(deltaChanges, Has.Count.EqualTo(4));
                Assert.That(deltaChanges[^2].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Upsert));
                Assert.That(deltaChanges[^1].Kind, Is.EqualTo(BmsLibraryIndexRepository.SyncChangeKind.Delete));

                string offlineChart = createBmsFile(secondRoot, "song-c", "c.bms", "Offline");
                await manager.ScanLibraryAsync(new[] { firstRoot, secondRoot }).ConfigureAwait(false);
                Assert.That(repository.ScanRevision, Is.EqualTo(4));
                Assert.That(repository.ChartCount, Is.EqualTo(2));

                Directory.Delete(secondRoot, true);
                await manager.ScanLibraryAsync(new[] { firstRoot, secondRoot }).ConfigureAwait(false);
                Assert.That(repository.ScanRevision, Is.EqualTo(4));
                Assert.That(repository.ChartCount, Is.EqualTo(2));
                Assert.That(repository.TryGetChart(BmsChartIdentity.CreateBeatmapId(offlineChart), out _), Is.True);

                File.Delete(firstChart);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                await manager.ScanLibraryAsync(firstRoot, cancellation.Token).ConfigureAwait(false);

                Assert.That(repository.ScanRevision, Is.EqualTo(4));
                Assert.That(repository.ChartCount, Is.EqualTo(2));
                Assert.That(repository.TryGetChart(BmsChartIdentity.CreateBeatmapId(firstChart), out _), Is.True);
            }
            finally
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

        private static string createBmsFile(string rootPath, string folderName, string fileName, string title)
        {
            string folderPath = Path.Combine(rootPath, folderName);
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, fileName);
            File.WriteAllText(filePath, $"""
                                         #TITLE {title}
                                         #ARTIST Test
                                         #BPM 120
                                         #PLAYLEVEL 1
                                         #00111:0100
                                         """);
            return filePath;
        }
    }
}
