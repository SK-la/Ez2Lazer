// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Content hashes for BMS chart files (MD5 + SHA-256 of raw file bytes),
    /// matching beatoraja / difficulty-table conventions (lowercase hex).
    /// </summary>
    public readonly record struct BmsContentHashes(string Md5, string Sha256)
    {
        public bool IsValid => Md5.Length == 32 && Sha256.Length == 64;
    }

    public static class BmsContentHash
    {
        public static BmsContentHashes ComputeFile(string chartPath)
        {
            byte[] bytes = File.ReadAllBytes(chartPath);
            return ComputeBytes(bytes);
        }

        public static BmsContentHashes ComputeBytes(ReadOnlySpan<byte> bytes)
        {
            string md5 = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return new BmsContentHashes(md5, sha256);
        }

        public static bool LooksLikeSha256(string? hash)
            => !string.IsNullOrEmpty(hash) && hash.Length == 64 && isHex(hash);

        public static bool LooksLikeMd5(string? hash)
            => !string.IsNullOrEmpty(hash) && hash.Length == 32 && isHex(hash);

        private static bool isHex(string value)
        {
            foreach (char c in value)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }

            return true;
        }
    }
}
