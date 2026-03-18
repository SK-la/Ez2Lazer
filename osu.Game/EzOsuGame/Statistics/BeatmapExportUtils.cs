// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
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

            beatmap.Metadata.Author.Username = GetExportCreator(mods);
        }

        public static string GetExportCreator(IReadOnlyList<Mod> mods)
        {
            string modAcronyms = string.Join("+", mods.Select(mod => mod.Acronym));

            return $"{exported_beatmap_creator_prefix}{modAcronyms}";
        }
    }
}
