// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
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

                BmsRealmSyncChange firstChange = manager.GetPendingRealmSyncChanges(10).Single(change => change.ChartPath == firstChart);
                Assert.That(manager.TryGetRealmSyncSet(firstChange.SetId, out BmsRealmSyncSet syncSet), Is.True);
                var targetSet = manager.BuildRealmSyncTarget(syncSet, new BMSRuleset().RulesetInfo);
                Assert.That(targetSet.ID, Is.EqualTo(BmsChartIdentity.CreateSetId(Path.GetDirectoryName(firstChart)!)));
                Assert.That(targetSet.Beatmaps.Single().ID, Is.EqualTo(BmsChartIdentity.CreateBeatmapId(firstChart)));

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

        [Test]
        public async Task TestScanReparsesChartsIndexedByOlderParsingLogic()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-manager-reparse-{Guid.NewGuid():N}");
            string storagePath = Path.Combine(tempDir, "storage");
            string root = Path.Combine(tempDir, "library");
            Directory.CreateDirectory(storagePath);
            Directory.CreateDirectory(root);

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                const string title = "东方红 幻想曲";
                string chartPath = createGbkBmsFile(root, "song-a", "a.bms", title);
                string dbPath = Path.Combine(storagePath, BmsStoragePaths.INDEX_DATABASE_FILE);
                var manager = new BMSBeatmapManager(storagePath);
                var repository = new BmsLibraryIndexRepository(dbPath);

                await manager.ScanLibraryAsync(new[] { root }).ConfigureAwait(false);
                Assert.That(repository.TryLoadChart(chartPath, out var indexed), Is.True);
                Assert.That(indexed.Title, Is.EqualTo(title));

                // Simulate an index written by the older parser: an untouched file with stale metadata.
                executeSql(dbPath, "UPDATE charts SET title = 'ｶｫｷｽｺ', parse_version = 1 WHERE chart_path = $path;", chartPath);

                await manager.ScanLibraryAsync(new[] { root }).ConfigureAwait(false);

                Assert.That(repository.TryLoadChart(chartPath, out var reparsed), Is.True);
                Assert.That(reparsed.Title, Is.EqualTo(title), "a chart indexed by older parsing logic must be re-parsed");
                Assert.That(readParseVersion(dbPath, chartPath), Is.EqualTo(BmsLibraryIndexRepository.CHART_PARSE_VERSION));

                // Charts already at the current parse version keep being skipped, so the forced re-parse is a
                // one-time cost rather than a full re-read on every scan.
                executeSql(dbPath, "UPDATE charts SET title = 'not-reparsed' WHERE chart_path = $path;", chartPath);

                await manager.ScanLibraryAsync(new[] { root }).ConfigureAwait(false);

                Assert.That(repository.TryLoadChart(chartPath, out var skipped), Is.True);
                Assert.That(skipped.Title, Is.EqualTo("not-reparsed"));
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

        private static void executeSql(string dbPath, string sql, string chartPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$path", chartPath);
            Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
        }

        private static int readParseVersion(string dbPath, string chartPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT parse_version FROM charts WHERE chart_path = $path;";
            command.Parameters.AddWithValue("$path", chartPath);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static string createGbkBmsFile(string rootPath, string folderName, string fileName, string title)
        {
            string folderPath = Path.Combine(rootPath, folderName);
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, fileName);
            string chart = $"#TITLE {title}\n#ARTIST 中文作者\n#BPM 120\n#PLAYLEVEL 1\n#WAV01 kick.wav\n#00111:0100\n";
            File.WriteAllBytes(filePath, Encoding.GetEncoding(936).GetBytes(chart));
            File.WriteAllBytes(Path.Combine(folderPath, "kick.wav"), new byte[] { 0 });
            return filePath;
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
