// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    /// <summary>
    /// CRC32 folder ids aligned with beatoraja <c>SongUtils.crc32</c> usage for folder navigation.
    /// </summary>
    internal static class BmsPathCrc
    {
        private const uint polynomial = 0xEDB88320;

        public static string Compute(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "e2977170";

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            uint crc = 0xFFFFFFFF;

            foreach (byte b in bytes)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }

            return (~crc).ToString("x8");
        }
    }
}
