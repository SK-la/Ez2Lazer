// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;

namespace osu.Game.Rulesets.BMS.Tests.Raja
{
    [TestFixture]
    public class BmsFilterDatabaseDeltaTest
    {
        [Test]
        public void TestFilterDeltaUpsertAndDeleteMatchChangeCount()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-filter-delta-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string indexPath = Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE);

                var repository = new BmsLibraryIndexRepository(indexPath);
                var manager = new BMSBeatmapManager(tempDir);
                manager.LoadCache();

                BMSChartCache first = createChart(tempDir, "one.bms", 10);
                BMSChartCache second = createChart(tempDir, "two.bms", 20);
                Guid firstId = BmsChartIdentity.CreateBeatmapId(first.FullPath);
                Guid secondId = BmsChartIdentity.CreateBeatmapId(second.FullPath);
                repository.UpsertSong(new BMSSongCache { FolderPath = first.FolderPath, Title = "Song" });
                repository.UpsertChart(first, firstId, BmsPathKeys.ComputeChartPathKey(first.FullPath));
                repository.UpsertChart(second, secondId, BmsPathKeys.ComputeChartPathKey(second.FullPath));

                // Realm cursor catches up so filter is the remaining consumer.
                var pending = repository.GetPendingSyncChanges(10);
                repository.AcknowledgeSyncChanges(pending.Select(change => change.Revision).ToList());

                Assert.That(manager.PendingFilterSyncChangeCount, Is.EqualTo(2));
                Assert.That(manager.GetPendingFilterSyncChanges(10), Has.Count.EqualTo(2));

                manager.AcknowledgeFilterSyncChanges(manager.GetPendingFilterSyncChanges(10).Select(c => c.Revision).ToList());
                Assert.That(manager.PendingFilterSyncChangeCount, Is.EqualTo(0));
                Assert.That(manager.FilterSyncCursor, Is.GreaterThan(0));

                // New chart creates exactly one pending filter change.
                BMSChartCache third = createChart(tempDir, "three.bms", 30);
                Guid thirdId = BmsChartIdentity.CreateBeatmapId(third.FullPath);
                repository.UpsertChart(third, thirdId, BmsPathKeys.ComputeChartPathKey(third.FullPath));
                Assert.That(manager.PendingFilterSyncChangeCount, Is.EqualTo(1));
                Assert.That(manager.GetPendingFilterSyncChanges(10).Single().BeatmapId, Is.EqualTo(thirdId));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); }
                catch
                {
                    // ignore
                }
            }
        }

        private static BMSChartCache createChart(string folder, string fileName, long size)
        {
            return new BMSChartCache
            {
                FolderPath = folder,
                FileName = fileName,
                FileSize = size,
                LastModified = DateTime.UtcNow,
                Title = fileName,
                Artist = "Artist",
                KeyCount = 7,
                TotalNotes = 100,
                Bpm = 140,
            };
        }
    }
}
