// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Statistics
{
    internal static class BeatmapExportUtils
    {
        private const string exported_beatmap_creator_prefix = "Ez2Lazer Mods=";

        public static bool HasMods(IReadOnlyList<Mod> mods) => mods.Count > 0;

        public static void ApplyExportMetadata(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            if (!HasMods(mods))
                return;

            // 标记导出作者为 Ez2Lazer + mods 列表。
            beatmap.Metadata.Author.Username = GetExportCreator(mods);
            beatmap.Metadata.Author.OnlineID = -1;

            // 导出的 mods 转谱应当被视为本地新谱，不能继续携带原谱的在线关联信息。
            beatmap.BeatmapInfo.ResetOnlineInfo();

            if (beatmap.BeatmapInfo.BeatmapSet == null)
                return;

            beatmap.BeatmapInfo.BeatmapSet.OnlineID = -1;
            beatmap.BeatmapInfo.BeatmapSet.Status = BeatmapOnlineStatus.None;
            beatmap.BeatmapInfo.BeatmapSet.DateRanked = null;
            beatmap.BeatmapInfo.BeatmapSet.DateSubmitted = null;
        }

        public static string GetExportCreator(IReadOnlyList<Mod> mods)
        {
            string modAcronyms = string.Join("+", mods.Select(mod => mod.Acronym));

            return $"{exported_beatmap_creator_prefix}{modAcronyms}";
        }

        public static MemoryStream EncodeToStream(IBeatmap beatmap, ISkin? beatmapSkin)
        {
            var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                new LegacyBeatmapEncoder(beatmap, beatmapSkin).Encode(writer);

            stream.Position = 0;
            return stream;
        }
    }
}
