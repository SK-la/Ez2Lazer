// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Helpers for the <c>bms-ext:set:&lt;base64&gt;</c> hash convention used to track
    /// external BMS folders inside the osu library without copying their assets into Realm.
    /// </summary>
    public static class BMSExternalPath
    {
        public const string HashPrefix = "bms-ext:set:";

        /// <summary>
        /// Encode an external folder path into the canonical BMS external set hash.
        /// </summary>
        public static string Encode(string folderPath)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(folderPath ?? string.Empty));
            return HashPrefix + encoded;
        }

        /// <summary>
        /// Try to recover the external folder path from a beatmap set hash.
        /// </summary>
        /// <param name="hash">The <see cref="osu.Game.Beatmaps.BeatmapSetInfo.Hash"/> value.</param>
        /// <param name="folderPath">When the method returns true, contains the decoded folder path.</param>
        /// <returns>True if <paramref name="hash"/> is a valid <c>bms-ext:set:</c> hash and the folder still exists.</returns>
        public static bool TryDecode(string? hash, out string folderPath)
        {
            folderPath = string.Empty;

            if (string.IsNullOrEmpty(hash))
                return false;

            if (!hash.StartsWith(HashPrefix, StringComparison.Ordinal))
                return false;

            string encoded = hash.Substring(HashPrefix.Length);

            if (string.IsNullOrEmpty(encoded))
                return false;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                string decoded = Encoding.UTF8.GetString(bytes);

                if (string.IsNullOrWhiteSpace(decoded))
                    return false;

                folderPath = decoded;
                return Directory.Exists(folderPath);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Try to recover the external folder path without verifying its existence on disk.
        /// Useful in test environments or when a probe fallback is desired.
        /// </summary>
        public static bool TryDecodeRaw(string? hash, out string folderPath)
        {
            folderPath = string.Empty;

            if (string.IsNullOrEmpty(hash))
                return false;

            if (!hash.StartsWith(HashPrefix, StringComparison.Ordinal))
                return false;

            string encoded = hash.Substring(HashPrefix.Length);

            if (string.IsNullOrEmpty(encoded))
                return false;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                string decoded = Encoding.UTF8.GetString(bytes);

                if (string.IsNullOrWhiteSpace(decoded))
                    return false;

                folderPath = decoded;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
