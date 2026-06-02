// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSBeatmapDecoderLoadTest
    {
        [Test]
        public void TestDecodeMinimalChartProducesPlayableBmsBeatmap()
        {
            const string bms = """
                               #TITLE Unit Test Song
                               #ARTIST Unit Test Artist
                               #GENRE TEST
                               #BPM 150
                               #WAV01 kick.wav
                               #STOP01 192
                               #SCROLL 1.0
                               #00111:0100
                               #00109:0100
                               """;

            var decoder = new BMSBeatmapDecoder();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(bms));
            using var reader = new LineBufferedReader(stream);

            Beatmap beatmap = decoder.Decode(reader);

            Assert.That(beatmap, Is.TypeOf<BMSBeatmap>());
            Assert.That(beatmap.HitObjects.OfType<BMSHitObject>().Any(), Is.True, "decoded beatmap should contain at least one BMS hit object");
            Assert.That(beatmap.BeatmapInfo.Metadata.Title, Is.EqualTo("Unit Test Song"));
            Assert.That(beatmap.BeatmapInfo.Metadata.Artist, Is.EqualTo("Unit Test Artist"));
            Assert.That(beatmap.BeatmapInfo.Metadata.Tags, Does.Contain("bms"));
            Assert.That(beatmap.BeatmapInfo.Metadata.Tags, Does.Contain("stop"));
            Assert.That(beatmap.BeatmapInfo.Metadata.Tags, Does.Contain("scroll"));
        }

        [Test]
        public void TestDecodeTenPlusTwoChartCompactsToTwelveColumns()
        {
            const string bms = """
                               #TITLE 10+2
                               #WAV01 hit.wav
                               #00111:01
                               #00112:01
                               #00113:01
                               #00114:01
                               #00115:01
                               #00116:01
                               #00121:01
                               #00122:01
                               #00123:01
                               #00124:01
                               #00125:01
                               #00126:01
                               """;

            var decoder = new BMSBeatmapDecoder();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(bms));
            using var reader = new LineBufferedReader(stream);

            var beatmap = decoder.Decode(reader);
            int columns = beatmap.HitObjects.OfType<BMSHitObject>().Select(h => h.Column).DefaultIfEmpty(-1).Max() + 1;

            Assert.That(columns, Is.EqualTo(12));
        }
    }
}
