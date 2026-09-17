// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Text encoding detection for legacy chart formats (BMS, ProjectDIVA, ...) that were authored
    /// before UTF-8 was universal.
    /// </summary>
    /// <remarks>
    /// These charts are typically saved in the authoring machine's ANSI code page. On modern .NET,
    /// <see cref="Encoding.Default"/> is UTF-8, so reading with it corrupts CJK titles and, more
    /// importantly, the media filenames a chart references (which then no longer match the files on disk).
    /// Candidates are scored by decoding quality and by whether the paths a chart references actually
    /// resolve inside its folder, which is a far stronger signal than byte heuristics alone.
    /// </remarks>
    public static class LegacyChartTextEncoding
    {
        private static readonly Lock provider_lock = new Lock();
        private static bool providerRegistered;
        private static int systemAnsiCodePage = 1252;

        private static readonly Regex referenced_file_pattern = new Regex(
            @"^\s*\d+\s+(.+\.(?:mp3|ogg|wav|flac|jpg|jpeg|png|bmp|mpg|mpeg|avi|mp4|wmv))\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches a bare filename with one of the media extensions, for chart formats that reference
        /// assets by name alone rather than as an indexed list entry.
        /// </summary>
        private static readonly Regex bare_file_pattern = new Regex(
            @"[^\s<>""|?*]+\.(?:mp3|ogg|wav|flac|jpg|jpeg|png|bmp|mpg|mpeg|avi|mp4|wmv)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static LegacyChartTextEncoding()
        {
            ensureCodePages();
        }

        /// <summary>
        /// Decodes a chart file, using its folder for reference-resolution scoring.
        /// </summary>
        public static string DecodeFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string songFolder = Path.GetDirectoryName(path) ?? string.Empty;
            return DecodeBytes(bytes, songFolder);
        }

        /// <summary>
        /// Decodes raw chart bytes using the candidate encodings and scoring.
        /// </summary>
        /// <param name="bytes">Raw file bytes.</param>
        /// <param name="songFolder">
        /// Folder the chart lives in, used to score whether referenced media actually exists.
        /// Pass an empty string when the folder is unknown (scoring degrades to byte heuristics).
        /// </param>
        public static string DecodeBytes(byte[] bytes, string songFolder = "")
        {
            ensureCodePages();

            Encoding? best = null;
            string? bestText = null;
            int bestScore = int.MinValue;

            foreach (Encoding encoding in getCandidates(bytes))
            {
                string text;

                try
                {
                    text = encoding.GetString(bytes);
                }
                catch
                {
                    continue;
                }

                text = stripByteOrderMark(text);
                int score = scoreDecodedText(text, songFolder, bytes, encoding);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = encoding;
                bestText = text;
            }

            return bestText ?? stripByteOrderMark(Encoding.UTF8.GetString(bytes));
        }

        public static TextReader OpenReader(string path) => new StringReader(DecodeFile(path));

        /// <summary>
        /// Detects the best encoding for a chart file (same scoring as <see cref="DecodeFile"/>).
        /// </summary>
        public static Encoding DetectBestEncoding(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string songFolder = Path.GetDirectoryName(path) ?? string.Empty;
            return DetectBestEncoding(bytes, songFolder);
        }

        /// <summary>
        /// Detects the best encoding for raw chart bytes (same scoring as <see cref="DecodeBytes"/>).
        /// </summary>
        public static Encoding DetectBestEncoding(byte[] bytes, string songFolder = "")
        {
            ensureCodePages();

            Encoding? best = null;
            int bestScore = int.MinValue;

            foreach (Encoding encoding in getCandidates(bytes))
            {
                string text;

                try
                {
                    text = encoding.GetString(bytes);
                }
                catch
                {
                    continue;
                }

                int score = scoreDecodedText(text, songFolder, bytes, encoding);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = encoding;
            }

            return best ?? Encoding.UTF8;
        }

        public static void WriteFile(string path, string text, Encoding encoding)
        {
            ensureCodePages();
            byte[] bytes = encoding.GetBytes(text);
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Resolves a chart-relative path against <paramref name="contentRoot"/>, tolerating
        /// separator differences and falling back to a unique same-extension filename match.
        /// </summary>
        public static string? ResolveExistingRelativePath(string contentRoot, string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(contentRoot) || string.IsNullOrWhiteSpace(relativePath))
                return null;

            string normalisedRelative = relativePath.Replace('\\', '/').Trim();
            string full = Path.GetFullPath(Path.Combine(contentRoot, normalisedRelative.Replace('/', Path.DirectorySeparatorChar)));

            if (File.Exists(full))
                return Path.GetRelativePath(contentRoot, full).Replace('\\', '/');

            string fileName = Path.GetFileName(normalisedRelative);
            string direct = Path.Combine(contentRoot, fileName);

            if (File.Exists(direct))
                return fileName;

            // Common layouts keep media in a dedicated subfolder while charts store bare names.
            foreach (string folder in new[] { "RES", "res", "WAV", "wav" })
            {
                string nested = Path.Combine(contentRoot, folder, fileName);

                if (File.Exists(nested))
                    return Path.Combine(folder, fileName).Replace('\\', '/');
            }

            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || !Directory.Exists(contentRoot))
                return null;

            string asciiHint = extractAsciiHint(fileName);
            string[] candidates;

            try
            {
                candidates = Directory.GetFiles(contentRoot, "*" + extension, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return null;
            }

            IEnumerable<string> matches = candidates;

            if (!string.IsNullOrEmpty(asciiHint))
            {
                string[] hinted = candidates
                                  .Where(f => Path.GetFileName(f).Contains(asciiHint, StringComparison.OrdinalIgnoreCase))
                                  .ToArray();

                if (hinted.Length > 0)
                    matches = hinted;
            }

            string[] matchList = matches.ToArray();

            if (matchList.Length == 1)
                return Path.GetFileName(matchList[0]);

            return null;
        }

        private static void ensureCodePages()
        {
            lock (provider_lock)
            {
                if (providerRegistered)
                    return;
            }

            lock (provider_lock)
            {
                if (providerRegistered)
                    return;

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                try
                {
                    systemAnsiCodePage = Encoding.GetEncoding(0).CodePage;
                }
                catch
                {
                    systemAnsiCodePage = 1252;
                }

                providerRegistered = true;
            }
        }

        private static IEnumerable<Encoding> getCandidates(byte[] bytes)
        {
            // UTF-8 with BOM is authoritative when present.
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                yield return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

                yield break;
            }

            var seen = new HashSet<int>();

            // Prefer the system ANSI code page — the historical default for these formats.
            foreach (int codePage in new[] { 0, 936, 932 })
            {
                Encoding? encoding = tryGetEncoding(codePage);

                if (encoding == null || !seen.Add(encoding.CodePage))
                    continue;

                yield return encoding;
            }

            yield return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        }

        private static string stripByteOrderMark(string text)
            => text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;

        private static Encoding? tryGetEncoding(int codePage)
        {
            try
            {
                return Encoding.GetEncoding(codePage);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decodes strictly, reporting whether every byte sequence is valid for the encoding.
        /// </summary>
        private static bool isValidForEncoding(byte[] bytes, Encoding encoding)
        {
            try
            {
                Encoding strict = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                _ = strict.GetString(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int scoreDecodedText(string text, string songFolder, byte[] bytes, Encoding encoding)
        {
            int score = 0;

            if (!text.Contains('\uFFFD'))
                score += 20;

            // Bytes that are not valid for this encoding mean it cannot be the authoring encoding.
            if (!isValidForEncoding(bytes, encoding))
                score -= 60;

            // Kana is a strong marker of a genuine Japanese decode: byte pairs that decode to kana
            // almost never appear as incidental output when the bytes were authored in GBK.
            score += Math.Min(countKana(text), 40) * 4;

            // Valid UTF-8 payload (no invalid sequences) gets a small bonus when not ANSI.
            if (encoding.CodePage is 65001 or 1200)
            {
                try
                {
                    _ = new UTF8Encoding(false, true).GetString(bytes);
                    score += 5;
                }
                catch
                {
                    score -= 40;
                }
            }

            if (encoding.CodePage == systemAnsiCodePage)
                score += 5; // prefer ACP only as a tie-break — historical authoring behaviour

            if (string.IsNullOrEmpty(songFolder) || !Directory.Exists(songFolder))
                return score;

            int existing = 0;
            int referenced = 0;

            // Indexed reference lists (e.g. ProjectDIVA) plus bare filenames (e.g. BMS #WAV definitions).
            foreach (string relative in enumerateReferencedPaths(text))
            {
                referenced++;

                if (ResolveExistingRelativePath(songFolder, relative) != null)
                    existing++;
            }

            if (referenced > 0)
                score += existing * 50 - (referenced - existing) * 10;

            return score;
        }

        private static int countKana(string text)
        {
            int count = 0;

            foreach (char c in text)
            {
                if (c is (>= '\u3041' and <= '\u3096') or (>= '\u30A1' and <= '\u30FA'))
                {
                    count++;

                    if (count >= 40)
                        break;
                }
            }

            return count;
        }

        private static IEnumerable<string> enumerateReferencedPaths(string text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in referenced_file_pattern.Matches(text))
            {
                string relative = match.Groups[1].Value.Trim().TrimEnd('\0', '\r', '\n');

                if (seen.Add(relative))
                    yield return relative;
            }

            foreach (Match match in bare_file_pattern.Matches(text))
            {
                string relative = match.Value.Trim().TrimEnd('\0', '\r', '\n');

                if (relative.Length == 0 || relative.Contains("://", StringComparison.Ordinal))
                    continue;

                if (seen.Add(relative))
                    yield return relative;
            }
        }

        private static string extractAsciiHint(string fileName)
        {
            var sb = new StringBuilder();

            foreach (char c in Path.GetFileNameWithoutExtension(fileName))
            {
                if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9'))
                    sb.Append(c);
                else if (sb.Length >= 4)
                    break;
                else
                    sb.Clear();
            }

            return sb.Length >= 4 ? sb.ToString() : string.Empty;
        }
    }
}
