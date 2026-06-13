// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzDisplayKpc : CompositeDrawable, IHasCustomTooltip<LocalisableString>
    {
        internal static readonly LocalisableString KPC_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "每列Note数:蓝色是note，黄色是LN",
            "Notes per column: blue is notes, yellow is LNs");

        private EzManiaSummary? maniaSummary;

        public EzManiaSummary? ManiaSummary
        {
            get => maniaSummary;
            set
            {
                if (value == null)
                {
                    maniaSummary = null;
                    clear();
                    return;
                }

                bool dataChanged = maniaSummary != value;
                maniaSummary = value;

                // 只在数据真正变化时才更新显示
                if (dataChanged)
                    onDataChanged();
            }
        }

        private readonly Box background;
        private readonly FillFlowContainer columnNotesContainer;

        private List<BarChartColumnEntry>? barEntries;

        // 用于缓存列数，以避免每次更新都重建 UI
        private int currentColumnCount;

        // 缓存上次已知的列音符数量和面条数量，以便在模式切换时快速刷新显示而无需重新计算（如果数据未变）
        private int[]? lastKnownColumns;
        private int[]? lastKnownHolds;
        private int lastKnownCount;

        /// <summary>
        /// Whether the capsule background is shown.
        /// </summary>
        public bool ShowBackground
        {
            get => background.IsPresent;
            set
            {
                if (value)
                    background.Show();
                else
                    background.Hide();
            }
        }

        public EzDisplayKpc()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            InternalChild = new Container
            {
                Masking = true,
                CornerRadius = 6,
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4Extensions.FromHex("303d47"),
                    },
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding { Horizontal = 7f, Vertical = 1.5f },
                        Children = new[]
                        {
                            columnNotesContainer = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(5f, 0f),
                            },
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 数据变化时调用（ManiaSummary setter）
        /// </summary>
        private void onDataChanged()
        {
            if (maniaSummary == null)
                return;

            // 解析并缓存数据
            parseAndCacheData(maniaSummary.Value.ColumnCounts, maniaSummary.Value.HoldNoteCounts);

            // 渲染显示
            if (lastKnownColumns != null)
                rebuildAndRender(lastKnownColumns, lastKnownHolds, lastKnownCount);
        }

        /// <summary>
        /// 解析并缓存数据
        /// </summary>
        private void parseAndCacheData(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            if (columnNoteCounts.Count == 0)
            {
                clear();
                return;
            }

            // 从 columnNoteCounts 的最大 key + 1 推断列数
            int maxKey = 0;

            foreach (int k in columnNoteCounts.Keys)
            {
                if (k > maxKey)
                    maxKey = k;
            }

            int kc = maxKey + 1;
            var pool = ArrayPool<int>.Shared;
            int[] normalized = pool.Rent(Math.Max(1, kc));
            int[] normalizedHold = pool.Rent(Math.Max(1, kc));

            try
            {
                for (int i = 0; i < kc; i++)
                {
                    normalized[i] = columnNoteCounts.GetValueOrDefault(i);
                    normalizedHold[i] = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                }

                // 缓存数据
                if (lastKnownColumns == null || lastKnownColumns.Length < kc)
                    lastKnownColumns = new int[kc];

                if (lastKnownHolds == null || lastKnownHolds.Length < kc)
                    lastKnownHolds = new int[kc];

                Array.Copy(normalized, lastKnownColumns, kc);
                Array.Copy(normalizedHold, lastKnownHolds, kc);
                lastKnownCount = kc;
            }
            finally
            {
                pool.Return(normalized, clearArray: true);
                pool.Return(normalizedHold, clearArray: true);
            }
        }

        /// <summary>
        /// 根据当前模式和列数重建 UI 并渲染数值
        /// </summary>
        private void rebuildAndRender(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            // 步骤 1: 确保 UI 元素数量与列数匹配
            ensureColumnEntries(columns);

            // 步骤 2: 更新数值显示
            renderValues(columnNoteCounts, holdNoteCounts, columns);
        }

        /// <summary>
        /// 确保 UI 元素数量与目标列数匹配
        /// </summary>
        private void ensureColumnEntries(int targetColumns)
        {
            if (currentColumnCount == targetColumns)
                return;

            // 清空容器并释放旧 Entry
            columnNotesContainer.Clear();

            if (barEntries != null)
            {
                foreach (var entry in barEntries)
                    entry.Dispose();
                barEntries = null;
            }

            // 创建新的 BarChart Entry 列表
            barEntries = new List<BarChartColumnEntry>(targetColumns);

            for (int i = 0; i < targetColumns; i++)
            {
                var entry = new BarChartColumnEntry();
                barEntries.Add(entry);
                columnNotesContainer.Add(entry.Container);
            }

            currentColumnCount = targetColumns;
        }

        /// <summary>
        /// 渲染数值到现有的 UI 元素
        /// </summary>
        private void renderValues(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            renderBarChartValues(columnNoteCounts, holdNoteCounts, columns);
        }

        /// <summary>
        /// 渲染 BarChart 模式的数值
        /// </summary>
        private void renderBarChartValues(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            if (barEntries == null) return;

            // 计算最大值用于归一化。
            // 对齐: columnNoteCounts already contains total hit objects for the column (includes LNs).
            // 因此归一化应基于 total，而不是 total + hold（否则会重复计入 LN）。
            int maxCount = 0;

            for (int i = 0; i < currentColumnCount; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                if (total > maxCount)
                    maxCount = total;
            }

            if (maxCount == 0)
            {
                for (int i = 0; i < currentColumnCount; i++)
                {
                    barEntries[i].SetValues(0, 0, 1);
                }

                return;
            }

            for (int i = 0; i < currentColumnCount; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;

                barEntries[i].SetValues(total, hold, maxCount);
            }
        }

        #region ColumnEntry

        private class BarChartColumnEntry : IDisposable
        {
            private const float bar_width = 7f;
            private const float max_bar_height = 12f;
            private static readonly Color4 hold_note_color = Color4Extensions.FromHex("#FFD39B");
            private static readonly Color4 note_color = Color4Extensions.FromHex("#4DA6FF");

            public readonly Container Container;
            private readonly Box regularBox;
            private readonly Box holdBox;

            private int lastTotalNotes = int.MinValue;
            private int lastHoldNotes = int.MinValue;
            private int lastMaxCount = int.MinValue;

            public BarChartColumnEntry()
            {
                // Match SpreadDisplay's largest dot: width 7, height 12 (maxBarHeight).
                Container = new Container
                {
                    Size = new Vector2(bar_width, max_bar_height),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding(-1.5f),
                };

                // Create a masked bar area with fixed pixel size so mask cropping is exact.
                var barArea = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(bar_width, max_bar_height),
                    Masking = true,
                    CornerRadius = bar_width / 2f,
                };

                // background inside mask so corners are applied consistently
                var background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Gray.Opacity(0.2f),
                };

                regularBox = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    Colour = note_color,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                };

                holdBox = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    Colour = hold_note_color,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                };

                barArea.AddRange(new Drawable[] { background, regularBox, holdBox });

                var children = new List<Drawable>
                {
                    barArea,
                };

                Container.Children = children.ToArray();
            }

            public void SetValues(int totalNotes, int holdNotes, int maxCount)
            {
                if (lastTotalNotes == totalNotes && lastHoldNotes == holdNotes && lastMaxCount == maxCount)
                    return;

                lastTotalNotes = totalNotes;
                lastHoldNotes = holdNotes;
                lastMaxCount = maxCount;

                int regularNotes = totalNotes - holdNotes;

                float totalHeight = maxCount > 0 ? (float)totalNotes / maxCount * max_bar_height : 0;
                float regularHeight = maxCount > 0 ? (float)regularNotes / maxCount * max_bar_height : 0;

                float holdHeight = Math.Max(0, totalHeight - regularHeight);
                regularBox.Height = regularHeight;
                holdBox.Height = holdHeight;
                holdBox.Margin = new MarginPadding { Bottom = regularHeight };
            }

            public void Dispose()
            {
            }
        }

        #endregion

        private void clear()
        {
            // 释放 Entry 对象
            if (barEntries != null)
            {
                foreach (var entry in barEntries)
                    entry.Dispose();
                barEntries = null;
            }

            // 只在未 dispose 状态下才清空容器（避免线程安全问题）
            if (!IsDisposed)
                columnNotesContainer.Clear();

            currentColumnCount = 0;

            lastKnownColumns = null;
            lastKnownHolds = null;
            lastKnownCount = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                maniaSummary = null;
                clear();
            }
        }

