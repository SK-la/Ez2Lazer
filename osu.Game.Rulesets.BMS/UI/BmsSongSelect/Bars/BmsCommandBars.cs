// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public sealed class BmsContainerBar : BmsDirectoryBar
    {
        private readonly IReadOnlyList<BmsRajaFolderDefinition> children;

        public BmsContainerBar(string title, IReadOnlyList<BmsRajaFolderDefinition> children)
        {
            Title = title;
            this.children = children;
        }

        public override string Title { get; }

        public override bool IsSortable => false;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context) => BmsFolderConfigLoader.BuildBars(children, context);
    }

    public sealed class BmsCommandBar : BmsDirectoryBar
    {
        private readonly string sql;
        private readonly bool showInvisible;

        public BmsCommandBar(string title, string sql, bool showInvisible)
        {
            Title = title;
            this.sql = sql;
            this.showInvisible = showInvisible;
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
            => GetPage(context, null, 150).Bars;

        public override BmsBarPage GetPage(BmsBarContext context, BmsBarPageCursor? after, int limit)
        {
            IReadOnlyCollection<int>? keyCounts = showInvisible ? null : context.KeyModeFilter.ToKeyCounts();
            IReadOnlyList<BmsChartSummary> summaries = context.SqlQuery.ExecutePage(sql, after?.FilterCursor, limit, keyCounts);
            var bars = summaries.Select(summary => (BmsBar)new BmsSongBar(summary)).ToList();
            BmsBarPageCursor? next = summaries.Count == limit
                ? new BmsBarPageCursor(null, summaries[^1].PathKey)
                : null;
            return new BmsBarPage(bars, next);
        }

        public override BmsChartSummary? GetRandom(BmsBarContext context, IReadOnlyDictionary<string, JsonElement>? filter)
            => context.SqlQuery.GetRandom(sql, showInvisible ? null : context.KeyModeFilter.ToKeyCounts(), filter);
    }

    public sealed class BmsSearchBar : BmsDirectoryBar
    {
        public string Query { get; }

        public BmsSearchBar(string query)
        {
            Query = query;
            Title = $"Search: {query}";
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
            => GetPage(context, null, 150).Bars;

        public override BmsBarPage GetPage(BmsBarContext context, BmsBarPageCursor? after, int limit)
        {
            BmsChartSummaryPage page = context.SqlQuery.SearchByText(
                Query,
                after?.IndexCursor,
                limit,
                context.SortPolicy.IndexSort,
                context.KeyModeFilter.ToKeyCounts());
            return new BmsBarPage(
                page.Items.Select(summary => (BmsBar)new BmsSongBar(summary)).ToList(),
                page.NextCursor == null ? null : new BmsBarPageCursor(page.NextCursor, null));
        }

        public override BmsChartSummary? GetRandom(BmsBarContext context, IReadOnlyDictionary<string, JsonElement>? filter)
        {
            if (filter is { Count: > 0 })
            {
                const string where = "rtrim(song.title||' '||song.subtitle||' '||song.artist||' '||song.subartist||' '||song.genre) LIKE $search";
                return context.SqlQuery.GetRandom(
                    where,
                    context.KeyModeFilter.ToKeyCounts(),
                    filter,
                    new Dictionary<string, object?> { ["$search"] = $"%{Query}%" });
            }

            return context.BeatmapManager.GetRandomChartSummary(new BmsChartQuery(
                SearchText: Query,
                KeyCounts: context.KeyModeFilter.ToKeyCounts(),
                Sort: context.SortPolicy.IndexSort));
        }
    }

    public sealed class BmsSameFolderBar : BmsDirectoryBar
    {
        private readonly string folderPath;

        public BmsSameFolderBar(string folderPath)
        {
            this.folderPath = folderPath;
            Title = BmsStrings.RAJA_SAME_FOLDER_FILTER_TITLE.ToString();
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context)
            => GetPage(context, null, 150).Bars;

        public override BmsBarPage GetPage(BmsBarContext context, BmsBarPageCursor? after, int limit)
        {
            var query = new BmsChartQuery(
                FolderPath: folderPath,
                KeyCounts: context.KeyModeFilter.ToKeyCounts(),
                Sort: context.SortPolicy.IndexSort,
                FolderRecursive: true);
            BmsChartSummaryPage page = context.BeatmapManager.GetChartSummaryPage(query, after?.IndexCursor, limit);
            return new BmsBarPage(
                page.Items.Select(summary => (BmsBar)new BmsSongBar(summary)).ToList(),
                page.NextCursor == null ? null : new BmsBarPageCursor(page.NextCursor, null));
        }

        public override BmsChartSummary? GetRandom(BmsBarContext context, IReadOnlyDictionary<string, JsonElement>? filter)
        {
            if (filter is { Count: > 0 })
            {
                return context.SqlQuery.GetRandom(
                    "song.folder = $folder",
                    context.KeyModeFilter.ToKeyCounts(),
                    filter,
                    new Dictionary<string, object?> { ["$folder"] = folderPath });
            }

            return context.BeatmapManager.GetRandomChartSummary(new BmsChartQuery(
                FolderPath: folderPath,
                KeyCounts: context.KeyModeFilter.ToKeyCounts(),
                Sort: context.SortPolicy.IndexSort,
                FolderRecursive: true));
        }
    }

    public sealed class BmsRandomExecutableBar : BmsSelectableBar
    {
        public string RandomName { get; }
        public IReadOnlyDictionary<string, JsonElement>? Filter { get; }

        public BmsRandomExecutableBar(string randomName, IReadOnlyDictionary<string, JsonElement>? filter)
        {
            RandomName = randomName;
            Filter = filter;
            Title = $"[RANDOM] {randomName}";
        }

        public override string Title { get; }
    }
}
