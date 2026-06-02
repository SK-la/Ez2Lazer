// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;

namespace osu.Game.Rulesets.BMS.Tests.Raja
{
    [TestFixture]
    public class BmsRajaSqlQueryTest
    {
        [Test]
        public void TestClearTypePeakDensityAndPlaycountFilters()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-raja-sql-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string filterPath = Path.Combine(tempDir, BmsStoragePaths.FILTER_DATABASE_FILE);
                string indexPath = Path.Combine(tempDir, BmsStoragePaths.INDEX_DATABASE_FILE);

                var chart = new BMSChartCache
                {
                    FolderPath = @"E:\bms\sample",
                    FileName = "test.bms",
                    Title = "Test",
                    KeyCount = 7,
                    PlayLevel = 10,
                    TotalNotes = 100,
                    Duration = 120000,
                };

                var indexRepository = new BmsLibraryIndexRepository(indexPath);
                indexRepository.UpsertSong(new BMSSongCache { FolderPath = chart.FolderPath, Title = "Song" });
                indexRepository.UpsertChart(chart, Guid.NewGuid(), BmsPathKeys.ComputeChartPathKey(chart.FullPath));

                var manager = new BMSBeatmapManager(tempDir);
                manager.LoadCache();

                using (var connection = new SqliteConnection($"Data Source={filterPath}"))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
CREATE TABLE song (md5 TEXT, sha256 TEXT PRIMARY KEY, title TEXT, subtitle TEXT, genre TEXT, artist TEXT, subartist TEXT, path TEXT, folder TEXT, parent TEXT, level INTEGER, difficulty INTEGER, mode INTEGER, notes INTEGER, favorite INTEGER, maxbpm INTEGER, minbpm INTEGER, length INTEGER, date INTEGER, adddate INTEGER);
CREATE TABLE score (sha256 TEXT PRIMARY KEY, mode INTEGER, clear INTEGER, playcount INTEGER, clearcount INTEGER, epg INTEGER, lpg INTEGER, egr INTEGER, lgr INTEGER, egd INTEGER, lgd INTEGER, ebd INTEGER, lbd INTEGER, epr INTEGER, lpr INTEGER, ems INTEGER, lms INTEGER, notes INTEGER, combo INTEGER, minbp INTEGER, avgjudge INTEGER, date INTEGER);
CREATE TABLE scorelog (sha256 TEXT, mode INTEGER, clear INTEGER, playcount INTEGER, clearcount INTEGER, epg INTEGER, lpg INTEGER, egr INTEGER, lgr INTEGER, notes INTEGER, combo INTEGER, minbp INTEGER, date INTEGER);
CREATE TABLE information (sha256 TEXT PRIMARY KEY, n INTEGER, ln INTEGER, s INTEGER, ls INTEGER, total REAL, density REAL, peakdensity REAL, enddensity REAL, mainbpm REAL);";
                    cmd.ExecuteNonQuery();

                    string key = BmsPathKeys.ComputeChartPathKey(chart.FullPath);
                    cmd.CommandText = @"
INSERT INTO song (md5, sha256, title, subtitle, genre, artist, subartist, path, folder, parent, level, difficulty, mode, notes, favorite, maxbpm, minbpm, length, date, adddate)
VALUES ($md5, $sha, 'Test', '', '', '', '', 't.bms', $folder, 'p', 10, 2, 7, 100, 0, 140, 130, 120000, 0, 0);
INSERT INTO score (sha256, mode, clear, playcount, clearcount, epg, lpg, egr, lgr, egd, lgd, ebd, lbd, epr, lpr, ems, lms, notes, combo, minbp, avgjudge, date)
VALUES ($sha, 7, 5, 1, 1, 90, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100, 100, 0, 2147483647, 0);
INSERT INTO information (sha256, n, ln, s, ls, total, density, peakdensity, enddensity, mainbpm)
VALUES ($sha, 90, 10, 0, 0, 100, 12, 18, 10, 140);";
                    cmd.Parameters.AddWithValue("$md5", key[..32]);
                    cmd.Parameters.AddWithValue("$sha", key);
                    cmd.Parameters.AddWithValue("$folder", chart.FolderPath);
                    cmd.ExecuteNonQuery();
                }

                var query = new BmsSqlSongQuery(filterPath, manager);

                Assert.That(query.Execute("score.clear >= 5"), Has.Count.EqualTo(1));
                Assert.That(query.Execute("peakdensity >= 15 AND peakdensity < 20"), Has.Count.EqualTo(1));
                Assert.That(query.Execute("density >= 10 AND density < 15"), Has.Count.EqualTo(1));
                Assert.That(query.Execute("playcount = 0"), Is.Empty);
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
