// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class BeatmapImporterFilenameEncodingTests : RealmTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        [Test]
        public void TestImportUtf8AudioFilenameWithoutEfsFlag()
        {
            RunTestWithRealmAsync(async (realm, storage) =>
            {
                const string audio_filename = "한글곡.mp3";
                const string background_filename = "배경.jpg";

                var importer = new BeatmapImporter(storage, realm);
                using var store = new RealmRulesetStore(realm, storage);

                using var osz = createMinimalOszWithoutEfs(audio_filename, background_filename);
                var imported = await importer.Import(new ImportTask(osz, "utf8-filenames.osz"));

                ClassicAssert.NotNull(imported);
                imported!.PerformRead(set =>
                {
                    ClassicAssert.NotNull(set.GetFile(audio_filename),
                        $"Files: {string.Join(", ", set.Files.Select(f => f.Filename))}");
                    ClassicAssert.NotNull(set.GetFile(background_filename));

                    var beatmap = set.Beatmaps.Single();
                    ClassicAssert.AreEqual(audio_filename, beatmap.Metadata.AudioFile);
                    ClassicAssert.AreEqual(background_filename, beatmap.Metadata.BackgroundFile);
                });
            });
        }

        [Test]
        public void TestImportReconcilesMismatchedAudioViaUniqueExtension()
        {
            RunTestWithRealmAsync(async (realm, storage) =>
            {
                // Archive stores ASCII name; .osu still references Unicode. Unique .mp3 → rename to match.
                const string audio_reference = "한글곡.mp3";
                const string zip_audio_name = "audio.mp3";
                const string background = "bg.jpg";

                var importer = new BeatmapImporter(storage, realm);
                using var store = new RealmRulesetStore(realm, storage);

                using var osz = createMinimalOszWithZipWriter(audio_reference, background, zip_audio_name, background);
                var imported = await importer.Import(new ImportTask(osz, "unique-ext-reconcile.osz"));

                ClassicAssert.NotNull(imported);
                imported!.PerformRead(set =>
                {
                    ClassicAssert.NotNull(set.GetFile(audio_reference),
                        $"Expected rename to .osu audio reference. Files: {string.Join(", ", set.Files.Select(f => f.Filename))}");
                    ClassicAssert.IsNull(set.GetFile(zip_audio_name));
                    ClassicAssert.AreEqual(audio_reference, set.Beatmaps.Single().Metadata.AudioFile);
                });
            });
        }

        private static MemoryStream createMinimalOszWithoutEfs(string audioFilename, string backgroundFilename)
        {
            string osu = createOsuText(audioFilename, backgroundFilename);

            var entries = new List<(byte[] Name, byte[] Content)>
            {
                (Encoding.UTF8.GetBytes("test.osu"), Encoding.UTF8.GetBytes(osu)),
                (Encoding.UTF8.GetBytes(audioFilename), "audio"u8.ToArray()),
                (Encoding.UTF8.GetBytes(backgroundFilename), "bg"u8.ToArray()),
            };

            return createStoredZipWithoutEfs(entries);
        }

        private static MemoryStream createMinimalOszWithZipWriter(
            string osuAudioReference,
            string osuBackgroundReference,
            string zipAudioEntryName,
            string zipBackgroundEntryName)
        {
            string osu = createOsuText(osuAudioReference, osuBackgroundReference);
            var stream = new MemoryStream();

            using (var writer = new ZipWriter(stream, new ZipWriterOptions(CompressionType.None)
                   {
                       LeaveStreamOpen = true,
                       ArchiveEncoding = new ArchiveEncoding
                       {
                           Default = Encoding.UTF8,
                           Password = Encoding.UTF8,
                           UTF8 = Encoding.UTF8,
                       },
                   }))
            {
                writer.Write("test.osu", new MemoryStream(Encoding.UTF8.GetBytes(osu)));
                writer.Write(zipAudioEntryName, new MemoryStream("audio"u8.ToArray()));
                writer.Write(zipBackgroundEntryName, new MemoryStream("bg"u8.ToArray()));
            }

            stream.Position = 0;
            return stream;
        }

        private static string createOsuText(string audioFilename, string backgroundFilename) =>
            $@"osu file format v14
[General]
AudioFilename: {audioFilename}
Mode: 0

[Metadata]
Title:Test
TitleUnicode:Test
Artist:Test
ArtistUnicode:Test
Creator:Test
Version:Normal

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:8
ApproachRate:9
SliderMultiplier:1.4
SliderTickRate:1

[Events]
0,0,""{backgroundFilename}"",0,0

[TimingPoints]
0,500,4,2,0,100,1,0

[HitObjects]
256,192,0,1,0,0:0:0:0:
";

        private static MemoryStream createStoredZipWithoutEfs(IReadOnlyList<(byte[] Name, byte[] Content)> entries)
        {
            var stream = new MemoryStream();
            var centralDirectory = new MemoryStream();
            var localOffsets = new List<uint>(entries.Count);

            foreach (var (name, content) in entries)
            {
                localOffsets.Add((uint)stream.Position);
                writeLocalFileHeader(stream, name, content);
            }

            uint cdOffset = (uint)stream.Position;

            for (int i = 0; i < entries.Count; i++)
                writeCentralDirectoryHeader(centralDirectory, entries[i].Name, entries[i].Content, localOffsets[i]);

            centralDirectory.Position = 0;
            centralDirectory.CopyTo(stream);
            uint cdSize = (uint)centralDirectory.Length;

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0x06054b50u);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)entries.Count);
                writer.Write((ushort)entries.Count);
                writer.Write(cdSize);
                writer.Write(cdOffset);
                writer.Write((ushort)0);
            }

            stream.Position = 0;
            return stream;
        }

        private static void writeLocalFileHeader(Stream stream, byte[] filenameBytes, byte[] content)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            uint crc = crc32(content);

            writer.Write(0x04034b50u);
            writer.Write((ushort)20);
            writer.Write((ushort)0); // flags: EFS clear
            writer.Write((ushort)0); // store
            writer.Write(0u);
            writer.Write(crc);
            writer.Write((uint)content.Length);
            writer.Write((uint)content.Length);
            writer.Write((ushort)filenameBytes.Length);
            writer.Write((ushort)0);
            writer.Write(filenameBytes);
            writer.Write(content);
        }

        private static void writeCentralDirectoryHeader(Stream stream, byte[] filenameBytes, byte[] content, uint localHeaderOffset)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            uint crc = crc32(content);

            writer.Write(0x02014b50u);
            writer.Write((ushort)20);
            writer.Write((ushort)20);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(0u);
            writer.Write(crc);
            writer.Write((uint)content.Length);
            writer.Write((uint)content.Length);
            writer.Write((ushort)filenameBytes.Length);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(0u);
            writer.Write(localHeaderOffset);
            writer.Write(filenameBytes);
        }

        private static uint crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;

            foreach (byte b in data)
            {
                crc ^= b;

                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
            }

            return ~crc;
        }
    }
}
