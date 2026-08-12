// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace osu.Game.Tests.Beatmaps.IO
{
    [TestFixture]
    public class ZipArchiveReaderEncodingTest
    {
        private static readonly Encoding shift_jis = Encoding.GetEncoding(932);

        [OneTimeSetUp]
        public void OneTimeSetUp() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        [Test]
        public void TestDecodeShiftJisBytesAsDefault()
        {
            // 見本 in Shift-JIS — not valid UTF-8, so CP932 fallback must win.
            byte[] bytes = shift_jis.GetBytes("見本.wav");

            string decoded = ZipArchiveReader.DecodeEntryName(bytes, 0, bytes.Length, EncodingType.Default);

            Assert.That(decoded, Is.EqualTo("見本.wav"));
        }

        [Test]
        public void TestDecodeUtf8BytesWithoutEfsFlag()
        {
            // Modern packagers may store UTF-8 names without setting the language encoding bit.
            byte[] bytes = Encoding.UTF8.GetBytes("한글곡.mp3");

            string decoded = ZipArchiveReader.DecodeEntryName(bytes, 0, bytes.Length, EncodingType.Default);

            Assert.That(decoded, Is.EqualTo("한글곡.mp3"));
        }

        [Test]
        public void TestDecodeUtf8BytesWithEfsFlag()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("日本語.mp3");

            string decoded = ZipArchiveReader.DecodeEntryName(bytes, 0, bytes.Length, EncodingType.UTF8);

            Assert.That(decoded, Is.EqualTo("日本語.mp3"));
        }

        [Test]
        public void TestZipRoundTripShiftJisFilenames()
        {
            const string filename = "見本.wav";

            using var zipStream = createZipWithEncoding(filename, ZipArchiveReader.DEFAULT_ENCODING);
            using var reader = new ZipArchiveReader(zipStream);

            Assert.That(reader.Filenames.Single(), Is.EqualTo(filename));
        }

        [Test]
        public void TestZipRoundTripUtf8WithEfsFlag()
        {
            const string filename = "한글곡.mp3";

            var utf8Encoding = new ArchiveEncoding
            {
                Default = Encoding.UTF8,
                Password = Encoding.UTF8,
                UTF8 = Encoding.UTF8,
            };

            using var zipStream = createZipWithEncoding(filename, utf8Encoding);
            using var reader = new ZipArchiveReader(zipStream);

            Assert.That(reader.Filenames.Single(), Is.EqualTo(filename));
        }

        [Test]
        public void TestZipUtf8FilenameWithoutEfsFlag()
        {
            const string filename = "한글곡.mp3";

            using var zipStream = createStoredZipWithoutEfs(filename, Encoding.UTF8.GetBytes(filename), "audio"u8.ToArray());
            using var reader = new ZipArchiveReader(zipStream);

            Assert.That(reader.Filenames.Single(), Is.EqualTo(filename));
        }

        [Test]
        public void TestLegacyForcedCp932WouldMojibakeUtf8WithoutHeuristic()
        {
            // Documents the original failure mode: UTF-8 bytes forced through CP932.
            byte[] utf8Bytes = Encoding.UTF8.GetBytes("한글.mp3");
            string mojibake = shift_jis.GetString(utf8Bytes);

            Assert.That(mojibake, Is.Not.EqualTo("한글.mp3"));
            Assert.That(ZipArchiveReader.DecodeEntryName(utf8Bytes, 0, utf8Bytes.Length, EncodingType.Default),
                Is.EqualTo("한글.mp3"));
        }

        private static MemoryStream createZipWithEncoding(string filename, ArchiveEncoding encoding)
        {
            var stream = new MemoryStream();

            using (var writer = new ZipWriter(stream, new ZipWriterOptions(CompressionType.None)
                   {
                       LeaveStreamOpen = true,
                       ArchiveEncoding = encoding,
                   }))
            {
                using var content = new MemoryStream("content"u8.ToArray());
                writer.Write(filename, content);
            }

            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Build a minimal store-method zip whose local/central headers carry UTF-8 filename bytes with EFS cleared.
        /// </summary>
        private static MemoryStream createStoredZipWithoutEfs(string displayName, byte[] filenameBytes, byte[] content)
        {
            var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            uint crc = crc32(content);
            ushort nameLength = (ushort)filenameBytes.Length;
            ushort flags = 0; // EFS deliberately clear
            ushort method = 0; // store
            uint dosTime = 0;

            long localHeaderOffset = stream.Position;

            // Local file header
            writer.Write(0x04034b50u);
            writer.Write((ushort)20); // version needed
            writer.Write(flags);
            writer.Write(method);
            writer.Write(dosTime);
            writer.Write(crc);
            writer.Write((uint)content.Length);
            writer.Write((uint)content.Length);
            writer.Write(nameLength);
            writer.Write((ushort)0); // extra length
            writer.Write(filenameBytes);
            writer.Write(content);

            long centralDirectoryOffset = stream.Position;

            // Central directory header
            writer.Write(0x02014b50u);
            writer.Write((ushort)20); // version made by
            writer.Write((ushort)20); // version needed
            writer.Write(flags);
            writer.Write(method);
            writer.Write(dosTime);
            writer.Write(crc);
            writer.Write((uint)content.Length);
            writer.Write((uint)content.Length);
            writer.Write(nameLength);
            writer.Write((ushort)0); // extra
            writer.Write((ushort)0); // comment
            writer.Write((ushort)0); // disk start
            writer.Write((ushort)0); // internal attr
            writer.Write(0u); // external attr
            writer.Write((uint)localHeaderOffset);
            writer.Write(filenameBytes);

            long centralDirectorySize = stream.Position - centralDirectoryOffset;

            // End of central directory
            writer.Write(0x06054b50u);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((uint)centralDirectorySize);
            writer.Write((uint)centralDirectoryOffset);
            writer.Write((ushort)0);

            stream.Position = 0;
            _ = displayName; // retained for call-site clarity
            return stream;
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
