// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSBeatmapManagerSourceMapTest
    {
        [Test]
        public void TestSourceMapReadWhileCatalogRebuildDoesNotThrow()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-source-map-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var manager = new BMSBeatmapManager(tempDir);
                var index = new BmsLibraryIndexRepository(Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE));
                index.ImportFromLibraryCache(new BMSLibraryCache
                {
                    Songs =
                    {
                        new BMSSongCache
                        {
                            FolderPath = @"E:\bms\song-a",
                            Title = "Song A",
                            Artist = "Artist A",
                            LastModified = DateTime.UtcNow,
                            Charts =
                            {
                                new BMSChartCache
                                {
                                    FolderPath = @"E:\bms\song-a",
                                    FileName = "a.bms",
                                    Md5Hash = BmsPathKeys.ComputeChartPathKey(@"E:\bms\song-a\a.bms"),
                                    Title = "A",
                                    Artist = "AA",
                                    KeyCount = 7,
                                }
                            }
                        },
                        new BMSSongCache
                        {
                            FolderPath = @"E:\bms\song-b",
                            Title = "Song B",
                            Artist = "Artist B",
                            LastModified = DateTime.UtcNow,
                            Charts =
                            {
                                new BMSChartCache
                                {
                                    FolderPath = @"E:\bms\song-b",
                                    FileName = "b.bms",
                                    Md5Hash = BmsPathKeys.ComputeChartPathKey(@"E:\bms\song-b\b.bms"),
                                    Title = "B",
                                    Artist = "BB",
                                    KeyCount = 7,
                                }
                            }
                        }
                    }
                });
                manager.LoadCache();

                string pathKeyA = BmsPathKeys.ComputeChartPathKey(@"E:\bms\song-a\a.bms");
                string pathKeyB = BmsPathKeys.ComputeChartPathKey(@"E:\bms\song-b\b.bms");

                var rulesetInfo = new BMSRuleset().RulesetInfo;
                var errors = new ConcurrentQueue<Exception>();

                Parallel.For(0, 60, i =>
                {
                    try
                    {
                        manager.BuildVirtualBeatmapCatalog(rulesetInfo);
                        manager.TryGetSourceReferenceByHash(pathKeyA, out _);
                        manager.TryGetSourceReferenceByHash(pathKeyB, out _);
                        _ = manager.GetCurrentSourceMap().Count;
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });

                Assert.That(errors.Count, Is.EqualTo(0), errors.TryPeek(out var e) ? e.ToString() : string.Empty);
                Assert.That(manager.TryGetSourceReferenceByHash(pathKeyA, out _), Is.True);
                Assert.That(manager.TryGetSourceReferenceByHash(pathKeyB, out _), Is.True);
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
    }
}
