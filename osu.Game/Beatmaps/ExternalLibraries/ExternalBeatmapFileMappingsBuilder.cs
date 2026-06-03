// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public static class ExternalBeatmapFileMappingsBuilder
    {
        public static IEnumerable<(string storagePath, string relativeFilename)> Build(BeatmapInfo beatmapInfo, RealmAccess? realmAccess)
        {
            var mappings = new List<(string storagePath, string relativeFilename)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void add(string storagePath, string relativeFilename)
            {
                if (string.IsNullOrEmpty(storagePath) || string.IsNullOrEmpty(relativeFilename))
                    return;

                if (seen.Add(storagePath))
                    mappings.Add((storagePath, relativeFilename));
            }

            if (beatmapInfo.BeatmapSet != null)
            {
                foreach (var file in beatmapInfo.BeatmapSet.Files)
                    add(file.File.GetStoragePath(), file.Filename);
            }

            if (beatmapInfo.File is RealmNamedFileUsage usage)
                add(usage.File.GetStoragePath(), usage.Filename);

            realmAccess?.Run(r =>
            {
                var freshBeatmap = r.Find<BeatmapInfo>(beatmapInfo.ID);

                if (freshBeatmap?.File is RealmNamedFileUsage freshFile)
                    add(freshFile.File.GetStoragePath(), freshFile.Filename);

                if (freshBeatmap?.BeatmapSet is BeatmapSetInfo freshSet)
                {
                    foreach (var file in freshSet.Files)
                        add(file.File.GetStoragePath(), file.Filename);
                }
            });

            if (beatmapInfo.Path != null)
            {
                string chartPath = beatmapInfo.Path;

                if (!string.IsNullOrEmpty(chartPath) && beatmapInfo.BeatmapSet != null)
                {
                    string? storagePath = beatmapInfo.BeatmapSet.GetPathForFile(chartPath);

                    if (!string.IsNullOrEmpty(storagePath))
                        add(storagePath, chartPath);
                }
            }

            return mappings;
        }
    }
}
