// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 显示每种判定的 早/晚 数量；有 IHasColumn 接口的谱面（如 Mania）将按每列单独统计。
    /// </summary>
    public partial class EzScoreHitResultCountGraph : CompositeDrawable
    {
        private readonly IReadOnlyList<HitEvent> hitEvents;
        private readonly int columnCount;

        /// <summary>
        /// 规则集实例，子类可以覆写此属性以返回不同类型的 Ruleset 实例。
        /// </summary>
        protected virtual Ruleset RulesetInstance { get; set; }

        private Color4 totalColour = Color4Extensions.FromHex(@"ff8c00");

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzScoreHitResultCountGraph(ScoreInfo score)
        {
            hitEvents = score.HitEvents.ToList();

            columnCount = hitEvents.Where(e => e.HitObject is IHasColumn).Select(e => ((IHasColumn)e.HitObject).Column).DefaultIfEmpty(-1).Max() + 1;

            // 默认使用 ScoreInfo 中记录的规则集实例，派生类可在构造时或通过覆写属性返回其它规则集实例。
            RulesetInstance = score.Ruleset.CreateInstance();

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEvents.Count == 0)
                return;

            totalColour = colours.Orange1;

            var groupedGraphs = columnCount > 0
                ? createColumnGraphs()
                : new List<Drawable>
                {
                    createStatisticsGroup(hitEvents, calculateColumnStatistics(hitEvents))
                };

            if (groupedGraphs.Count == 0)
                return;

            // 保留其余现有布局逻辑。
            const int columns_per_row = 2;
            int rowCount = (groupedGraphs.Count + columns_per_row - 1) / columns_per_row;

            var gridContent = new Drawable[rowCount][];

            for (int i = 0; i < rowCount; i++)
            {
                gridContent[i] = new Drawable[columns_per_row];

                for (int j = 0; j < columns_per_row; j++)
                {
                    int index = i * columns_per_row + j;

                    if (index < groupedGraphs.Count)
                    {
                        gridContent[i][j] = groupedGraphs[index];

                        if (groupedGraphs.Count % 2 == 1 && i == rowCount - 1 && j == 0)
                        {
                            var position = gridContent[i][j].Position;
                            position.X += 228;
                            gridContent[i][j].Position = position;
                        }
                    }
                    else
                        gridContent[i][j] = Empty();
                }
            }

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = Enumerable.Range(0, rowCount).Select(_ => new Dimension(GridSizeMode.AutoSize)).ToArray(),
                ColumnDimensions = Enumerable.Range(0, columns_per_row).Select(_ => new Dimension()).ToArray(),
                Content = gridContent
            };
        }

        private List<Drawable> createColumnGraphs()
        {
            var eventsByColumn = new List<HitEvent>?[columnCount];

            foreach (var hitEvent in hitEvents)
            {
                if (hitEvent.HitObject is not IHasColumn hasColumn)
                    continue;

                int col = hasColumn.Column;
                var list = eventsByColumn[col];

                if (list == null)
                {
                    list = new List<HitEvent>(Math.Max(4, hitEvents.Count / Math.Max(1, columnCount)));
                    eventsByColumn[col] = list;
                }

                list.Add(hitEvent);
            }

            var graphs = new List<Drawable>();

            for (int i = 0; i < columnCount; i++)
            {
                var columnEvents = eventsByColumn[i];

                if (columnEvents == null)
                    continue;

                var content = createStatisticsGroup(columnEvents, calculateColumnStatistics(columnEvents), $"Column {i + 1}");

                graphs.Add(content);
            }

            return graphs;
        }

        private FillFlowContainer createStatisticsGroup(IReadOnlyList<HitEvent> groupHitEvents, ColumnStatistics stats, string? title = null)
        {
            var groupContainer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical
            };

            groupContainer.Add(new HitEventTimingDistributionGraph(groupHitEvents)
            {
                RelativeSizeAxes = Axes.X,
                Height = 100,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Scale = new Vector2(0.96f),
                Margin = new MarginPadding { Top = 5, Bottom = 10 }
            });

            if (!string.IsNullOrEmpty(title))
            {
                groupContainer.Add(new OsuSpriteText
                {
                    Text = title,
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.Bold),
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre
                });
            }

            groupContainer.Add(new AverageHitError(groupHitEvents)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Scale = new Vector2(0.96f)
            });

            groupContainer.Add(new UnstableRate(groupHitEvents)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Scale = new Vector2(0.96f)
            });

            groupContainer.Add(createStatisticTable(stats));

            return groupContainer;
        }

        /// <summary>
        /// 一次遍历计算单列的所有统计信息
        /// </summary>
        private ColumnStatistics calculateColumnStatistics(IReadOnlyList<HitEvent> columnEvents)
        {
            var stats = new ColumnStatistics();

            // 对列事件进行单次遍历 —— 更高效
            foreach (var hitEvent in columnEvents)
            {
                bool isLate = hitEvent.TimeOffset > 0;
                var r = hitEvent.Result;

                stats.Total.TryAdd(r, 0);

                stats.Total[r] += 1;
                stats.TotalAll++;

                if (isLate)
                {
                    stats.Late.TryAdd(r, 0);

                    stats.Late[r] += 1;
                    stats.TotalLateAll++;
                }
                else
                {
                    stats.Early.TryAdd(r, 0);

                    stats.Early[r] += 1;
                    stats.TotalEarlyAll++;
                }
            }

            return stats;
        }

        /// <summary>
        /// 列统计的数据结构，用于缓存计算结果。
        /// 通过字典支持任意的 HitResult 值。
        /// </summary>
        private class ColumnStatistics
        {
            public readonly Dictionary<HitResult, int> Total = new Dictionary<HitResult, int>();
            public readonly Dictionary<HitResult, int> Early = new Dictionary<HitResult, int>();
            public readonly Dictionary<HitResult, int> Late = new Dictionary<HitResult, int>();

            public int TotalAll;
            public int TotalEarlyAll;
            public int TotalLateAll;
        }

        /// <summary>
        /// 从预计算的列统计生成统计表
        /// </summary>
        private SimpleStatisticTable createStatisticTable(ColumnStatistics stats)
        {
            var items = new List<SimpleStatisticItem<string>>
            {
                makeSimpleStat(stats.TotalAll.ToString(), "Total", totalColour),
                makeSimpleStat(stats.TotalEarlyAll.ToString(), "Total (Early)", ColourInfo.GradientVertical(Colour4.White, totalColour)),
                makeSimpleStat(stats.TotalLateAll.ToString(), "Total (Late)", ColourInfo.GradientVertical(totalColour, Colour4.White)),
            };

            // 按规则集提供的顺序排序（若可用），否则使用枚举上定义的展示顺序。
            // 另外，确保把统计中实际存在但规则集未列出的特殊判定也展示出来（例如 ComboBreak、Poor）。
            List<HitResult> results = RulesetInstance.GetValidHitResults()
                                                     .Where(r => !r.ToString().Contains("Ignore"))
                                                     .Where(r => stats.Total.ContainsKey(r))
                                                     .OrderBy(r => r.GetIndexForOrderedDisplay())
                                                     .ToList();

            foreach (HitResult r in results)
            {
                // string name = RulesetInstance.GetDisplayNameForHitResult(r).ToString();
                string name = r.GetHitModeDisplayName().ToString();

                stats.Total.TryGetValue(r, out int total);
                stats.Early.TryGetValue(r, out int early);
                stats.Late.TryGetValue(r, out int late);

                var c = colours.ForHitResult(r);

                items.Add(makeSimpleStat(total.ToString(), name, c));
                items.Add(makeSimpleStat(early.ToString(), name + " (Early)", ColourInfo.GradientVertical(Colour4.White, c)));
                items.Add(makeSimpleStat(late.ToString(), name + " (Late)", ColourInfo.GradientVertical(c, Colour4.White)));
            }

            return new SimpleStatisticTable(3, items.ToArray())
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Scale = new Vector2(0.96f)
            };
        }

        private SimpleStatisticItem<string> makeSimpleStat(string display, string name = "Count", ColourInfo? colour = null)
        {
            var item = new SimpleStatisticItem<string>(name) { Value = display };
            item.Colour = colour ?? Colour4.White;
            return item;
        }
    }
}
