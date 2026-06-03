// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Audio
{
    /// <summary>
    /// One-time index of audio files under an external BMS folder.
    /// Resolves chart/logical filenames to a single relative path without per-lookup disk probing.
    /// </summary>
    public sealed class BmsFolderSampleIndex
    {
        private static readonly string[] sample_extensions = { string.Empty, ".wav", ".ogg", ".mp3", ".flac" };

        private readonly Dictionary<string, string> lookupToRelative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> missedLookups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private BmsFolderSampleIndex()
        {
        }

        public static BmsFolderSampleIndex? TryBuild(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return null;

            var index = new BmsFolderSampleIndex();
            index.scanFolder(folderPath);
            return index;
        }

        /// <summary>
        /// Resolves a chart or lookup filename to a relative path within the BMS folder, or <see langword="null"/> if not found.
        /// </summary>
        public string? TryResolveRelativePath(string lookupName)
        {
            if (string.IsNullOrWhiteSpace(lookupName))
                return null;

            if (missedLookups.Contains(lookupName))
                return null;

            if (lookupToRelative.TryGetValue(lookupName, out string? cached))
                return cached;

            string normalised = lookupName.Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(normalised);
            string directory = Path.GetDirectoryName(normalised) ?? string.Empty;

            if (!string.IsNullOrEmpty(baseName))
            {
                foreach (string variant in buildCaseVariants(baseName))
                {
                    foreach (string ext in sample_extensions)
                    {
                        string candidateRelative = string.IsNullOrEmpty(directory)
                            ? variant + ext
                            : directory + "/" + variant + ext;

                        if (lookupToRelative.TryGetValue(candidateRelative, out string? resolved))
                        {
                            registerAlias(lookupName, resolved);
                            return resolved;
                        }
                    }
                }
            }

            missedLookups.Add(lookupName);
            return null;
        }

        private void scanFolder(string folderPath)
        {
            foreach (string fullPath in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(fullPath);
                if (!isAudioExtension(ext))
                    continue;

                string relative = Path.GetRelativePath(folderPath, fullPath).Replace('\\', '/');
                registerAlias(relative, relative);

                string fileName = Path.GetFileName(relative);
                registerAlias(fileName, relative);

                string withoutExt = Path.GetFileNameWithoutExtension(relative);
                if (!string.IsNullOrEmpty(withoutExt))
                    registerAlias(withoutExt, relative);
            }
        }

        private void registerAlias(string alias, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return;

            lookupToRelative.TryAdd(alias, relativePath);

            string normalised = alias.Replace('\\', '/');
            if (!string.Equals(normalised, alias, StringComparison.Ordinal))
                lookupToRelative.TryAdd(normalised, relativePath);
        }

        private static bool isAudioExtension(string ext)
            => ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".flac", StringComparison.OrdinalIgnoreCase);

        private static string[] buildCaseVariants(string baseName)
        {
            string lower = baseName.ToLowerInvariant();
            string upper = baseName.ToUpperInvariant();

            return new[] { baseName, lower, upper }
                   .Distinct(StringComparer.Ordinal)
                   .ToArray();
        }
    }
}