#region Tooltip 文本生成

        /// <summary>
        /// 生成动态的 tooltip 文本（Markdown 表格格式）
        /// </summary>
        private LocalisableString generateTooltipText()
        {
            if (maniaSummary == null || lastKnownColumns == null)
                return KPC_TOOLTIP;

            // 构建 Markdown 表格
            var sb = new StringBuilder();

            // 表头（第一列为空）
            sb.Append("|  | ");

            for (int i = 0; i < lastKnownCount; i++)
            {
                sb.Append($"N{i + 1} | ");
            }

            sb.AppendLine();

            // 分隔线（第一列为空）
            sb.Append("| --- |");

            for (int i = 0; i < lastKnownCount; i++)
            {
                sb.Append(" ------ |");
            }

            sb.AppendLine();

            // Note 行 —— 显示普通 note（regular = total - hold），遵循口径 A（ColumnCounts 为总数）
            sb.Append("| notes |");

            for (int i = 0; i < lastKnownCount; i++)
            {
                int total = lastKnownColumns[i];
                int hold = lastKnownHolds?[i] ?? 0;
                int regular = Math.Max(0, total - hold);
                sb.Append($" {(regular > 0 ? regular.ToString() : "-")} |");
            }

            sb.AppendLine();

            // LN 行
            sb.Append("| LNs |");

            for (int i = 0; i < lastKnownCount; i++)
            {
                int count = lastKnownHolds?[i] ?? 0;
                sb.Append($" {(count > 0 ? count.ToString() : "-")} |");
            }

            sb.AppendLine();

            return sb.ToString();
        }

        public ITooltip<LocalisableString> GetCustomTooltip() => new MarkdownTooltip();

        public LocalisableString TooltipContent => generateTooltipText();

#endregion
    }
}
