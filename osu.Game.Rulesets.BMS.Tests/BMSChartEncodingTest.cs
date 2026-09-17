// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Legacy BMS charts are authored in the author's ANSI code page, so decoding must not assume UTF-8:
    /// a wrong guess corrupts the title and, worse, the <c>#WAV</c> filenames that have to match the files on disk.
    /// </summary>
    [TestFixture]
    public class BMSChartEncodingTest
    {
        private static readonly Encoding shift_jis = Encoding.GetEncoding(932);
        private static readonly Encoding gbk = Encoding.GetEncoding(936);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Test]
        public void TestShiftJisChartDecodesAndResolvesReferencedAudio()
        {
            const string chart = """
                                 #TITLE テスト楽曲
                                 #ARTIST 作曲者
                                 #GENRE 東方
                                 #BPM 150
                                 #WAV01 テスト音.wav
                                 #00111:01
                                 #00101:01
                                 """;

            string folder = createTempFolder();

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "テスト音.wav"), new byte[] { 0 });
                string path = Path.Combine(folder, "chart.bms");
                File.WriteAllBytes(path, shift_jis.GetBytes(chart));

                string decoded = BmsChartTextEncoding.DecodeFile(path);

                Assert.That(decoded, Does.Contain("#TITLE テスト楽曲"));
                Assert.That(decoded, Does.Contain("#ARTIST 作曲者"));

                string? wav = extractDefinition(decoded, "#WAV01");
                Assert.That(wav, Is.EqualTo("テスト音.wav"));
                Assert.That(BmsChartTextEncoding.ResolveExistingRelativePath(folder, wav), Is.Not.Null,
                    "the decoded filename must match the file on disk or the chart plays silently");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestGbkChartDecodesAndResolvesReferencedAudio()
        {
            const string chart = """
                                 #TITLE 东方红
                                 #ARTIST 测试
                                 #BPM 180
                                 #WAV01 打击音.wav
                                 #00111:01
                                 """;

            string folder = createTempFolder();

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "打击音.wav"), new byte[] { 0 });
                string path = Path.Combine(folder, "chart.bms");
                File.WriteAllBytes(path, gbk.GetBytes(chart));

                string decoded = BmsChartTextEncoding.DecodeFile(path);

                Assert.That(decoded, Does.Contain("#TITLE 东方红"));

                string? wav = extractDefinition(decoded, "#WAV01");
                Assert.That(wav, Is.EqualTo("打击音.wav"));
                Assert.That(BmsChartTextEncoding.ResolveExistingRelativePath(folder, wav), Is.Not.Null);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestUtf8BomChartDoesNotLeakByteOrderMarkIntoFirstDirective()
        {
            const string chart = "#TITLE Bom Song\n#WAV01 kick.wav\n#00111:01\n";

            string folder = createTempFolder();

            try
            {
                string path = Path.Combine(folder, "chart.bms");
                File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(chart)).ToArray());

                string decoded = BmsChartTextEncoding.DecodeFile(path);

                Assert.That(decoded, Does.Not.Contain('\uFEFF'));
                Assert.That(BmsChartTextEncoding.SplitLines(decoded)[0], Is.EqualTo("#TITLE Bom Song"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestDecoderReadsShiftJisMetadataFromStream()
        {
            const string chart = "#TITLE テスト楽曲\n#ARTIST 作曲者\n#WAV01 テスト音.wav\n#00111:01\n";

            using var stream = new MemoryStream(shift_jis.GetBytes(chart));
            using var reader = new LineBufferedReader(stream);

            var beatmap = new BMSBeatmapDecoder().Decode(reader);

            Assert.That(beatmap.BeatmapInfo.Metadata.Title, Is.EqualTo("テスト楽曲"));
            Assert.That(beatmap.BeatmapInfo.Metadata.Artist, Is.EqualTo("作曲者"));
        }

        [Test]
        public void TestChartExtensionsRecognised()
        {
            Assert.That(BmsChartTextEncoding.IsChartPath("song.bms"), Is.True);
            Assert.That(BmsChartTextEncoding.IsChartPath("song.BME"), Is.True);
            Assert.That(BmsChartTextEncoding.IsChartPath("song.pms"), Is.True);
            Assert.That(BmsChartTextEncoding.IsChartPath("song.osu"), Is.False);
            Assert.That(BmsChartTextEncoding.IsChartPath(null), Is.False);
        }

        private static string? extractDefinition(string chart, string directive)
        {
            return BmsChartTextEncoding.SplitLines(chart)
                                       .Select(line => line.Trim())
                                       .Where(line => line.StartsWith(directive + " ", StringComparison.OrdinalIgnoreCase))
                                       .Select(line => line.Substring(directive.Length + 1).Trim())
                                       .FirstOrDefault();
        }

        private static string createTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-encoding-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
