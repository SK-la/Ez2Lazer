// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.SongSelect;

namespace osu.Game.Rulesets.BMS.Tests.Analytics
{
    [TestFixture]
    public class BmsAnalyticsScanLifecycleTest
    {
        [Test]
        public void TestUnchangedFileVersionIsSkipped()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-analytics-skip-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string dbPath = Path.Combine(tempDir, BmsStoragePaths.ANALYTICS_DATABASE_FILE);
                var repository = new BmsAnalyticsSqliteRepository(dbPath);
                const string path_key = "chart-key";
                repository.Upsert(new BmsAnalyticsRecord
                {
                    PathKey = path_key,
                    Pp = 10,
                    FileSize = 123,
                    LastModifiedTicks = 456,
                });

                Assert.That(repository.IsUpToDate(path_key, 123, 456), Is.True);
                Assert.That(repository.IsUpToDate(path_key, 124, 456), Is.False);
                Assert.That(repository.IsUpToDate(path_key, 123, 457), Is.False);
                Assert.That(repository.IsUpToDate("missing", 123, 456), Is.False);
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

        [Test]
        public void TestUiBackgroundOperationCancelStopsCallbacks()
        {
            using var operation = new BmsUiBackgroundOperation();
            Assert.That(operation.IsCancelled, Is.False);
            operation.Cancel();
            Assert.That(operation.IsCancelled, Is.True);
            Assert.That(operation.Token.IsCancellationRequested, Is.True);
        }
    }
}
