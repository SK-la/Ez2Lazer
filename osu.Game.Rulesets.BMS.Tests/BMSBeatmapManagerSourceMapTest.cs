// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Models;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;
using osu.Game.Rulesets.BMS.UI.SongSelect;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSBeatmapManagerIndexAccessTest
    {
        [Test]
        public void TestLoadCacheKeepsOnlyIndexSummaryAndSupportsPointReads()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-source-map-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var index = new BmsLibraryIndexRepository(Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE));
                const string folderPath = @"E:\bms\song-a";
                var cache = new BMSLibraryCache
                {
                    RootPaths = { @"E:\bms" },
                    Songs =
                    {
                        new BMSSongCache
                        {
                            FolderPath = folderPath,
                            Title = "Song A",
                            Artist = "Artist A",
                            LastModified = DateTime.UtcNow,
                        }
                    }
                };

                for (int i = 0; i < 256; i++)
                {
                    string fileName = $"{i:D4}.bms";
                    string chartPath = Path.Combine(folderPath, fileName);
                    cache.Songs[0].Charts.Add(new BMSChartCache
                    {
                        FolderPath = folderPath,
                        FileName = fileName,
                        Md5Hash = BmsPathKeys.ComputeChartPathKey(chartPath),
                        Title = $"Chart {i}",
                        Artist = "Artist",
                        KeyCount = 7,
                    });
                }

                index.ImportFromLibraryCache(cache);
                var manager = new BMSBeatmapManager(tempDir);
                manager.LoadCache();

                string selectedPath = Path.Combine(folderPath, "0128.bms");
                string selectedKey = BmsPathKeys.ComputeChartPathKey(selectedPath);
                Guid selectedId = BmsChartIdentity.CreateBeatmapId(selectedPath);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.ChartCount, Is.EqualTo(256));
                    Assert.That(manager.SongCount, Is.EqualTo(1));
                    Assert.That(manager.RootPaths, Is.EqualTo(new[] { @"E:\bms" }));
                    Assert.That(manager.TryGetSourceReference(selectedId, out var reference), Is.True);
                    Assert.That(reference.ChartPath, Is.EqualTo(selectedPath));
                    Assert.That(manager.TryGetChartByPathKey(selectedKey, out var chart), Is.True);
                    Assert.That(chart.FullPath, Is.EqualTo(selectedPath));
                    Assert.That(manager.GetChartPage(0, 16), Has.Count.EqualTo(16));
                });
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

        [Test]
        public void TestPlaySourcePrefersSafeRealmPathAndRejectsTraversal()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-source-path-test-{Guid.NewGuid():N}");
            string storagePath = Path.Combine(tempDir, "storage");
            string rootPath = Path.Combine(tempDir, "library");
            Directory.CreateDirectory(storagePath);
            Directory.CreateDirectory(rootPath);

            try
            {
                string chartPath = Path.Combine(rootPath, "chart.bms");
                File.WriteAllText(chartPath, "#TITLE Test");
                var chart = new BMSChartCache
                {
                    FolderPath = rootPath,
                    FileName = "chart.bms",
                    FileSize = new FileInfo(chartPath).Length,
                    LastModified = File.GetLastWriteTime(chartPath),
                };
                BmsChartIdentity identity = BmsChartIdentity.Create(chartPath, rootPath);
                var repository = new BmsLibraryIndexRepository(Path.Combine(storagePath, BmsStoragePaths.INDEX_DATABASE_FILE));
                repository.UpsertChart(chart, identity.BeatmapId, identity.PathKey);
                var manager = new BMSBeatmapManager(storagePath);
                manager.LoadCache();

                string realmHash = BmsPathKeys.ComputeRealmFileHash(chartPath);
                var set = new BeatmapSetInfo
                {
                    ID = identity.SetId,
                    Hash = ExternalBeatmapPathEncoding.Encode(rootPath),
                    ExternalContentRoot = rootPath,
                    HostingKind = BeatmapSetHostingKind.External,
                };
                var usage = new RealmNamedFileUsage(new RealmFile { Hash = realmHash }, "chart.bms");
                set.Files.Add(usage);
                var beatmap = new BeatmapInfo
                {
                    ID = identity.BeatmapId,
                    Hash = realmHash,
                    MD5Hash = identity.PathKey,
                    BeatmapSet = set,
                };
                set.Beatmaps.Add(beatmap);

                Assert.That(BmsSongSelectPlayHelper.TryResolveSource(manager, beatmap, out string resolved, out BMSChartCache? loaded), Is.True);
                Assert.That(resolved, Is.EqualTo(chartPath));
                Assert.That(loaded?.FullPath, Is.EqualTo(chartPath));

                File.WriteAllText(Path.Combine(tempDir, "outside.bms"), "#TITLE Outside");
                usage.Filename = "../outside.bms";

                Assert.That(BmsSongSelectPlayHelper.TryResolveSource(manager, beatmap, out resolved, out loaded), Is.True);
                Assert.That(resolved, Is.EqualTo(chartPath));
                Assert.That(loaded?.FullPath, Is.EqualTo(chartPath));
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
