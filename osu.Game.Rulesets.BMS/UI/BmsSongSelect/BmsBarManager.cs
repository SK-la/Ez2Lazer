// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public sealed class BmsBarManager
    {
        private readonly BmsBarContext context;
        private readonly IBmsDifficultyTableProvider tableProvider;
        private readonly IReadOnlyList<BmsRajaRandomDefinition> randomDefinitions;
        private readonly Stack<BmsDirectoryBar> directoryStack = new Stack<BmsDirectoryBar>();
        private readonly Stack<BmsBar> sourceBars = new Stack<BmsBar>();
        private readonly List<BmsRajaSearchEntry> searchHistory = new List<BmsRajaSearchEntry>();

        public IReadOnlyList<BmsBar> CurrentBars { get; private set; } = Array.Empty<BmsBar>();
        public int SelectedIndex { get; private set; }
        public string Breadcrumb { get; private set; } = string.Empty;

        public event Action? Changed;

        public BmsBarManager(BmsBarContext context, IBmsDifficultyTableProvider? tableProvider = null)
        {
            this.context = context;
            this.tableProvider = tableProvider ?? new BmsDifficultyTableRegistry();
            randomDefinitions = BmsFolderConfigLoader.LoadRandomDefinitions();
        }

        public void ResetToRoot() => UpdateBar(null);

        public void UpdateBar(BmsDirectoryBar? bar)
        {
            if (bar == null)
            {
                directoryStack.Clear();
                sourceBars.Clear();
                CurrentBars = buildRootBars();
            }
            else
            {
                if (directoryStack.Contains(bar))
                {
                    while (directoryStack.Count > 0 && directoryStack.Peek() != bar)
                    {
                        directoryStack.Pop();
                        if (sourceBars.Count > 0)
                            sourceBars.Pop();
                    }
                }
                else
                {
                    directoryStack.Push(bar);
                }

                CurrentBars = bar.GetChildren(context);
            }

            CurrentBars = applyPipeline(CurrentBars);
            SelectedIndex = findNearestSelectableIndex(SelectedIndex);
            updateBreadcrumb();
            Changed?.Invoke();
        }

        private int findNearestSelectableIndex(int start)
        {
            if (CurrentBars.Count == 0)
                return 0;

            start = Math.Clamp(start, 0, CurrentBars.Count - 1);
            if (CurrentBars[start] is not BmsSectionLabelBar)
                return start;

            for (int i = start + 1; i < CurrentBars.Count; i++)
            {
                if (CurrentBars[i] is not BmsSectionLabelBar)
                    return i;
            }

            for (int i = start - 1; i >= 0; i--)
            {
                if (CurrentBars[i] is not BmsSectionLabelBar)
                    return i;
            }

            return 0;
        }

        public void CloseFolder()
        {
            if (directoryStack.Count == 0)
            {
                context.SortPolicy.CycleNext();
                CurrentBars = applyPipeline(buildRootBars());
                Changed?.Invoke();
                return;
            }

            directoryStack.Pop();
            if (sourceBars.Count > 0)
                sourceBars.Pop();

            var parent = directoryStack.Count > 0 ? directoryStack.Peek() : null;
            UpdateBar(parent);
        }

        public void MoveSelection(int delta)
        {
            if (CurrentBars.Count == 0)
                return;

            int index = SelectedIndex;

            for (int step = 0; step < CurrentBars.Count; step++)
            {
                index = (index + delta + CurrentBars.Count) % CurrentBars.Count;
                if (CurrentBars[index] is not BmsSectionLabelBar)
                    break;
            }

            SelectedIndex = index;
            Changed?.Invoke();
        }

        public BmsBar? GetSelectedBar() => CurrentBars.Count == 0 ? null : CurrentBars[SelectedIndex];

        public void OpenSelected()
        {
            var bar = GetSelectedBar();
            if (bar == null || bar is BmsSectionLabelBar)
                return;

            if (bar is BmsDirectoryBar dir)
            {
                sourceBars.Push(bar);
                UpdateBar(dir);
                return;
            }

            if (bar is BmsRandomExecutableBar randomBar)
            {
                var songs = CurrentBars.OfType<BmsSongBar>().Where(s => BmsRandomFilterEvaluator.Matches(s, randomBar.Filter, context)).ToList();
                if (songs.Count == 0)
                    return;

                var pick = songs[Random.Shared.Next(songs.Count)];
                SelectedIndex = Math.Max(0, CurrentBars.ToList().IndexOf(pick));
                Changed?.Invoke();
            }
        }

        public void ShowSameFolder()
        {
            if (GetSelectedBar() is not BmsSongBar song)
                return;

            var sameFolder = new BmsSameFolderBar(song.Chart.FolderPath);
            sourceBars.Push(sameFolder);
            UpdateBar(sameFolder);
        }

        public void AddSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            searchHistory.Insert(0, new BmsRajaSearchEntry(query.Trim(), DateTime.UtcNow));
            context.SearchHistory = searchHistory.Take(20).ToList();

            var searchBar = new BmsSearchBar(query.Trim());
            sourceBars.Push(searchBar);
            UpdateBar(searchBar);
        }

        public BmsSongBar? GetSelectedSong()
        {
            if (GetSelectedBar() is BmsSongBar song)
                return song;

            if (GetSelectedBar() is BmsRandomExecutableBar)
            {
                OpenSelected();
                return GetSelectedBar() as BmsSongBar;
            }

            return null;
        }

        private IReadOnlyList<BmsBar> buildRootBars()
        {
            var bars = new List<BmsBar>();

            var roots = context.FolderTree.GetRootBars();

            if (roots.Count > 0)
            {
                bars.Add(new BmsSectionLabelBar("── 曲库文件夹 ──"));
                bars.AddRange(roots);
            }

            var commands = BmsFolderConfigLoader.BuildRootCommandBars(context);

            if (commands.Count > 0)
            {
                bars.Add(new BmsSectionLabelBar("── 过滤 / 收藏 / 等级 ──"));
                bars.AddRange(commands);
            }

            var tables = tableProvider.GetTables().Cast<BmsBar>().ToList();

            if (tables.Count > 0)
            {
                bars.Add(new BmsSectionLabelBar("── 难度表 (TODO) ──"));
                bars.AddRange(tables);
            }

            if (searchHistory.Count > 0)
            {
                bars.Add(new BmsSectionLabelBar("── 搜索历史 ──"));
                foreach (var search in searchHistory.Take(5))
                    bars.Add(new BmsSearchBar(search.Query));
            }

            return bars;
        }

        private IReadOnlyList<BmsBar> applyPipeline(IReadOnlyList<BmsBar> input)
        {
            var bars = input.ToList();

            for (int attempt = 0; attempt < 6; attempt++)
            {
                bars = bars.Where(shouldShow).ToList();
                if (bars.Any(b => b is BmsSongBar))
                    break;

                context.KeyModeFilter.CycleNext();
            }

            if (directoryStack.Count == 0 || directoryStack.Peek().IsSortable)
                bars = context.SortPolicy.Sort(bars).ToList();

            bars = prependRandomBars(bars);
            return bars;
        }

        private bool shouldShow(BmsBar bar)
        {
            if (bar is BmsSectionLabelBar)
                return true;

            if (bar is BmsSongBar song)
                return context.KeyModeFilter.Matches(song.Chart.KeyCount);

            return true;
        }

        private List<BmsBar> prependRandomBars(List<BmsBar> bars)
        {
            var songs = bars.OfType<BmsSongBar>().ToList();
            var randomBars = new List<BmsBar>();

            foreach (var def in randomDefinitions)
            {
                int matchCount = string.IsNullOrEmpty(def.Name) || def.Name == "RANDOM SELECT"
                    ? songs.Count
                    : songs.Count(s => BmsRandomFilterEvaluator.Matches(s, def.Filter, context));

                int required = def.Filter == null || def.Filter.Count == 0 ? 2 : 1;
                if (matchCount >= required)
                    randomBars.Add(new BmsRandomExecutableBar(def.Name, def.Filter));
            }

            randomBars.AddRange(bars);
            return randomBars;
        }

        public void SetSelectedIndex(int index)
        {
            if (CurrentBars.Count == 0)
                return;

            SelectedIndex = Math.Clamp(index, 0, CurrentBars.Count - 1);
            Changed?.Invoke();
        }

        private void updateBreadcrumb()
        {
            if (directoryStack.Count == 0)
            {
                Breadcrumb = "ROOT";
                return;
            }

            Breadcrumb = string.Join(" > ", directoryStack.Select(d => d.Title));
        }
    }
}
