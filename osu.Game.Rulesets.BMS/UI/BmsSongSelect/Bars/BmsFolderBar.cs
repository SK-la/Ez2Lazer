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
                foreach (BmsFolderSummary folder in context.BeatmapManager.GetChildFolderPage(FullPath, null, limit))
                {
                    BmsBar? child = createChildBar(context, folder);
                    if (child != null)
                        result.Add(child);
                }

                BmsChartSummaryPage loose = context.BeatmapManager.GetChartSummaryPage(
                    new BmsChartQuery(
                        FolderPath: FullPath,
                        KeyCounts: context.KeyModeFilter.ToKeyCounts(),
                        Sort: context.SortPolicy.IndexSort),
                    null,
                    Math.Max(1, limit - result.Count));

                if (loose.Items.Count > 0)
                    result.Add(new BmsSongPackBar(FullPath, Title, loose.Items));
            }

            return new BmsBarPage(result, null);
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
                Sort: context.SortPolicy.IndexSort,
                FolderRecursive: true));
        }

        /// <summary>
        /// Packs (folders with charts, or leaf folders whose charts live in nested paths) become song rows.
        /// Intermediate grouping folders stay directories.
        /// </summary>
        private static BmsBar? createChildBar(BmsBarContext context, BmsFolderSummary folder)
        {
            var keyCounts = context.KeyModeFilter.ToKeyCounts();
            BmsChartSort sort = context.SortPolicy.IndexSort;

            BmsChartSummaryPage exact = context.BeatmapManager.GetChartSummaryPage(
                new BmsChartQuery(FolderPath: folder.FolderPath, KeyCounts: keyCounts, Sort: sort),
                null,
                1);
            bool hasChildFolders = context.BeatmapManager.GetChildFolderPage(folder.FolderPath, null, 1).Count > 0;
            bool treatAsPack = exact.Items.Count > 0 || !hasChildFolders;

            if (!treatAsPack)
                return new BmsFolderBar(BmsPathCrc.Compute(folder.FolderPath), folder.Name, folder.FolderPath);

            BmsChartSummaryPage charts = context.BeatmapManager.GetChartSummaryPage(
                new BmsChartQuery(
                    FolderPath: folder.FolderPath,
                    KeyCounts: keyCounts,
                    Sort: BmsChartSort.Level,
                    FolderRecursive: true),
                null,
                200);

            if (charts.Items.Count == 0)
            {
                return hasChildFolders
                    ? new BmsFolderBar(BmsPathCrc.Compute(folder.FolderPath), folder.Name, folder.FolderPath)
                    : null;
            }

            return new BmsSongPackBar(folder.FolderPath, folder.Name, charts.Items);
        }
    }
}
