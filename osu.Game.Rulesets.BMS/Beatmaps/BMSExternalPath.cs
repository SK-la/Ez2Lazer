// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// BMS-specific helpers for resolving external chart paths. New imports use <see cref="ExternalBeatmapPathEncoding"/>.
    /// </summary>
    public static class BMSExternalPath
    {
        /// <summary>
        /// Legacy hash prefix written by older BMS library sync before unified external hosting.
        /// </summary>
        public const string LEGACY_HASH_PREFIX = "bms-ext:set:";

        private static readonly string[] chart_extensions = { ".bms", ".bme", ".bml", ".pms" };

        public static string Encode(string? folderPath) => ExternalBeatmapPathEncoding.Encode(folderPath ?? string.Empty);

        /// <summary>
        /// Resolves the on-disk folder for an externally hosted BMS set.
        /// </summary>
        public static bool TryGetContentRoot(BeatmapSetInfo? beatmapSet, out string folderPath)
        {
            folderPath = beatmapSet?.GetEffectiveExternalContentRoot() ?? string.Empty;

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                return true;

            return TryDecode(beatmapSet?.Hash, out folderPath);
        }

        public static bool TryDecode(string? hash, out string folderPath)
        {
            if (ExternalBeatmapPathEncoding.TryDecode(hash, out folderPath))
                return Directory.Exists(folderPath);

            if (!TryDecodeLegacyHash(hash, out folderPath))
                return false;

            return Directory.Exists(folderPath);
        }

        public static bool TryDecodeRaw(string? hash, out string folderPath)
        {
            if (ExternalBeatmapPathEncoding.TryDecode(hash, out folderPath))
                return true;

            return TryDecodeLegacyHash(hash, out folderPath);
        }

        internal static bool TryDecodeLegacyHash(string? hash, out string folderPath)
        {
            folderPath = string.Empty;

            if (string.IsNullOrEmpty(hash) || !hash.StartsWith(LEGACY_HASH_PREFIX, StringComparison.Ordinal))
                return false;

            string encoded = hash.Substring(LEGACY_HASH_PREFIX.Length);

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

        public static bool TryResolveExternalChartPath(BeatmapSetInfo? beatmapSet, string? beatmapPath, IEnumerable<string> setFilenames, out string chartPath)
        {
            chartPath = string.Empty;

            if (!TryGetContentRoot(beatmapSet, out string folderPath))
                return false;

            string? chartFilename = beatmapPath;

            if (string.IsNullOrEmpty(chartFilename))
                chartFilename = setFilenames.FirstOrDefault(name => chart_extensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

            if (string.IsNullOrEmpty(chartFilename))
                return false;

            string candidate = Path.Combine(folderPath, chartFilename);

            if (!File.Exists(candidate))
                return false;

            chartPath = candidate;
            return true;
        }
    }
}
