// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework;
using osu.Framework.Logging;
using osu.Framework.Text;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Fonts
{
    /// <summary>
    /// Enumerates installed outline fonts from common system font directories,
    /// and resolves the platform colour emoji font for global fallback.
    /// </summary>
    public static class EzSystemFontCatalog
    {
        public const string NONE_OPTION = "";

        private static readonly Lock cache_lock = new Lock();
        private static IReadOnlyList<EzSystemFontEntry>? cached;
        public static IReadOnlyList<EzSystemFontEntry>? CachedEmoji;

        /// <summary>Sample emoji codepoints used to verify a face is emoji-capable.</summary>
        private static readonly int[] emoji_probe_codepoints =
        {
            0x1F600, // 😀
            0x1F602, // 😂
            0x1F44D, // 👍
            0x1F389, // 🎉
            0x1F525, // 🔥
        };

        public static IReadOnlyList<EzSystemFontEntry> GetEntries(bool forceRefresh = false)
        {
            lock (cache_lock)
            {
                if (cached != null && !forceRefresh)
                    return cached;

                if (forceRefresh)
                    CachedEmoji = null;

                var byFamily = new Dictionary<string, EzSystemFontEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (string directory in enumerateFontDirectories())
                {
                    if (!Directory.Exists(directory))
                        continue;

                    IEnumerable<string> files;

                    try
                    {
                        // Windows Fonts is mostly flat; Linux/macOS trees nest under family dirs.
                        var depth = RuntimeInfo.OS == RuntimeInfo.Platform.Windows
                            ? SearchOption.TopDirectoryOnly
                            : SearchOption.AllDirectories;

                        files = Directory.EnumerateFiles(directory, "*.*", depth)
                                         .Where(f =>
                                         {
                                             string ext = Path.GetExtension(f);
                                             return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                                                    || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                                                    || ext.Equals(".ttc", StringComparison.OrdinalIgnoreCase);
                                         });
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"EzSystemFontCatalog: cannot list {directory}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                        continue;
                    }

                    foreach (string file in files)
                    {
                        try
                        {
                            // Face 0 only for v1 (collections may expose more later).
                            string? family = OutlineFont.TryGetFamilyName(file, 0);

                            if (string.IsNullOrWhiteSpace(family) || isPlaceholderFamilyName(family))
                                family = Path.GetFileNameWithoutExtension(file);

                            bool prefer = isPreferredStyleFile(file);

                            if (!byFamily.TryGetValue(family, out var existing) || (prefer && !isPreferredStyleFile(existing.Path)))
                                byFamily[family] = new EzSystemFontEntry(family, family, file, 0);
                        }
                        catch
                        {
                            // Skip unreadable / broken fonts.
                        }
                    }
                }

                cached = byFamily.Values
                                 .OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                                 .ToList();

                return cached;
            }
        }

        public static EzSystemFontEntry? FindByFamily(string? family)
        {
            if (string.IsNullOrWhiteSpace(family))
                return null;

            foreach (var entry in GetEntries())
            {
                if (string.Equals(entry.Family, family, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private static bool isPlaceholderFamilyName(string name)
        {
            foreach (char c in name)
            {
                if (c != '?')
                    return false;
            }

            return name.Length > 0;
        }

        /// <summary>
        /// Resolves the platform colour emoji font (TTF/OTF/TTC) for global glyph fallback.
        /// Does not require a full catalog scan when well-known paths exist.
        /// </summary>
        public static EzSystemFontEntry? FindSystemEmojiFont()
        {
            foreach (var candidate in enumerateSystemEmojiCandidates())
            {
                if (!File.Exists(candidate.Path))
                    continue;

                try
                {
                    string? family = OutlineFont.TryGetFamilyName(candidate.Path, candidate.FaceIndex);

                    if (string.IsNullOrWhiteSpace(family))
                        family = Path.GetFileNameWithoutExtension(candidate.Path);

                    return new EzSystemFontEntry(family, family, candidate.Path, candidate.FaceIndex);
                }
                catch (Exception ex)
                {
                    Logger.Log($"EzSystemFontCatalog: emoji candidate unreadable {candidate.Path}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                }
            }

            foreach (string family in knownSystemEmojiFamilyNames())
            {
                var entry = FindByFamily(family);

                if (entry != null)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Installed fonts that look like emoji faces and actually contain common emoji codepoints.
        /// </summary>
        public static IReadOnlyList<EzSystemFontEntry> GetEmojiEntries(bool forceRefresh = false)
        {
            lock (cache_lock)
            {
                if (CachedEmoji != null && !forceRefresh)
                    return CachedEmoji;

                var byFamily = new Dictionary<string, EzSystemFontEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (var candidate in enumerateSystemEmojiCandidates())
                    tryAddEmojiEntry(byFamily, candidate.Path, candidate.FaceIndex);

                foreach (var entry in GetEntries())
                {
                    if (!looksLikeEmojiFont(entry.Family, entry.Path))
                        continue;

                    if (!isEmojiCapable(entry.Path, entry.FaceIndex))
                        continue;

                    byFamily.TryAdd(entry.Family, entry);
                }

                foreach (string family in knownSystemEmojiFamilyNames())
                {
                    if (byFamily.ContainsKey(family))
                        continue;

                    var entry = FindByFamily(family);
                    if (entry == null || !isEmojiCapable(entry.Value.Path, entry.Value.FaceIndex))
                        continue;

                    byFamily[entry.Value.Family] = entry.Value;
                }

                CachedEmoji = byFamily.Values
                                      .OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                                      .ToList();

                return CachedEmoji;
            }
        }

        private static void tryAddEmojiEntry(Dictionary<string, EzSystemFontEntry> byFamily, string path, int faceIndex)
        {
            if (!File.Exists(path) || !isEmojiCapable(path, faceIndex))
                return;

            try
            {
                string? family = OutlineFont.TryGetFamilyName(path, faceIndex);

                if (string.IsNullOrWhiteSpace(family) || isPlaceholderFamilyName(family))
                    family = Path.GetFileNameWithoutExtension(path);

                byFamily.TryAdd(family, new EzSystemFontEntry(family, family, path, faceIndex));
            }
            catch
            {
                // Skip unreadable candidates.
            }
        }

        private static bool looksLikeEmojiFont(string family, string path)
        {
            string haystack = family + " " + Path.GetFileNameWithoutExtension(path);

            return haystack.Contains("emoji", StringComparison.OrdinalIgnoreCase)
                   || haystack.Contains("seguiemj", StringComparison.OrdinalIgnoreCase)
                   || haystack.Contains("seguisym", StringComparison.OrdinalIgnoreCase)
                   || haystack.Contains("twemoji", StringComparison.OrdinalIgnoreCase)
                   || haystack.Contains("joypixels", StringComparison.OrdinalIgnoreCase)
                   || haystack.Contains("openmoji", StringComparison.OrdinalIgnoreCase)
                   || haystack.Contains("blobmoji", StringComparison.OrdinalIgnoreCase);
        }

        private static bool isEmojiCapable(string path, int faceIndex)
            => OutlineFont.TryHasAnyCodepoint(path, faceIndex, emoji_probe_codepoints);

        private static IEnumerable<(string Path, int FaceIndex)> enumerateSystemEmojiCandidates()
        {
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                if (!string.IsNullOrEmpty(fonts))
                {
                    // Colour CBDT emoji first; Symbol is a mono/symbol fallback.
                    yield return (Path.Combine(fonts, "seguiemj.ttf"), 0);
                    yield return (Path.Combine(fonts, "seguisym.ttf"), 0);
                }
            }

            if (RuntimeInfo.IsApple)
            {
                yield return ("/System/Library/Fonts/Apple Color Emoji.ttc", 0);
                yield return ("/Library/Fonts/Apple Color Emoji.ttc", 0);
                yield return ("/System/Library/Fonts/Supplemental/Apple Color Emoji.ttc", 0);
            }

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                yield return ("/usr/share/fonts/truetype/noto/NotoColorEmoji.ttf", 0);
                yield return ("/usr/share/fonts/noto/NotoColorEmoji.ttf", 0);
                yield return ("/usr/share/fonts/google-noto-emoji/NotoColorEmoji.ttf", 0);
                yield return ("/usr/share/fonts/truetype/noto-color-emoji/NotoColorEmoji.ttf", 0);
                yield return ("/usr/local/share/fonts/NotoColorEmoji.ttf", 0);

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(home))
                {
                    yield return (Path.Combine(home, ".local", "share", "fonts", "NotoColorEmoji.ttf"), 0);
                    yield return (Path.Combine(home, ".fonts", "NotoColorEmoji.ttf"), 0);
                }
            }
        }

        private static IEnumerable<string> knownSystemEmojiFamilyNames()
        {
            yield return "Segoe UI Emoji";
            yield return "Segoe UI Symbol";
            yield return "Apple Color Emoji";
            yield return "Noto Color Emoji";
            yield return "NotoColorEmoji";
        }

        private static IEnumerable<string> enumerateFontDirectories()
        {
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            if (!string.IsNullOrEmpty(fonts))
                yield return fonts;

            // Per-user Windows fonts
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrEmpty(local))
                yield return Path.Combine(local, "Microsoft", "Windows", "Fonts");

            if (RuntimeInfo.IsApple)
            {
                yield return "/System/Library/Fonts";
                yield return "/Library/Fonts";

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(home))
                    yield return Path.Combine(home, "Library", "Fonts");
            }

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                yield return "/usr/share/fonts";
                yield return "/usr/local/share/fonts";

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(home))
                {
                    yield return Path.Combine(home, ".fonts");
                    yield return Path.Combine(home, ".local", "share", "fonts");
                }
            }
        }

        private static bool isPreferredStyleFile(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            return name.Contains("Regular", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("正常", StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith("Reg", StringComparison.OrdinalIgnoreCase);
        }
    }

    public readonly record struct EzSystemFontEntry(string Family, string DisplayName, string Path, int FaceIndex);
}
