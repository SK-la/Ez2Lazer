// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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
                setLibraryCache(manager, new BMSLibraryCache
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
                                    Md5Hash = "md5-a",
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
                                    Md5Hash = "md5-b",
                                    Title = "B",
                                    Artist = "BB",
                                    KeyCount = 7,
                                }
                            }
                        }
                    }
                });

                var rulesetInfo = new BMSRuleset().RulesetInfo;
                var errors = new ConcurrentQueue<Exception>();

                Parallel.For(0, 60, i =>
                {
                    try
                    {
                        manager.BuildVirtualBeatmapCatalog(rulesetInfo);
                        manager.TryGetSourceReferenceByHash("md5-a", out _);
                        manager.TryGetSourceReferenceByHash("md5-b", out _);
                        _ = manager.GetCurrentSourceMap().Count;
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });

                Assert.That(errors.Count, Is.EqualTo(0), errors.TryPeek(out var e) ? e.ToString() : string.Empty);
                Assert.That(manager.TryGetSourceReferenceByHash("md5-a", out _), Is.True);
                Assert.That(manager.TryGetSourceReferenceByHash("md5-b", out _), Is.True);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        private static void setLibraryCache(BMSBeatmapManager manager, BMSLibraryCache cache)
        {
            typeof(BMSBeatmapManager).GetField("<LibraryCache>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                                     .SetValue(manager, cache);
        }
    }
}
