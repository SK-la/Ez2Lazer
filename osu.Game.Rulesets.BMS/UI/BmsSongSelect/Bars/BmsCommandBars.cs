// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
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
        {
            var charts = context.SqlQuery.Execute(sql);
            var bars = charts.Select(c => new BmsSongBar(c)).Cast<BmsBar>().ToList();

            if (!showInvisible)
                bars = bars.Where(b => b is not BmsSongBar song || context.KeyModeFilter.Matches(song.Chart.KeyCount)).ToList();

            return bars;
        }
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

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context) => context.SqlQuery.SearchByText(Query).Select(c => new BmsSongBar(c)).Cast<BmsBar>().ToList();
    }

    public sealed class BmsSameFolderBar : BmsDirectoryBar
    {
        private readonly string folderCrc;

        public BmsSameFolderBar(string folderPath)
        {
            folderCrc = BmsPathCrc.Compute(folderPath);
            Title = BmsStrings.RAJA_SAME_FOLDER_FILTER_TITLE.ToString();
        }

        public override string Title { get; }

        public override bool IsSortable => true;

        public override IReadOnlyList<BmsBar> GetChildren(BmsBarContext context) => context.FolderTree.GetChildren(folderCrc);
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
