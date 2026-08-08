// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public sealed class BmsFolderBar : BmsDirectoryBar
    {
        public string Crc { get; }
        public string FullPath { get; }

        public BmsFolderBar(string crc, string name, string fullPath)
        {
            Crc = crc;
            FullPath = fullPath;
            Title = string.IsNullOrEmpty(name) ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : name;
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
            => GetPage(context, null, 150)!.Bars;

        public override BmsBarPage? GetPage(BmsBarContext context, BmsBarPageCursor? after, int limit)
        {
            var result = new List<BmsBar>(limit);

            if (after == null)
            {
                int folderLimit = Math.Min(50, limit);

                foreach (BmsFolderSummary folder in context.BeatmapManager.GetChildFolderPage(FullPath, null, folderLimit))
                    result.Add(new BmsFolderBar(BmsPathCrc.Compute(folder.FolderPath), folder.Name, folder.FolderPath));
            }

            int chartLimit = Math.Max(1, limit - result.Count);
            var query = new BmsChartQuery(
                FolderPath: FullPath,
                KeyCounts: context.KeyModeFilter.ToKeyCounts(),
                Sort: context.SortPolicy.IndexSort);
            BmsChartSummaryPage page = context.BeatmapManager.GetChartSummaryPage(query, after?.IndexCursor, chartLimit);
            result.AddRange(page.Items.Select(summary => new BmsSongBar(summary)));
            BmsBarPageCursor? next = page.NextCursor == null ? null : new BmsBarPageCursor(page.NextCursor, null);
            return new BmsBarPage(result, next);
        }

        public override BmsChartSummary? GetRandom(BmsBarContext context, IReadOnlyDictionary<string, System.Text.Json.JsonElement>? filter)
        {
            if (filter is { Count: > 0 })
            {
                return context.SqlQuery.GetRandom(
                    "song.folder = $folder",
                    context.KeyModeFilter.ToKeyCounts(),
                    filter,
                    new Dictionary<string, object?> { ["$folder"] = FullPath });
            }

            return context.BeatmapManager.GetRandomChartSummary(new BmsChartQuery(
                FolderPath: FullPath,
                KeyCounts: context.KeyModeFilter.ToKeyCounts(),
                Sort: context.SortPolicy.IndexSort));
        }
    }
}
