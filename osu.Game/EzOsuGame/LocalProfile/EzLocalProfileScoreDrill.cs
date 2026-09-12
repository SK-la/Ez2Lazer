// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public static class EzLocalProfileScoreDrillQuery
    {
        public static List<EzLocalProfileDrillScoreRow> Filter(IReadOnlyList<EzLocalProfileDrillScoreRow> scores, string searchText)
        {
            if (scores.Count == 0 || string.IsNullOrWhiteSpace(searchText))
                return scores.ToList();

            string term = searchText.Trim();
            var results = new List<EzLocalProfileDrillScoreRow>();

            foreach (var row in scores)
            {
                if (matchesSearch(row, term))
                    results.Add(row);
            }

            return results;
        }

        public static List<EzLocalProfileDrillScoreRow> PeersOnSameBeatmap(EzLocalProfileDrillScoreRow? row, IEnumerable<EzLocalProfileDrillScoreRow> allScores)
        {
            if (row == null)
                return new List<EzLocalProfileDrillScoreRow>();

            string hash = row.BeatmapHash;
            string version = row.DifficultyName;
            var results = new List<EzLocalProfileDrillScoreRow>();

            foreach (var peer in allScores)
            {
                if (peer.BeatmapHash == hash && peer.DifficultyName == version)
                    results.Add(peer);
            }

            return results;
        }

        /// <summary>
        /// 每张谱面（BeatmapId）只保留最高 PP 的一条，用于成绩记录列表展示。
        /// 输入假定已按 PP 降序；对无序输入先按 PP 降序排列。
        /// </summary>
        public static List<EzLocalProfileDrillScoreRow> BestPerBeatmap(IEnumerable<EzLocalProfileDrillScoreRow> scores)
        {
            var ordered = scores.OrderByDescending(s => s.PpResolved).ToList();
            var seen = new HashSet<Guid>();

            var result = new List<EzLocalProfileDrillScoreRow>();

            foreach (var row in ordered)
            {
                if (!seen.Add(row.BeatmapId))
                    continue;

                result.Add(row);
            }

            return result;
        }

        private static bool matchesSearch(EzLocalProfileDrillScoreRow row, string term)
        {
            if (double.TryParse(term, NumberStyles.Float, CultureInfo.InvariantCulture, out double ppTarget)
                && row.PpResolved > 0
                && Math.Abs(row.PpResolved - ppTarget) < 0.5)
            {
                return true;
            }

            return row.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                   || row.Artist.Contains(term, StringComparison.OrdinalIgnoreCase)
                   || row.MapperUsername.Contains(term, StringComparison.OrdinalIgnoreCase)
                   || row.DifficultyName.Contains(term, StringComparison.OrdinalIgnoreCase)
                   || row.PpResolved.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase);
        }
    }

    public partial class EzLocalProfileScoreSearchBox : OsuTextBox
    {
        public EzLocalProfileScoreSearchBox()
        {
            PlaceholderText = EzSettingsProfile.LOCAL_PROFILE_DRILL_SEARCH_PLACEHOLDER;
            RelativeSizeAxes = Axes.X;
        }
    }

    /// <summary>
    /// Persistent drill block: search filters the selector in place without recreating the text box.
    /// </summary>
    public partial class EzLocalProfileScoreDrillPanel : CompositeDrawable
    {
        private readonly Bindable<EzLocalProfileDrillScoreRow?> currentScore;
        private readonly Bindable<string> searchQuery;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow> allScores;

        private EzLocalProfileScoreSelector selector = null!;
        private OsuSpriteText noMatchesText = null!;
        private GridContainer resultsGrid = null!;

        public EzLocalProfileScoreDrillPanel(
            Bindable<EzLocalProfileDrillScoreRow?> currentScore,
            Bindable<string> searchQuery,
            IReadOnlyList<EzLocalProfileDrillScoreRow> allScores)
        {
            this.currentScore = currentScore;
            this.searchQuery = searchQuery;
            this.allScores = allScores;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var searchBox = new EzLocalProfileScoreSearchBox();
            if (!string.IsNullOrEmpty(searchQuery.Value))
                searchBox.Text = searchQuery.Value;

            searchBox.Current.BindValueChanged(e =>
            {
                searchQuery.Value = e.NewValue ?? string.Empty;
                applyFilter();
            });

            selector = new EzLocalProfileScoreSelector { Current = { BindTarget = currentScore } };

            noMatchesText = new OsuSpriteText
            {
                Text = EzSettingsProfile.LOCAL_PROFILE_DRILL_NO_MATCHES,
                Font = OsuFont.GetFont(size: 14),
            };

            resultsGrid = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, EzLocalProfileScoreSelector.WIDTH),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        selector,
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Margin = new MarginPadding { Left = 12 },
                            Child = new EzLocalProfileScoreDetailColumn(currentScore, allScores),
                        },
                    },
                },
            };

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    searchBox,
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            noMatchesText,
                            resultsGrid,
                        }
                    },
                }
            };

            applyFilter();
        }

        private void applyFilter()
        {
            var filtered = EzLocalProfileScoreDrillQuery.Filter(allScores, searchQuery.Value);

            // 成绩记录列表按谱面去重（每谱面最高 PP 一条）；详情列仍用全量 allScores 聚合 max/min/avg。
            var bestPerBeatmap = EzLocalProfileScoreDrillQuery.BestPerBeatmap(filtered);

            bool hasMatches = bestPerBeatmap.Count > 0;

            noMatchesText.Alpha = hasMatches ? 0 : 1;
            resultsGrid.Alpha = hasMatches ? 1 : 0;

            selector.SetScores(bestPerBeatmap);
        }
    }

    public partial class EzLocalProfileScoreSelector : CompositeDrawable
    {
        private const int entries_per_frame = 24;

        public Bindable<EzLocalProfileDrillScoreRow?> Current { get; } = new Bindable<EzLocalProfileDrillScoreRow?>();

        private readonly BindableList<EzLocalProfileDrillScoreRow> entries = new BindableList<EzLocalProfileDrillScoreRow>();
        private FillFlowContainer listFlow = null!;

        private CancellationTokenSource? rebuildCts;
        private ScheduledDelegate? batchDelegate;
        private int rebuildGeneration;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        public const float WIDTH = 280f;
        public const float HEIGHT = 360f;

        public EzLocalProfileScoreSelector()
        {
            Width = WIDTH;
            Height = HEIGHT;
        }

        public void SetScores(IEnumerable<EzLocalProfileDrillScoreRow> items)
        {
            entries.Clear();
            entries.AddRange(items);

            rebuildList();

            if (entries.Count == 0)
            {
                Current.Value = null;
                return;
            }

            if (Current.Value == null || entries.All(e => e.ScoreId != Current.Value.ScoreId))
                Current.Value = entries[0];
            else
                updateSelectionHighlight();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = listFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                }
            };

            Current.BindValueChanged(_ => updateSelectionHighlight());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuildList();
            updateSelectionHighlight();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelRebuild();
            base.Dispose(isDisposing);
        }

        private void cancelRebuild()
        {
            rebuildCts?.Cancel();
            rebuildCts?.Dispose();
            rebuildCts = null;
            batchDelegate?.Cancel();
            batchDelegate = null;
        }

        private void rebuildList()
        {
            cancelRebuild();
            listFlow.Clear();

            if (entries.Count == 0)
                return;

            int generation = ++rebuildGeneration;
            var rows = entries.ToList();
            var localCts = rebuildCts = new CancellationTokenSource();
            var localRulesets = rulesets;

            Task.Run(() =>
            {
                var prepared = new List<(EzLocalProfileDrillScoreRow Row, Mod[] Mods)>(rows.Count);

                foreach (var row in rows)
                {
                    localCts.Token.ThrowIfCancellationRequested();
                    prepared.Add((row, EzLocalProfileDrillMods.Resolve(row, localRulesets)));
                }

                return prepared;
            }, localCts.Token).ContinueWith(task => Schedule(() =>
            {
                if (generation != rebuildGeneration || localCts.IsCancellationRequested || task.IsCanceled)
                    return;

                if (task.IsFaulted)
                {
                    foreach (var row in rows)
                        listFlow.Add(new EzLocalProfileScoreNarrowCard(row, Array.Empty<Mod>(), () => Current.Value = row));
                    updateSelectionHighlight();
                    return;
                }

                startBatchedAdd(task.GetResultSafely(), generation);
            }), localCts.Token);
        }

        private void startBatchedAdd(IReadOnlyList<(EzLocalProfileDrillScoreRow Row, Mod[] Mods)> prepared, int generation)
        {
            int index = 0;

            void addBatch()
            {
                if (generation != rebuildGeneration)
                    return;

                int end = Math.Min(index + entries_per_frame, prepared.Count);

                for (; index < end; index++)
                {
                    var (row, mods) = prepared[index];
                    listFlow.Add(new EzLocalProfileScoreNarrowCard(row, mods, () => Current.Value = row));
                }

                if (index < prepared.Count)
                    batchDelegate = Scheduler.AddDelayed(addBatch, 0);
                else
                    updateSelectionHighlight();
            }

            addBatch();
        }

        private void updateSelectionHighlight()
        {
            Guid? selectedId = Current.Value?.ScoreId;

            foreach (var child in listFlow)
            {
                if (child is EzLocalProfileScoreNarrowCard entry)
                    entry.SetSelected(selectedId != null && entry.ScoreId == selectedId);
            }
        }
    }
}
