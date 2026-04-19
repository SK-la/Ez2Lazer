// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzDisplayKpc : CompositeDrawable
    {
        public Bindable<EzEnumChartDisplay> KpcDisplayModeBindable { get; } = new Bindable<EzEnumChartDisplay>(EzEnumChartDisplay.BarChart);

        private EzManiaAnalysisAttributes? maniaAttributes;

        public EzManiaAnalysisAttributes? ManiaAttributes
        {
            get => maniaAttributes;
            set
            {
                if (value == null)
                {
                    maniaAttributes = null;
                    clear();
                    return;
                }

                bool dataChanged = maniaAttributes != value;
                maniaAttributes = value;

                // 只在数据真正变化时才更新显示
                if (dataChanged)
                    onDataChanged();
            }
        }

        private readonly FillFlowContainer columnNotesContainer;
        private readonly OsuSpriteText notesText;

        private List<NumberColumnEntry>? numberEntries;
        private List<BarChartColumnEntry>? barEntries;

        private EzEnumChartDisplay mode;

        // 用于缓存列数，以避免每次更新都重建 UI
        private int currentColumnCount;

        // 缓存上次已知的列音符数量和面条数量，以便在模式切换时快速刷新显示而无需重新计算（如果数据未变）
        private int[]? lastKnownColumns;
        private int[]? lastKnownHolds;
        private int lastKnownCount;

        public EzDisplayKpc()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            InternalChild = new Container
            {
                Masking = true,
                CornerRadius = 5,
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.6f),
                    },
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Children = new[]
                        {
                            notesText = new OsuSpriteText
                            {
                                Text = "[Notes]",
                                Font = OsuFont.GetFont(size: 14),
                                Colour = Colour4.GhostWhite,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Alpha = 0f
                            },
                            Empty(),
                            columnNotesContainer = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(5f, 0),
                                Padding = new MarginPadding { Horizontal = 0 },
                            },
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            mode = KpcDisplayModeBindable.Value;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            KpcDisplayModeBindable.BindValueChanged(onDisplayModeChanged, true);
        }

        /// <summary>
        /// 处理显示模式切换
        /// </summary>
        private void onDisplayModeChanged(ValueChangedEvent<EzEnumChartDisplay> v)
        {
            mode = v.NewValue;

            // 更新 UI 可见性
            updateModeVisibility();

            // 如果有数据，用新模弍重新渲染
            if (maniaAttributes != null && lastKnownColumns != null)
                rebuildAndRender(lastKnownColumns, lastKnownHolds, lastKnownCount);
        }

        /// <summary>
        /// 数据变化时调用（ManiaAttributes setter）
        /// </summary>
        private void onDataChanged()
        {
            if (maniaAttributes == null)
                return;

            // 解析并缓存数据
            parseAndCacheData(maniaAttributes.ColumnCounts, maniaAttributes.HoldNoteCounts);

            // 渲染显示
            if (lastKnownColumns != null)
                rebuildAndRender(lastKnownColumns, lastKnownHolds, lastKnownCount);
        }

        /// <summary>
        /// 更新模式相关的 UI 可见性
        /// </summary>
        private void updateModeVisibility()
        {
            switch (mode)
            {
                case EzEnumChartDisplay.Numbers:
                    notesText.Alpha = 1f;
                    break;

                case EzEnumChartDisplay.BarChart:
                    notesText.Alpha = 0f;
                    columnNotesContainer.Margin = new MarginPadding { Horizontal = 5f };
                    break;
            }
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

            if (numberEntries != null)
            {
                foreach (var entry in numberEntries)
                    entry.Dispose();
                numberEntries = null;
            }

            if (barEntries != null)
            {
                foreach (var entry in barEntries)
                    entry.Dispose();
                barEntries = null;
            }

            // 根据目标列数创建新的列表
            switch (mode)
            {
                case EzEnumChartDisplay.Numbers:
                    numberEntries = new List<NumberColumnEntry>(targetColumns);

                    for (int i = 0; i < targetColumns; i++)
                    {
                        var entry = new NumberColumnEntry(i);
                        numberEntries.Add(entry);
                        columnNotesContainer.Add(entry.Container);
                    }

                    break;

                case EzEnumChartDisplay.BarChart:
                    barEntries = new List<BarChartColumnEntry>(targetColumns);

                    for (int i = 0; i < targetColumns; i++)
                    {
                        var entry = new BarChartColumnEntry(i);
                        barEntries.Add(entry);
                        columnNotesContainer.Add(entry.Container);
                    }

                    break;
            }

            currentColumnCount = targetColumns;
        }

        /// <summary>
        /// 渲染数值到现有的 UI 元素
        /// </summary>
        private void renderValues(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            switch (mode)
            {
                case EzEnumChartDisplay.Numbers:
                    renderNumbersValues(columnNoteCounts, holdNoteCounts, columns);
                    break;

                case EzEnumChartDisplay.BarChart:
                    renderBarChartValues(columnNoteCounts, holdNoteCounts, columns);
                    break;
            }
        }

        /// <summary>
        /// 渲染 Numbers 模式的数值
        /// </summary>
        private void renderNumbersValues(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            if (numberEntries == null) return;

            for (int i = 0; i < currentColumnCount; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;
                var c = numberEntries[i].Container;
                if (c.Alpha < 0.99f) c.Show();
                numberEntries[i].SetValues(total, hold);
            }
        }

        /// <summary>
        /// 渲染 BarChart 模式的数值
        /// </summary>
        private void renderBarChartValues(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            if (barEntries == null) return;

            // 计算最大值用于归一化
            int maxCount = 0;

            for (int i = 0; i < currentColumnCount; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;
                int sum = total + hold;
                if (sum > maxCount)
                    maxCount = sum;
            }

            if (maxCount == 0)
            {
                for (int i = 0; i < currentColumnCount; i++)
                {
                    var c = barEntries[i].Container;
                    if (c.Alpha < 0.99f) c.Show();
                    barEntries[i].SetValues(0, 0, 1);
                }

                return;
            }

            for (int i = 0; i < currentColumnCount; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;
                var c = barEntries[i].Container;
                if (c.Alpha < 0.99f) c.Show();
                barEntries[i].SetValues(total, hold, maxCount);
            }
        }

        #region ColumnEntry

        private class NumberColumnEntry : IDisposable
        {
            public readonly Container Container;

            private readonly OsuSpriteText indexText;
            private readonly OsuSpriteText valueText;
            private readonly OsuSpriteText holdText;

            private int lastTotal = int.MinValue;
            private int lastHold = int.MinValue;

            public NumberColumnEntry(int index)
            {
                Container = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };

                indexText = new OsuSpriteText
                {
                    Text = $"{index + 1}/",
                    Font = OsuFont.GetFont(size: 12),
                    Colour = Color4.Gray,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };

                valueText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 14),
                    Colour = Color4.LightCoral,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };

                holdText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 12),
                    Colour = Color4.LightGoldenrodYellow.Darken(0.2f),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                };

                Container.AddRange(new Drawable[] { indexText, valueText, holdText });
            }

            public void SetValues(int total, int hold)
            {
                if (lastTotal != total)
                {
                    lastTotal = total;
                    valueText.Text = total.ToString(CultureInfo.InvariantCulture);
                }

                if (lastHold != hold)
                {
                    lastHold = hold;
                    holdText.Text = hold > 0 ? $"/{hold.ToString(CultureInfo.InvariantCulture)} " : " ";
                }

                // Manual lightweight layout: position texts horizontally based on measured widths.
                // This avoids FillFlowContainer's per-frame layout overhead.
                float x = 0f;
                indexText.X = x;
                x += indexText.DrawWidth;

                valueText.X = x;
                x += valueText.DrawWidth;

                holdText.X = x + 2f; // small spacing
            }

            public void Dispose()
            {
                // Container 会从父容器移除时自动清理，这里不需要额外操作
                // 但如果需要手动清理资源，可以在这里添加
            }
        }

        private class BarChartColumnEntry : IDisposable
        {
            private const float bar_width = 7f;
            private const float max_bar_height = 12f;
            private static readonly Color4 hold_note_color = Color4Extensions.FromHex("#FFD39B");
            private static readonly Color4 note_color = Color4Extensions.FromHex("#4DA6FF");

            public readonly Container Container;
            private readonly Box regularBox;
            private readonly Box holdBox;

            // private readonly OsuSpriteText? valueText;

            private int lastTotalNotes = int.MinValue;
            private int lastHoldNotes = int.MinValue;
            private int lastMaxCount = int.MinValue;

            public BarChartColumnEntry(int index, bool showText = false)
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

                // if (showText)
                // {
                //     var idxText = new OsuSpriteText
                //     {
                //         Text = (index + 1).ToString(),
                //         Font = OsuFont.GetFont(size: 12),
                //         Colour = Color4.Gray,
                //         Anchor = Anchor.BottomCentre,
                //         Origin = Anchor.BottomCentre,
                //         Margin = new MarginPadding { Bottom = 2 }
                //     };
                //
                //     valueText = new OsuSpriteText
                //     {
                //         Font = OsuFont.GetFont(size: 10),
                //         Colour = Color4.White,
                //         Anchor = Anchor.BottomCentre,
                //         Origin = Anchor.BottomCentre,
                //     };
                //
                //     children.Add(idxText);
                //     children.Add(valueText);
                // }

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

                // if (valueText != null)
                // {
                //     valueText.Text = holdNotes > 0
                //         ? $"{totalNotes.ToString(CultureInfo.InvariantCulture)}({holdNotes.ToString(CultureInfo.InvariantCulture)})"
                //         : totalNotes.ToString(CultureInfo.InvariantCulture);
                //     valueText.Y = -(totalHeight + 6);
                // }
            }

            public void Dispose()
            {
                // Container 会从父容器移除时自动清理
            }
        }

        #endregion

        private void clear()
        {
            // 释放 Entry 对象
            if (numberEntries != null)
            {
                foreach (var entry in numberEntries)
                    entry.Dispose();
                numberEntries = null;
            }

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
                maniaAttributes = null;
                clear();
            }
        }
    }
}
