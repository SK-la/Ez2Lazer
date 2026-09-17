// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps.ExternalLibraries;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// BMS-facing wrapper over <see cref="LegacyChartTextEncoding"/>.
    /// </summary>
    /// <remarks>
    /// BMS charts in the wild are commonly Shift-JIS or GBK, and a chart's <c>#WAV</c>/<c>#BMP</c>
    /// definitions must decode to byte-identical filenames to match the files on disk. Keeping a
    /// ruleset-local entry point means the shared implementation can evolve without pulling surprises
    /// into BMS behaviour.
    /// </remarks>
    public static class BmsChartTextEncoding
    {
        public static readonly string[] CHART_EXTENSIONS = { ".bms", ".bme", ".bml", ".pms" };

        public static bool IsChartPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string extension = Path.GetExtension(path);

            foreach (string candidate in CHART_EXTENSIONS)
            {
                if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Decodes a chart file, using its own folder for reference-resolution scoring.
        /// </summary>
        public static string DecodeFile(string path) => LegacyChartTextEncoding.DecodeFile(path);

        public static string DecodeBytes(byte[] bytes, string songFolder = "") => LegacyChartTextEncoding.DecodeBytes(bytes, songFolder);

        public static string[] SplitLines(string text) => LegacyChartStreamDecode.SplitLines(text);

        public static TextReader OpenReader(string path) => LegacyChartTextEncoding.OpenReader(path);

        public static string? ResolveExistingRelativePath(string contentRoot, string? relativePath)
            => LegacyChartTextEncoding.ResolveExistingRelativePath(contentRoot, relativePath);

        /// <summary>
        /// Tries to decode an in-flight chart stream, falling back to the reader's own lines when the
        /// underlying stream is unreachable (e.g. non-seekable).
        /// </summary>
        public static IEnumerable<string> ReadLines(IO.LineBufferedReader reader)
        {
            if (LegacyChartStreamDecode.TryDecode(reader, out LegacyChartStreamDecode.Result result))
                return SplitLines(result.Text);

            var lines = new List<string>();
            string? line;

            while ((line = reader.ReadLine()) != null)
                lines.Add(line);

            return lines;
        }
    }
}
