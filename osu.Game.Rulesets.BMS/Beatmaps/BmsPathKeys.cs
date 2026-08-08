// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Path-derived identifiers for BMS charts (no content hashing).
    /// </summary>
    public static class BmsPathKeys
    {
        public static string ComputeChartPathKey(string chartPath)
        {
            string normalised = normalisePath(chartPath);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalised))).ToLowerInvariant();
        }

        public static string ComputeRealmFileHash(string chartPath) => ComputeChartPathKey(chartPath);

        public static string CreateRandomStartKey(ulong? value = null)
        {
            if (value != null)
                return value.Value.ToString("x16").PadRight(64, '0');

            Span<byte> randomBytes = stackalloc byte[8];
            Random.Shared.NextBytes(randomBytes);
            return Convert.ToHexString(randomBytes).ToLowerInvariant().PadRight(64, '0');
        }

        private static string normalisePath(string chartPath)
        {
            try
            {
                return Path.GetFullPath(chartPath).Replace('\\', '/').ToLowerInvariant();
            }
            catch
            {
                return chartPath.Replace('\\', '/').ToLowerInvariant();
            }
        }
    }
}
