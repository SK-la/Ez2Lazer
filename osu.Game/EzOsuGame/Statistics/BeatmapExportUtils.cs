// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;

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

                // 标记导出作者为 Ez2Lazer + mods 列表
                beatmap.Metadata.Author.Username = GetExportCreator(mods);

                // 当进行 Mods 转谱导出时，重置谱面的在线 ID 为默认值，避免导出文件中包含原始在线 id
                // 这会导致 LegacyBeatmapEncoder 在导出时不输出 BeatmapID 行。
                try
                {
                    beatmap.BeatmapInfo.OnlineID = -1;

                    if (beatmap.BeatmapInfo.BeatmapSet != null)
                        beatmap.BeatmapInfo.BeatmapSet.OnlineID = -1;
                }
                catch
                {
                    Logger.Log("Failed to reset beatmap online ID during export. This may cause issues with some encoders.", LoggingTarget.Runtime, LogLevel.Error);
                }
        }

        public static string GetExportCreator(IReadOnlyList<Mod> mods)
        {
            string modAcronyms = string.Join("+", mods.Select(mod => mod.Acronym));

            return $"{exported_beatmap_creator_prefix}{modAcronyms}";
        }
    }
}
