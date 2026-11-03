// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Text encoding detection for legacy chart formats (BMS, ProjectDIVA, ...) that predate UTF-8 being universal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Candidates are ranked by evidence, strongest first: whether the media files a chart references exist once
    /// decoded, then mis-decode fingerprints, then which code page the machine itself uses. Byte validity alone
    /// cannot separate the CJK candidates — CP936 and CP932 accept almost any byte pair — so the fingerprints are
    /// what decide between them.
    /// </para>
    /// <para>
    /// Known limitation: on a CP936 machine a Shift-JIS chart whose <c>#WAV</c> list is pure ASCII leaves no usable
    /// evidence, and CP936 wins by preference. Positive evidence (a Japanese filename that resolves, or a decode
    /// that forces artefacts on the alternative) is required before Shift-JIS is chosen.
    /// </para>
    /// </remarks>
    public static class LegacyChartTextEncoding
    {
        // Reference resolution dominates: a filename that visibly matches disk is the one unambiguous signal.
        private const int reference_hit_score = 50;
        private const int reference_miss_penalty = 10;

        // Mis-decode fingerprints. GBK text read as Shift-JIS turns Chinese into runs of half-width katakana
        // (CP932 reads 0xA1-0xDF as single-byte katakana) plus private-use ideographs. The reverse direction
        // reads as ordinary-looking Chinese, so only these artefacts are usable as evidence.
        private const int artifact_penalty = 4;
        private const int max_artifact_penalty = 120;
        private const int replacement_penalty = 200;

        /// <summary>
        /// A decode that is strictly valid UTF-8 while carrying non-ASCII is decisive: no legacy CJK code page
        /// produces a whole file of well-formed multi-byte UTF-8 sequences by accident.
        /// </summary>
        private const int valid_utf8_bonus = 100;

        /// <summary>
        /// Full-width kana is weak evidence of genuine Japanese authorship (the Chinese mis-read above comes out
        /// half-width), so it only nudges a close call — but it is enough to outvote the system code page.
        /// </summary>
        private const int kana_bonus = 4;
        private const int max_kana_bonus = 20;

        /// <summary>Historical default: prefer the machine's own code page when the CJK candidates tie.</summary>
        private const int system_codepage_bonus = 10;

        private static readonly object provider_lock = new object();
        private static bool providerRegistered;
        private static int systemAnsiCodePage = 1252;

        private static readonly Regex referenced_file_pattern = new Regex(
            @"^\s*\d+\s+(.+\.(?:mp3|ogg|wav|flac|jpg|jpeg|png|bmp|mpg|mpeg|avi|mp4|wmv))\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches a bare filename with a media extension, for formats that reference assets by name alone
        /// (BMS <c>#WAV</c>/<c>#BMP</c> definitions) rather than as an indexed list entry.
        /// </summary>
        private static readonly Regex bare_file_pattern = new Regex(
            @"[^\s<>""|?*]+\.(?:mp3|ogg|wav|flac|jpg|jpeg|png|bmp|mpg|mpeg|avi|mp4|wmv)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static LegacyChartTextEncoding()
        {
            ensureCodePages();
        }

        /// <summary>
        /// Decodes a chart file, using its folder for reference-resolution evidence.
        /// </summary>
        public static string DecodeFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string songFolder = Path.GetDirectoryName(path) ?? string.Empty;
            return DecodeBytes(bytes, songFolder);
        }

        /// <summary>
        /// Decodes raw chart bytes using the candidate encodings and their evidence scores.
        /// </summary>
        /// <param name="bytes">Raw file bytes.</param>
        /// <param name="songFolder">
        /// Folder the chart lives in, used to check whether referenced media exists. Pass an empty string when the
        /// folder is unknown; scoring then falls back to mis-decode fingerprints and code page preference.
        /// </param>
        public static string DecodeBytes(byte[] bytes, string songFolder = "")
        {
            var (_, bestText) = detect(bytes, songFolder);
            return bestText ?? stripByteOrderMark(encoding_utf8_lossy.GetString(bytes));
        }

        /// <summary>
        /// Detects the best encoding for a chart file (same evidence as <see cref="DecodeFile"/>).
        /// </summary>
        public static Encoding DetectBestEncoding(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string songFolder = Path.GetDirectoryName(path) ?? string.Empty;
            return DetectBestEncoding(bytes, songFolder);
        }

        /// <summary>
        /// Detects the best encoding for raw chart bytes (same evidence as <see cref="DecodeBytes"/>).
        /// </summary>
        public static Encoding DetectBestEncoding(byte[] bytes, string songFolder = "")
        {
            var (best, _) = detect(bytes, songFolder);
            return best ?? encoding_utf8_lossy;
        }

        /// <summary>
        /// Resolves a chart-relative path against <paramref name="contentRoot"/>, tolerating separator and case
        /// differences, and falling back to a bare filename match.
        /// </summary>
        public static string? ResolveExistingRelativePath(string contentRoot, string? relativePath)
            => ReferenceIndex.TryCreate(contentRoot)?.Resolve(relativePath);

        private static readonly UTF8Encoding encoding_utf8_lossy = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

        private static (Encoding? best, string? text) detect(byte[] bytes, string songFolder)
        {
            var index = ReferenceIndex.TryCreate(songFolder);

            Encoding? best = null;
            string? bestText = null;
            int bestScore = int.MinValue;

            foreach (Encoding encoding in getCandidates(bytes))
            {
                string text;

                try
                {
                    text = stripByteOrderMark(encoding.GetString(bytes));
                }
                catch
                {
                    continue;
                }

                int score = scoreDecodedText(text, index, bytes, encoding);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = encoding;
                bestText = text;
            }

            return (best, bestText);
        }

        private static void ensureCodePages()
        {
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
            // A BOM is authoritative; nothing else needs to be considered.
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                yield return encoding_utf8_lossy;

                yield break;
            }

            var seen = new HashSet<int>();

            // UTF-8 first when it is genuinely UTF-8: this covers UTF-8 charts without a BOM, which the legacy
            // candidates would otherwise mis-read as plausible-looking CJK.
            bool validUtf8 = hasNonAscii(bytes) && isValidForEncoding(bytes, encoding_utf8_lossy);

            if (validUtf8 && seen.Add(encoding_utf8_lossy.CodePage))
                yield return encoding_utf8_lossy;

            // The encodings these charts are actually authored in, ahead of the machine's code page so that a
            // non-CJK system code page cannot win a tie against them.
            foreach (int codePage in new[] { 936, 932 })
            {
                Encoding? encoding = tryGetEncoding(codePage);

                if (encoding != null && seen.Add(encoding.CodePage))
                    yield return encoding;
            }

            Encoding? ansi = tryGetEncoding(systemAnsiCodePage);

            if (ansi != null && seen.Add(ansi.CodePage))
                yield return ansi;

            // Always return something, even for a payload no candidate can decode.
            if (seen.Add(encoding_utf8_lossy.CodePage))
                yield return encoding_utf8_lossy;
        }

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

        private static bool hasNonAscii(byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                if (b >= 0x80)
                    return true;
            }

            return false;
        }

        private static int scoreDecodedText(string text, ReferenceIndex? index, byte[] bytes, Encoding encoding)
        {
            int score = 0;
            int artifacts = 0;
            int kana = 0;
            bool hasReplacement = false;

            foreach (char c in text)
            {
                if (c == '\uFFFD')
                    hasReplacement = true;
                else if (c is (>= '\uFF61' and <= '\uFF9F') or (>= '\uE000' and <= '\uF8FF'))
                    artifacts++;
                else if (c is (>= '\u3041' and <= '\u3096') or (>= '\u30A1' and <= '\u30FA'))
                    kana++;
            }

            if (hasReplacement)
                score -= replacement_penalty;

            score -= Math.Min(artifacts * artifact_penalty, max_artifact_penalty);
            score += Math.Min(kana * kana_bonus, max_kana_bonus);

            if (isUtf8Family(encoding) && !hasReplacement && isValidForEncoding(bytes, encoding) && hasNonAscii(bytes))
                score += valid_utf8_bonus;

            if (isCjkCodePage(systemAnsiCodePage) && encoding.CodePage == systemAnsiCodePage)
                score += system_codepage_bonus;

            if (index == null)
                return score;

            int existing = 0;
            int referenced = 0;

            foreach (string relative in enumerateReferencedPaths(text))
            {
                referenced++;

                if (index.Contains(relative))
                    existing++;
            }

            if (referenced > 0)
                score += existing * reference_hit_score - (referenced - existing) * reference_miss_penalty;

            return score;
        }

        private static bool isUtf8Family(Encoding encoding)
            => encoding.CodePage is 65001 or 1200;

        private static bool isCjkCodePage(int codePage)
            => codePage is 936 or 932 or 949 or 950;

        private static string stripByteOrderMark(string text)
            => text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;

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

        /// <summary>
        /// One-shot index of a chart folder, so evidence scoring never probes the filesystem per candidate.
        /// </summary>
        /// <remarks>
        /// Lookups are case-insensitive, which is what makes a correct decode visibly "resolve" while a mis-decoded
        /// name does not.
        /// </remarks>
        private sealed class ReferenceIndex
        {
            private const int max_depth = 8;

            private readonly string root;
            private readonly Dictionary<string, string> byRelativePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> byFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            private ReferenceIndex(string root)
            {
                this.root = root;
            }

            public static ReferenceIndex? TryCreate(string? folder)
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return null;

                var index = new ReferenceIndex(folder);
                index.scan(folder, 0);
                return index;
            }

            public bool Contains(string? relativePath) => Resolve(relativePath) != null;

            public string? Resolve(string? relativePath)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return null;

                string normalised = normalise(relativePath);

                if (byRelativePath.TryGetValue(normalised, out string? exact))
                    return exact;

                string fileName = fileNameOf(normalised);

                if (!string.IsNullOrEmpty(fileName) && byFileName.TryGetValue(fileName, out string? byName))
                    return byName;

                return null;
            }

            private void scan(string directory, int depth)
            {
                if (depth > max_depth)
                    return;

                try
                {
                    foreach (string file in Directory.EnumerateFiles(directory))
                    {
                        string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                        byRelativePath.TryAdd(relative, relative);

                        string name = fileNameOf(relative);

                        if (!string.IsNullOrEmpty(name))
                            byFileName.TryAdd(name, relative);
                    }
                }
                catch
                {
                    // Unreadable directory: evidence from it is simply unavailable.
                }

                try
                {
                    foreach (string subdirectory in Directory.EnumerateDirectories(directory))
                        scan(subdirectory, depth + 1);
                }
                catch
                {
                    // Unreadable subdirectories are skipped.
                }
            }

            private static string normalise(string path)
            {
                string normalised = path.Replace('\\', '/').Trim();

                while (normalised.StartsWith("./", StringComparison.Ordinal))
                    normalised = normalised.Substring(2);

                return normalised.TrimStart('/');
            }

            private static string fileNameOf(string normalisedPath)
            {
                int separator = normalisedPath.LastIndexOf('/');
                return separator < 0 ? normalisedPath : normalisedPath.Substring(separator + 1);
            }
        }
    }
}
