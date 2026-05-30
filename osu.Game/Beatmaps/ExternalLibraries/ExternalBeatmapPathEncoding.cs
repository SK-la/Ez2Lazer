// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Encodes external library folder paths into stable <see cref="BeatmapSetInfo.Hash"/> values.
    /// </summary>
    public static class ExternalBeatmapPathEncoding
    {
        public const string HASH_PREFIX = "ext:set:";

        public static string Encode(string folderPath)
        {
            string normalised = Path.GetFullPath(folderPath ?? string.Empty);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalised));
            return HASH_PREFIX + encoded;
        }

        public static bool TryDecode(string? hash, out string folderPath)
        {
            folderPath = string.Empty;

            if (string.IsNullOrEmpty(hash) || !hash.StartsWith(HASH_PREFIX, StringComparison.Ordinal))
                return false;

            string encoded = hash.Substring(HASH_PREFIX.Length);

            if (string.IsNullOrEmpty(encoded))
                return false;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                string decoded = Encoding.UTF8.GetString(bytes);

                if (string.IsNullOrWhiteSpace(decoded))
                    return false;

                folderPath = Path.GetFullPath(decoded);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsExternalSetHash(string? hash)
            => !string.IsNullOrEmpty(hash) && hash.StartsWith(HASH_PREFIX, StringComparison.Ordinal);

        /// <summary>
        /// Resolves the on-disk folder for a unified <see cref="HASH_PREFIX"/> external set.
        /// Prefers persisted <see cref="BeatmapSetInfo.ExternalContentRoot"/>; does not require the directory to exist for the first lookup pass.
        /// </summary>
        public static bool TryResolveContentRoot(BeatmapSetInfo set, out string contentRoot)
        {
            contentRoot = string.Empty;

            if (!string.IsNullOrWhiteSpace(set.ExternalContentRoot))
            {
                contentRoot = Path.GetFullPath(set.ExternalContentRoot);
                return true;
            }

            if (TryDecode(set.Hash, out contentRoot))
                return true;

            string? effective = set.GetEffectiveExternalContentRoot();

            if (!string.IsNullOrEmpty(effective))
            {
                contentRoot = effective;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Populates <see cref="BeatmapSetInfo.ExternalContentRoot"/> and <see cref="BeatmapSetInfo.HostingKind"/> from a standard <see cref="HASH_PREFIX"/> hash.
        /// </summary>
        public static bool TryPopulateExternalHosting(BeatmapSetInfo set)
        {
            if (!TryDecode(set.Hash, out string folderPath))
                return false;

            set.HostingKind = BeatmapSetHostingKind.External;
            set.ExternalContentRoot = folderPath;
            return true;
        }
    }
}
