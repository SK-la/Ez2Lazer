// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    public readonly record struct BmsChartIdentity(Guid BeatmapId, Guid SetId, string PathKey)
    {
        public static BmsChartIdentity Create(string chartPath, string folderPath)
        {
            return new BmsChartIdentity(
                CreateBeatmapId(chartPath),
                CreateSetId(folderPath),
                BmsPathKeys.ComputeChartPathKey(chartPath));
        }

        public static Guid CreateBeatmapId(string chartPath) => createDeterministicGuid($"bms:chart:{chartPath}");

        public static Guid CreateSetId(string folderPath) => createDeterministicGuid($"bms:set:{folderPath}");

        private static Guid createDeterministicGuid(string seed)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }
    }
}
