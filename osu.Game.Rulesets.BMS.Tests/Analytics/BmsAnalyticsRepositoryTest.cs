// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.Tests.Analytics
{
    [TestFixture]
    public class BmsAnalyticsRepositoryTest
    {
        [Test]
        public void TestUpsertAndLoad()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-analytics-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string dbPath = Path.Combine(tempDir, BmsStoragePaths.ANALYTICS_DATABASE_FILE);
                var repository = new BmsAnalyticsSqliteRepository(dbPath);

                const string path_key = "abc123";
                repository.Upsert(new BmsAnalyticsRecord
                {
                    PathKey = path_key,
                    Pp = 123.4,
                    XxySr = 5.67,
                    AvgKps = 8.1,
                    MaxKps = 12.3,
                    StarRating = 4.5,
                    ColumnCountsJson = "{\"0\":10}",
                });

                Assert.That(repository.TryGet(path_key, out var record), Is.True);
                Assert.That(record.Pp, Is.EqualTo(123.4).Within(0.01));
                Assert.That(record.XxySr, Is.EqualTo(5.67).Within(0.01));
                Assert.That(record.AvgKps, Is.EqualTo(8.1).Within(0.01));

                var all = repository.LoadAll();
                Assert.That(all.ContainsKey(path_key), Is.True);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); }
                catch
                {
                    /* ignore */
                }
            }
        }
    }
}
