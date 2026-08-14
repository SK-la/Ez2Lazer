// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public enum BmsSongSelectFocusPane
    {
        Source,
        List,
    }

    public enum BmsSongSelectSourceKind
    {
        TablesRoot,
        Table,
        TableLevel,
        FolderRoot,
        Folder,
        FilterRoot,
        Filter,
        Search,
    }

    /// <summary>
    /// Qwilight-style navigation: left source (tables / folders / filters) + center chart list.
    /// Tables are isolated from favorite SQL filters.
    /// </summary>
    public sealed class BmsSongSelectNavigator
    {
        private readonly BmsBarContext context;
        private readonly IBmsDifficultyTableProvider tableProvider;
        private readonly List<BmsRajaSearchEntry> searchHistory = new List<BmsRajaSearchEntry>();

        private BmsDirectoryBar? currentDirectory;
        private string listFilterText = string.Empty;

        public BmsSongSelectFocusPane FocusPane { get; private set; } = BmsSongSelectFocusPane.Source;

        public IReadOnlyList<BmsBar> SourceBars { get; private set; } = Array.Empty<BmsBar>();

        public IReadOnlyList<BmsBar> ListBars { get; private set; } = Array.Empty<BmsBar>();

        public int SourceIndex { get; private set; }

        public int ListIndex { get; private set; }

        public string Breadcrumb { get; private set; } = string.Empty;

        public BmsSongSelectSourceKind SourceKind { get; private set; } = BmsSongSelectSourceKind.TablesRoot;

        public BmsTableBar? ActiveTable { get; private set; }

        public BmsHashBar? ActiveLevel { get; private set; }

        public event Action? Changed;

        public BmsSongSelectNavigator(BmsBarContext context, IBmsDifficultyTableProvider tableProvider)
        {
            this.context = context;
            this.tableProvider = tableProvider;
        }

        public void Reset()
        {
            currentDirectory = null;
            ActiveTable = null;
            ActiveLevel = null;
            listFilterText = string.Empty;
            FocusPane = BmsSongSelectFocusPane.Source;
            SourceKind = BmsSongSelectSourceKind.TablesRoot;
            rebuildSourceRoot();
            ListBars = Array.Empty<BmsBar>();
            ListIndex = 0;
            SourceIndex = findNearestSelectable(SourceBars, 0);
            updateBreadcrumb();
            Changed?.Invoke();
        }

        public void SetListFilter(string text)
        {
            listFilterText = text?.Trim() ?? string.Empty;

            if (ActiveLevel != null || currentDirectory != null)
                refreshList();

            Changed?.Invoke();
        }

        public void AddSearch(string query)
        {
            query = query.Trim();

            if (string.IsNullOrEmpty(query))
                return;

            searchHistory.RemoveAll(s => string.Equals(s.Query, query, StringComparison.OrdinalIgnoreCase));
            searchHistory.Insert(0, new BmsRajaSearchEntry(query, DateTime.UtcNow));

            if (searchHistory.Count > 8)
                searchHistory.RemoveRange(8, searchHistory.Count - 8);

            context.SearchHistory = searchHistory.ToList();
            openDirectory(new BmsSearchBar(query), BmsSongSelectSourceKind.Search);
        }

        public void MoveSource(int delta)
        {
            if (SourceBars.Count == 0)
                return;

            SourceIndex = stepIndex(SourceBars, SourceIndex, delta);
            Changed?.Invoke();
        }

        public void SelectSourceIndex(int index)
        {
            if (SourceBars.Count == 0)
                return;

            SourceIndex = findNearestSelectable(SourceBars, index);
            Changed?.Invoke();
        }

        public void MoveList(int delta)
        {
            if (ListBars.Count == 0)
                return;

            ListIndex = stepIndex(ListBars, ListIndex, delta);
            Changed?.Invoke();
        }

        public void SelectListIndex(int index)
        {
            if (ListBars.Count == 0)
                return;

            ListIndex = findNearestSelectable(ListBars, index);
            Changed?.Invoke();
        }

        public void FocusSource() => setFocus(BmsSongSelectFocusPane.Source);

        public void FocusList()
        {
            if (ListBars.Count == 0)
                return;

            setFocus(BmsSongSelectFocusPane.List);
        }

        public void ActivateSource()
        {
            var bar = GetSelectedSourceBar();

            if (bar == null || bar is BmsSectionLabelBar)
                return;

            switch (bar)
            {
                case BmsTableBar table:
                    openTable(table);
                    break;

                case BmsHashBar level:
                    openLevel(level);
                    break;

                case BmsDirectoryBar dir:
                    openDirectory(dir, inferSourceKind(dir));
                    break;
            }
        }

        public void ActivateList()
        {
            var bar = GetSelectedListBar();

            if (bar == null || bar is BmsSectionLabelBar or BmsMissingChartBar)
                return;

            if (bar is BmsDirectoryBar dir)
            {
                openDirectory(dir, inferSourceKind(dir));
                return;
            }

            // Song bars are played by the screen, not opened here.
        }

        public bool CanNavigateBack =>
            FocusPane == BmsSongSelectFocusPane.List
            || ActiveLevel != null
            || ActiveTable != null
            || currentDirectory != null;

        public bool TryGoBack()
        {
            if (!CanNavigateBack)
                return false;

            GoBack();
            return true;
        }

        public void GoBack()
        {
            if (FocusPane == BmsSongSelectFocusPane.List)
            {
                FocusSource();
                return;
            }

            if (ActiveLevel != null && ActiveTable != null)
            {
                openTable(ActiveTable);
                return;
            }

            if (ActiveTable != null || currentDirectory != null)
            {
                Reset();
                return;
            }
        }

        public BmsBar? GetSelectedSourceBar()
            => SourceBars.Count == 0 ? null : SourceBars[Math.Clamp(SourceIndex, 0, SourceBars.Count - 1)];

        public BmsBar? GetSelectedListBar()
            => ListBars.Count == 0 ? null : ListBars[Math.Clamp(ListIndex, 0, ListBars.Count - 1)];

        public BmsSongBar? GetSelectedSong()
        {
            if (FocusPane == BmsSongSelectFocusPane.List && GetSelectedListBar() is BmsSongBar song)
                return song;

            if (GetSelectedListBar() is BmsSongBar fallback)
                return fallback;

            return null;
        }

        public BmsMissingChartBar? GetSelectedMissingChart()
        {
            if (GetSelectedListBar() is BmsMissingChartBar missing)
                return missing;

            return null;
        }

        public BmsBar? GetDetailBar()
        {
            if (FocusPane == BmsSongSelectFocusPane.List)
                return GetSelectedListBar() ?? GetSelectedSourceBar();

            return GetSelectedSourceBar() ?? GetSelectedListBar();
        }

        private void openTable(BmsTableBar table)
        {
            ActiveTable = table;
            ActiveLevel = null;
            currentDirectory = table;
            SourceKind = BmsSongSelectSourceKind.Table;
            SourceBars = table.GetChildren(context).ToList();
            SourceIndex = findNearestSelectable(SourceBars, 0);
            ListBars = Array.Empty<BmsBar>();
            ListIndex = 0;
            FocusPane = BmsSongSelectFocusPane.Source;
            updateBreadcrumb();
            Changed?.Invoke();
        }

        private void openLevel(BmsHashBar level)
        {
            ActiveLevel = level;
            ActiveTable ??= new BmsTableBar(level.Table);
            currentDirectory = level;
            SourceKind = BmsSongSelectSourceKind.TableLevel;
            refreshList();
            FocusPane = BmsSongSelectFocusPane.List;
            updateBreadcrumb();
            Changed?.Invoke();
        }

        private void openDirectory(BmsDirectoryBar dir, BmsSongSelectSourceKind kind)
        {
            ActiveTable = null;
            ActiveLevel = null;
            currentDirectory = dir;
            SourceKind = kind;

            // Keep root sources for folders/filters on the left; push children into list.
            if (kind is BmsSongSelectSourceKind.Folder or BmsSongSelectSourceKind.Filter or BmsSongSelectSourceKind.Search)
            {
                rebuildSourceRoot();
                selectSourceMatching(dir);
                refreshListFromDirectory(dir);
                FocusPane = BmsSongSelectFocusPane.List;
            }
            else
            {
                SourceBars = dir.GetChildren(context).ToList();
                SourceIndex = findNearestSelectable(SourceBars, 0);
                refreshListFromDirectory(dir);
                FocusPane = ListBars.Any(b => b is BmsSongBar or BmsMissingChartBar)
                    ? BmsSongSelectFocusPane.List
                    : BmsSongSelectFocusPane.Source;
            }

            updateBreadcrumb();
            Changed?.Invoke();
        }

        private void refreshList()
        {
            if (ActiveLevel != null)
            {
                refreshListFromDirectory(ActiveLevel);
                return;
            }

            if (currentDirectory != null)
                refreshListFromDirectory(currentDirectory);
        }

        private void refreshListFromDirectory(BmsDirectoryBar dir)
        {
            IReadOnlyList<BmsBar> children = dir.GetChildren(context);
            IEnumerable<BmsBar> filtered = children;

            if (!string.IsNullOrEmpty(listFilterText)
                && (ActiveLevel != null || SourceKind == BmsSongSelectSourceKind.TableLevel))
            {
                filtered = children.Where(bar => barMatchesFilter(bar, listFilterText));
            }
            else if (!string.IsNullOrEmpty(listFilterText)
                     && SourceKind is BmsSongSelectSourceKind.Folder or BmsSongSelectSourceKind.Filter or BmsSongSelectSourceKind.Search)
            {
                filtered = children.Where(bar => barMatchesFilter(bar, listFilterText));
            }

            var list = filtered.Where(b => b is BmsSongBar or BmsMissingChartBar or BmsDirectoryBar).ToList();

            if (dir.IsSortable)
                list = context.SortPolicy.Sort(list).ToList();

            ListBars = list;
            ListIndex = findNearestSelectable(ListBars, ListIndex);
        }

        private void rebuildSourceRoot()
        {
            var bars = new List<BmsBar>
            {
                new BmsSectionLabelBar("── 难度表 ──"),
            };

            var tables = tableProvider.GetTables();

            if (tables.Count == 0)
                bars.Add(new BmsPlaceholderHintBar("将 .bmt/.json 放入 EzBMS/tables，或使用「添加表 URL」"));
            else
                bars.AddRange(tables);

            bars.Add(new BmsSectionLabelBar("── 曲库文件夹 ──"));
            bars.AddRange(context.FolderTree.GetRootBars());

            bars.Add(new BmsSectionLabelBar("── 过滤 / 收藏 ──"));
            bars.AddRange(BmsFolderConfigLoader.BuildRootCommandBars(context));

            if (searchHistory.Count > 0)
            {
                bars.Add(new BmsSectionLabelBar("── 搜索历史 ──"));

                foreach (var search in searchHistory.Take(5))
                    bars.Add(new BmsSearchBar(search.Query));
            }

            SourceBars = bars;
            SourceIndex = findNearestSelectable(SourceBars, SourceIndex);
        }

        private void selectSourceMatching(BmsDirectoryBar dir)
        {
            for (int i = 0; i < SourceBars.Count; i++)
            {
                if (ReferenceEquals(SourceBars[i], dir) || SourceBars[i].Title == dir.Title)
                {
                    SourceIndex = i;
                    return;
                }
            }
        }

        private void setFocus(BmsSongSelectFocusPane pane)
        {
            FocusPane = pane;
            Changed?.Invoke();
        }

        private void updateBreadcrumb()
        {
            if (ActiveLevel != null && ActiveTable != null)
            {
                Breadcrumb = $"{ActiveTable.Title} / {ActiveLevel.Title}";
                return;
            }

            if (ActiveTable != null)
            {
                Breadcrumb = ActiveTable.Title;
                return;
            }

            if (currentDirectory != null)
            {
                Breadcrumb = currentDirectory.Title;
                return;
            }

            Breadcrumb = "BMS";
        }

        private static BmsSongSelectSourceKind inferSourceKind(BmsDirectoryBar dir) => dir switch
        {
            BmsTableBar => BmsSongSelectSourceKind.Table,
            BmsHashBar => BmsSongSelectSourceKind.TableLevel,
            BmsSearchBar => BmsSongSelectSourceKind.Search,
            BmsFolderBar => BmsSongSelectSourceKind.Folder,
            _ => BmsSongSelectSourceKind.Filter,
        };

        private static bool barMatchesFilter(BmsBar bar, string filter)
        {
            return bar.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                   || bar.Subtitle.Contains(filter, StringComparison.OrdinalIgnoreCase)
                   || (bar is BmsSongBar song
                       && (song.Summary.Artist.Contains(filter, StringComparison.OrdinalIgnoreCase)
                           || song.Summary.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                   || (bar is BmsMissingChartBar missing
                       && (missing.Entry.Artist.Contains(filter, StringComparison.OrdinalIgnoreCase)
                           || missing.Entry.PreferredHash.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }

        private static int stepIndex(IReadOnlyList<BmsBar> bars, int start, int delta)
        {
            if (bars.Count == 0)
                return 0;

            int index = start;

            for (int step = 0; step < bars.Count; step++)
            {
                index = (index + delta + bars.Count) % bars.Count;

                if (bars[index] is not BmsSectionLabelBar)
                    break;
            }

            return index;
        }

        private static int findNearestSelectable(IReadOnlyList<BmsBar> bars, int start)
        {
            if (bars.Count == 0)
                return 0;

            start = Math.Clamp(start, 0, bars.Count - 1);

            if (bars[start] is not BmsSectionLabelBar)
                return start;

            for (int i = start + 1; i < bars.Count; i++)
            {
                if (bars[i] is not BmsSectionLabelBar)
                    return i;
            }

            for (int i = start - 1; i >= 0; i--)
            {
                if (bars[i] is not BmsSectionLabelBar)
                    return i;
            }

            return 0;
        }
    }

    internal sealed class BmsPlaceholderHintBar : BmsBar
    {
        public BmsPlaceholderHintBar(string title) => Title = title;

        public override string Title { get; }
    }
}
