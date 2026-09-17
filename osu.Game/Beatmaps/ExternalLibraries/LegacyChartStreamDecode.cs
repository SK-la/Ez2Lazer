// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Reflection;
using osu.Game.IO;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Re-decodes a legacy chart that is already being read through a <see cref="LineBufferedReader"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="LineBufferedReader"/> always constructs its reader with UTF-8, which corrupts ANSI/CJK
    /// chart text before a decoder ever sees it. This helper reaches for the underlying stream and
    /// applies <see cref="LegacyChartTextEncoding"/> so parsing operates on correctly decoded text.
    /// </remarks>
    public static class LegacyChartStreamDecode
    {
        private static readonly FieldInfo? stream_reader_field = typeof(LineBufferedReader)
            .GetField("streamReader", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Decoded chart text plus the on-disk location it came from, when known.
        /// </summary>
        public readonly struct Result
        {
            public Result(string text, string sourcePath, string songFolder)
            {
                Text = text;
                SourcePath = sourcePath;
                SongFolder = songFolder;
            }

            public string Text { get; }

            /// <summary>Absolute path of the chart when it could be resolved from a file-backed stream; otherwise empty.</summary>
            public string SourcePath { get; }

            /// <summary>Folder used for reference-resolution scoring; empty when unknown.</summary>
            public string SongFolder { get; }
        }

        /// <summary>
        /// Attempts to decode <paramref name="buffered"/> from its underlying stream.
        /// Returns <see langword="false"/> when the stream cannot be reached or is not seekable,
        /// in which case callers should fall back to reading <paramref name="buffered"/> as-is.
        /// </summary>
        public static bool TryDecode(LineBufferedReader buffered, out Result result)
        {
            result = default;

            Stream? baseStream = tryGetBaseStream(buffered);

            if (baseStream == null)
                return false;

            try
            {
                if (baseStream is FileStream fileStream && !string.IsNullOrEmpty(fileStream.Name) && File.Exists(fileStream.Name))
                {
                    string path = fileStream.Name;
                    string folder = Path.GetDirectoryName(path) ?? string.Empty;
                    result = new Result(LegacyChartTextEncoding.DecodeFile(path), path, folder);
                    return true;
                }

                if (!baseStream.CanSeek)
                    return false;

                long originalPosition = baseStream.Position;
                baseStream.Position = 0;

                byte[] bytes;

                try
                {
                    using var memory = new MemoryStream();
                    baseStream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
                finally
                {
                    try
                    {
                        baseStream.Position = originalPosition;
                    }
                    catch
                    {
                        // Stream may already be closed by a concurrent dispose; ignore.
                    }
                }

                if (bytes.Length == 0)
                    return false;

                const string memory_song_folder = "";
                result = new Result(LegacyChartTextEncoding.DecodeBytes(bytes, memory_song_folder), string.Empty, memory_song_folder);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Stream? tryGetBaseStream(LineBufferedReader buffered)
        {
            if (stream_reader_field == null)
                return null;

            try
            {
                if (stream_reader_field.GetValue(buffered) is not StreamReader streamReader)
                    return null;

                return streamReader.BaseStream;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Splits decoded chart text into lines for callers that parse line-by-line.
        /// </summary>
        public static string[] SplitLines(string text)
            => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}
