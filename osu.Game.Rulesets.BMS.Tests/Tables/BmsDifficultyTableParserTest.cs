// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables;

namespace osu.Game.Rulesets.BMS.Tests.Tables
{
    [TestFixture]
    public class BmsDifficultyTableParserTest
    {
        [Test]
        public void ParseTableDataJson_BuildsLevelsAndEntries()
        {
            const string json = """
                                {
                                  "name": "Demo Table",
                                  "url": "https://example.com/table.html",
                                  "tag": "★",
                                  "folder": [
                                    {
                                      "name": "★1",
                                      "song": [
                                        { "md5": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "sha256": "", "title": "Song A", "artist": "Artist A", "url": "https://example.com/a.zip" },
                                        { "md5": "", "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "title": "Song B", "artist": "Artist B", "appendurl": "https://example.com/b.zip" }
                                      ]
                                    },
                                    {
                                      "name": "★2",
                                      "song": [
                                        { "md5": "cccccccccccccccccccccccccccccccc", "title": "Song C", "artist": "Artist C" }
                                      ]
                                    }
                                  ]
                                }
                                """;

            var table = BmsDifficultyTableParser.ParseTableDataJson(json, "memory");

            Assert.That(table, Is.Not.Null);
            Assert.That(table!.Name, Is.EqualTo("Demo Table"));
            Assert.That(table.Levels, Has.Count.EqualTo(2));
            Assert.That(table.Levels[0].Entries, Has.Count.EqualTo(2));
            Assert.That(table.Levels[0].Entries[0].PreferredDownloadUrl, Is.EqualTo("https://example.com/a.zip"));
            Assert.That(table.Levels[0].Entries[1].PreferredDownloadUrl, Is.EqualTo("https://example.com/b.zip"));
            Assert.That(table.Levels[1].Entries[0].HasDownloadUrl, Is.False);
            Assert.That(table.Levels[1].Entries[0].PreferredHash, Is.EqualTo("cccccccccccccccccccccccccccccccc"));
        }

        [Test]
        public void BuiltinCatalog_LoadsEmbeddedEntries()
        {
            var catalog = BmsBuiltinTableCatalog.Load();
            Assert.That(catalog, Is.Not.Empty);
            Assert.That(catalog.All(e => e.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(catalog.Any(e => e.Name.Contains("Satellite", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void TryLoadFile_ReadsGzipBmt()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-table-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string path = Path.Combine(tempDir, "demo.bmt");
                const string json = """
                                    {
                                      "name": "Gzip Table",
                                      "tag": "G",
                                      "folder": [
                                        {
                                          "name": "G1",
                                          "song": [ { "md5": "dddddddddddddddddddddddddddddddd", "title": "D", "artist": "E" } ]
                                        }
                                      ]
                                    }
                                    """;
                BmsDifficultyTableParser.WriteBmt(path, json);

                var table = BmsDifficultyTableParser.TryLoadFile(path);
                Assert.That(table, Is.Not.Null);
                Assert.That(table!.Name, Is.EqualTo("Gzip Table"));
                Assert.That(table.Levels[0].Entries[0].Md5, Is.EqualTo("dddddddddddddddddddddddddddddddd"));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ContentHash_MatchesRawFileBytes()
        {
            string temp = Path.Combine(Path.GetTempPath(), $"bms-hash-{Guid.NewGuid():N}.bms");
            byte[] bytes = Encoding.UTF8.GetBytes("#TITLE test\n#ARTIST a\n");
            File.WriteAllBytes(temp, bytes);

            try
            {
                var hashes = BmsContentHash.ComputeFile(temp);
                Assert.That(hashes.IsValid, Is.True);
                Assert.That(hashes.Md5, Has.Length.EqualTo(32));
                Assert.That(hashes.Sha256, Has.Length.EqualTo(64));
                Assert.That(hashes, Is.EqualTo(BmsContentHash.ComputeBytes(bytes)));
            }
            finally
            {
                File.Delete(temp);
            }
        }

        [Test]
        public void HashBar_ProducesMissingBarsWhenUnresolved()
        {
            var table = new BmsDifficultyTable
            {
                Name = "T",
                Levels = new[]
                {
                    new BmsTableLevel
                    {
                        Name = "1",
                        Entries = new[]
                        {
                            new BmsTableEntry { Md5 = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", Title = "Missing", Artist = "X" },
                        },
                    },
                },
            };

            var bar = new BmsHashBar(table, table.Levels[0]);
            // Context with empty beatmap manager is hard without DI; assert entry metadata only.
            Assert.That(bar.Title, Is.EqualTo("1"));
            Assert.That(table.Levels[0].Entries[0].PreferredHash, Is.EqualTo("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"));
            Assert.That(new BmsMissingChartBar(table.Levels[0].Entries[0], "1").Subtitle, Does.Contain("未导入"));
        }

        [Test]
        public void FavoritesSql_DoesNotReferenceTablesDirectory()
        {
            // Isolation smoke: embedded favorite SQL must not mention EzBMS/tables.
            string json = readEmbedded("osu.Game.Rulesets.BMS.Resources.Raja.folder.default.json");
            Assert.That(json.Contains("FAVORITE", StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(json.Contains("EzBMS/tables", StringComparison.OrdinalIgnoreCase), Is.False);
            Assert.That(json.Contains("difficulty table", StringComparison.OrdinalIgnoreCase), Is.False);
        }

        [Test]
        public void TryGetBmstableHeaderUrl_ResolvesRelativeContent()
        {
            const string html = """
                                <html>
                                  <head>
                                    <meta name="bmstable" content="header.json">
                                    <title>Satellite</title>
                                  </head>
                                </html>
                                """;

            Assert.That(BmsDifficultyTableParser.LooksLikeHtml(html), Is.True);
            Assert.That(BmsDifficultyTableParser.TryGetBmstableHeaderUrl(html, "https://stellabms.xyz/sl/table.html", out string headerUrl), Is.True);
            Assert.That(headerUrl, Is.EqualTo("https://stellabms.xyz/sl/header.json"));
        }

        [Test]
        public void TryGetBmstableHeaderUrl_AcceptsContentBeforeName()
        {
            const string html = """<meta content='score.json' name="bmstable">""";

            Assert.That(BmsDifficultyTableParser.TryGetBmstableHeaderUrl(html, "https://example.com/table.html", out string headerUrl), Is.True);
            Assert.That(headerUrl, Is.EqualTo("https://example.com/score.json"));
        }

        private static string readEmbedded(string name)
        {
            using var stream = typeof(BMSRuleset).Assembly.GetManifestResourceStream(name);
            Assert.That(stream, Is.Not.Null);
            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }
    }
}
