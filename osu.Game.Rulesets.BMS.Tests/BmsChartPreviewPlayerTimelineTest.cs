// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// <see cref="osu.Game.Rulesets.BMS.UI.SongSelect.BmsChartPreviewPlayer"/> walks the decoded chart and
    /// builds its preview timeline from two sources: <see cref="BMSBeatmap.BackgroundSoundEvents"/>
    /// (BMS background channels) and the <see cref="ConvertHitObjectParser.FileHitSampleInfo"/> samples
    /// attached to playable note objects.
    ///
    /// These tests guard the contracts the player relies on, so refactors of the decoder/converter can't
    /// silently break preview-time keysound playback again. They don't exercise the audio pipeline itself
    /// (which needs a host) — only the data shape.
    /// </summary>
    [TestFixture]
    public class BmsChartPreviewPlayerTimelineTest
    {
        [Test]
        public void TestDecodedChartExposesBothBackgroundAndKeysoundSamples()
        {
            // Channel 11 is "player A, lane 1" (visible note); channel 01 is "BGM" (background).
            const string bms = """
                               #TITLE preview-timeline
                               #ARTIST test
                               #BPM 120
                               #WAV01 bgm.wav
                               #WAV02 hit.wav
                               #00101:01
                               #00111:0200
                               """;

            var decoder = new BMSBeatmapDecoder();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(bms));
            using var reader = new LineBufferedReader(stream);

            Beatmap beatmap = decoder.Decode(reader);

            Assert.That(beatmap, Is.TypeOf<BMSBeatmap>());

            var bmsBeatmap = (BMSBeatmap)beatmap;

            // BGM channel populated → preview player can collect background events.
            Assert.That(bmsBeatmap.BackgroundSoundEvents, Is.Not.Empty,
                "BMSBeatmap must expose a non-empty BackgroundSoundEvents list — the preview player relies on it for the BGM half of its timeline.");
            Assert.That(bmsBeatmap.BackgroundSoundEvents.Any(e => e.Filename == "bgm.wav"), Is.True);

            // Note samples are FileHitSampleInfo with the matching #WAVxx filename.
            var fileSamples = bmsBeatmap.HitObjects
                                        .SelectMany(h => h.Samples)
                                        .OfType<ConvertHitObjectParser.FileHitSampleInfo>()
                                        .ToList();

            Assert.That(fileSamples, Is.Not.Empty,
                "Hit objects must carry FileHitSampleInfo samples — the preview player extracts keysound filenames from them. " +
                "If this regresses, refer to BMSBeatmapDecoder where samples are attached to BMSNote/BMSHoldNote.");
            Assert.That(fileSamples.Any(s => s.Filename == "hit.wav"), Is.True);
        }

        [Test]
        public void TestFileHitSampleInfoLookupNamesIncludeBareFilename()
        {
            // BmsChartPreviewPlayer's FilenameSampleInfo stores the bare filename ("hit.wav") and BMSSkin
            // probes for it. We make sure FileHitSampleInfo's own LookupNames also expose the same form,
            // because that's how the in-game audio path resolves keysounds — the two paths must agree.
            var info = new ConvertHitObjectParser.FileHitSampleInfo("hit.wav", 100);

            Assert.That(info.LookupNames, Does.Contain("hit.wav"));
            Assert.That(info.LookupNames, Does.Contain("hit"), "LookupNames should also expose the extension-less form so BMSSkin can probe alternate audio extensions.");
        }
    }
}
