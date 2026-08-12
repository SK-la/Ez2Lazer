// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Toolkit.HighPerformance;
using osu.Framework.Extensions;
using osu.Framework.IO.Stores;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SixLabors.ImageSharp.Memory;

namespace osu.Game.IO.Archives
{
    public sealed class ZipArchiveReader : ArchiveReader
    {
        /// <summary>
        /// Archives created by osu!stable still write out as Shift-JIS.
        /// We force CP932 as the non-UTF-8 fallback rather than leave it up to the library/system.
        /// When the zip EFS (UTF-8) flag is set, SharpCompress still selects UTF-8 via <see cref="EncodingType.UTF8"/>.
        /// Additionally, well-formed UTF-8 names without the EFS flag are recovered via <see cref="DecodeEntryName"/>.
        /// </summary>
        public static readonly ArchiveEncoding DEFAULT_ENCODING;

        private static readonly Encoding utf8_strict = Encoding.GetEncoding(
            "utf-8",
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);

        private static readonly Encoding shift_jis = Encoding.GetEncoding(932);

        private readonly Stream archiveStream;
        private readonly IWritableArchive archive;

        static ZipArchiveReader()
        {
            // Required to support rare code pages.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            DEFAULT_ENCODING = new ArchiveEncoding
            {
                Default = shift_jis,
                Password = shift_jis,
                UTF8 = Encoding.UTF8,
                CustomDecoder = DecodeEntryName,
            };
        }

        public ZipArchiveReader(Stream archiveStream, string name = null)
            : base(name)
        {
            this.archiveStream = archiveStream;

            archive = ZipArchive.OpenArchive(archiveStream, new ReaderOptions
            {
                ArchiveEncoding = DEFAULT_ENCODING
            });
        }

        /// <summary>
        /// Decode a zip entry name from raw header bytes.
        /// </summary>
        /// <remarks>
        /// Prefer UTF-8 when the EFS flag is set, or when the bytes are valid UTF-8 containing non-ASCII
        /// (common with modern packagers that omit the language encoding flag). Otherwise decode as CP932
        /// for osu!stable / Japanese archives.
        /// </remarks>
        public static string DecodeEntryName(byte[] bytes, int index, int count, EncodingType type)
        {
            if (type == EncodingType.UTF8)
                return Encoding.UTF8.GetString(bytes, index, count);

            if (tryDecodeUtf8(bytes, index, count, out string utf8) && utf8.Any(static c => c > 127))
                return utf8;

            return shift_jis.GetString(bytes, index, count);
        }

        private static bool tryDecodeUtf8(byte[] bytes, int index, int count, out string result)
        {
            try
            {
                result = utf8_strict.GetString(bytes, index, count);
                return true;
            }
            catch (DecoderFallbackException)
            {
                result = null;
                return false;
            }
        }

        public override Stream GetStream(string name)
        {
            IArchiveEntry entry = archive.Entries.SingleOrDefault(e => e.Key == name);
            if (entry == null)
                return null;

            using (Stream s = entry.OpenEntryStream())
            {
                if (entry.Size > 0)
                {
                    var owner = MemoryAllocator.Default.Allocate<byte>((int)entry.Size);
                    s.ReadExactly(owner.Memory.Span);
                    return new MemoryOwnerMemoryStream(owner);
                }

                // due to a sharpcompress bug (https://github.com/adamhathcock/sharpcompress/issues/88),
                // in rare instances the `ZipArchiveEntry` will not contain a correct `Size` but instead report 0.
                // this would lead to the block above reading nothing, and the game basically seeing an archive full of empty files.
                // since the bug is years old now, and this is a rather rare situation anyways (reported once in years),
                // work around this locally by falling back to reading as many bytes as possible and using a standard non-pooled memory stream.
                return new MemoryStream(s.ReadAllRemainingBytesToArray());
            }
        }

        public override void Dispose()
        {
            archive.Dispose();
            archiveStream.Dispose();
        }

        public override IEnumerable<string> Filenames => archive.Entries.Where(e => !e.IsDirectory).Select(e => e.Key).ExcludeSystemFileNames();

        private class MemoryOwnerMemoryStream : Stream
        {
            private readonly IMemoryOwner<byte> owner;
            private readonly Stream stream;

            public MemoryOwnerMemoryStream(IMemoryOwner<byte> owner)
            {
                this.owner = owner;

                stream = owner.Memory.AsStream();
            }

            protected override void Dispose(bool disposing)
            {
                owner?.Dispose();
                base.Dispose(disposing);
            }

            public override void Flush() => stream.Flush();

            public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

            public override void SetLength(long value) => stream.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

            public override bool CanRead => stream.CanRead;

            public override bool CanSeek => stream.CanSeek;

            public override bool CanWrite => stream.CanWrite;

            public override long Length => stream.Length;

            public override long Position
            {
                get => stream.Position;
                set => stream.Position = value;
            }
        }
    }
}
