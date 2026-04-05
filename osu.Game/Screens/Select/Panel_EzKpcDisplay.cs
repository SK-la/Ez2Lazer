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
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class EzKpcDisplay : CompositeDrawable, IHasCurrentValue<EzManiaSummary>
    {
        public Bindable<EzEnumChartDisplay> KpcDisplayModeBindable { get; } = new Bindable<EzEnumChartDisplay>(EzEnumChartDisplay.BarChart);

        private readonly BindableWithCurrent<EzManiaSummary> current = new BindableWithCurrent<EzManiaSummary>();

        public Bindable<EzManiaSummary> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        public EzManiaSummary ManiaSummary
        {
            get => Current.Value;
            set => Current.Value = value;
        }

        private readonly List<NumberColumnEntry> numberEntries = new List<NumberColumnEntry>();
        private readonly List<BarChartColumnEntry> barEntries = new List<BarChartColumnEntry>();
        private FillFlowContainer? columnNotesContainer;
        private OsuSpriteText? headerText;
        private Container? modePlaceholder;

        private int currentColumnCount;

        // last received data so mode switches can immediately refresh
        private int[]? lastKnownColumns;
        private int[]? lastKnownHolds;
        private int lastKnownCount;

        public EzKpcDisplay()
        {
            Current.Value = EzManiaSummary.EMPTY;

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
                    modePlaceholder = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            KpcDisplayModeBindable.BindValueChanged(e =>
            {
                buildForMode(e.NewValue);
                refresh();
            }, true);

            Current.BindValueChanged(_ => refresh());
        }

        private void buildForMode(EzEnumChartDisplay mode)
        {
            modePlaceholder?.Clear();
            columnNotesContainer = null;
            headerText = null;
            numberEntries.Clear();
            barEntries.Clear();
            currentColumnCount = 0;

            switch (mode)
            {
                case EzEnumChartDisplay.Numbers:
                {
                    columnNotesContainer = new FillFlowContainer
                    {
                        Direction = FillDirection.Horizontal,
                        AutoSizeAxes = Axes.Both,
                        Spacing = new Vector2(5),
                        Padding = new MarginPadding { Horizontal = 0 },
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    };

                    headerText = new OsuSpriteText
                    {
                        Text = "[Notes]",
                        Font = OsuFont.GetFont(size: 14),
                        Colour = Colour4.GhostWhite,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    };

                    var grid = new GridContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding { Horizontal = 5f },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 3f),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[] { new[] { headerText, Empty(), columnNotesContainer } }
                    };

                    modePlaceholder?.Add(grid);

                    if (lastKnownColumns != null)
                        updateDisplay(lastKnownColumns, lastKnownHolds, lastKnownCount);
                }

                    break;

                case EzEnumChartDisplay.BarChart:
                {
                    columnNotesContainer = new FillFlowContainer
                    {
                        Direction = FillDirection.Horizontal,
                        AutoSizeAxes = Axes.Both,
                        Spacing = new Vector2(5),
                        Padding = new MarginPadding { Horizontal = 0 },
                        Margin = new MarginPadding { Horizontal = 5f },
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    };

                    modePlaceholder?.Add(columnNotesContainer);

                    if (lastKnownColumns != null)
                        updateDisplay(lastKnownColumns, lastKnownHolds, lastKnownCount);
                }

                    break;
            }
        }

        private void refresh()
        {
            if (!ManiaSummary.HasData)
            {
                Hide();
                return;
            }

            ensureModeBuilt();
            updateColumnCounts(ManiaSummary.ColumnCounts, ManiaSummary.HoldNoteCounts);
            Show();
        }

        private void ensureModeBuilt()
        {
            if (modePlaceholder == null || columnNotesContainer != null)
                return;

            buildForMode(KpcDisplayModeBindable.Value);
        }

        /// <summary>
        /// 更新列音符数量显示
        /// </summary>
        /// <param name="columnNoteCounts">每列的音符数量</param>
        /// <param name="holdNoteCounts">面条数量</param>
        private void updateColumnCounts(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            if (columnNoteCounts.Count == 0)
            {
                updateDisplay(columnNoteCounts, holdNoteCounts);
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

                // 复用已有数组，避免重复分配
                if (lastKnownColumns == null || lastKnownColumns.Length < kc)
                    lastKnownColumns = new int[kc];

                if (lastKnownHolds == null || lastKnownHolds.Length < kc)
                    lastKnownHolds = new int[kc];

                Array.Copy(normalized, lastKnownColumns, kc);
                Array.Copy(normalizedHold, lastKnownHolds, kc);
                lastKnownCount = kc;

                updateDisplay(normalized, normalizedHold, kc);
            }
            finally
            {
                pool.Return(normalized, clearArray: true);
                pool.Return(normalizedHold, clearArray: true);
            }
        }

        private void releaseState()
        {
            modePlaceholder = null;
            columnNotesContainer = null;
            headerText = null;

            numberEntries.Clear();
            barEntries.Clear();
            currentColumnCount = 0;

            lastKnownColumns = null;
            lastKnownHolds = null;
            lastKnownCount = 0;
        }

        private void updateDisplay(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            switch (KpcDisplayModeBindable.Value)
            {
                case EzEnumChartDisplay.Numbers:
                    updateNumbersDisplay(columnNoteCounts, holdNoteCounts, columns);
                    break;

                case EzEnumChartDisplay.BarChart:
                    updateBarChartDisplay(columnNoteCounts, holdNoteCounts, columns);
                    break;
            }
        }

        private void updateNumbersDisplay(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            rebuildForModeIfNeeded(columns);

            int visible = currentColumnCount;

            for (int i = 0; i < visible; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;
                var c = numberEntries[i].Container;
                if (c.Alpha < 0.99f) c.Show();
                numberEntries[i].SetValues(total, hold);
            }
        }

        private void updateBarChartDisplay(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            rebuildForModeIfNeeded(columns);

            int visible = currentColumnCount;

            int maxCount = 0;

            for (int i = 0; i < visible; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;
                int sum = total + hold;
                if (sum > maxCount)
                    maxCount = sum;
            }

            if (maxCount == 0)
            {
                for (int i = 0; i < visible; i++)
                {
                    var c = barEntries[i].Container;
                    if (c.Alpha < 0.99f) c.Show();
                }

                for (int i = 0; i < visible; i++)
                    barEntries[i].SetValues(0, 0, 1);

                return;
            }

            for (int i = 0; i < visible; i++)
            {
                int total = i < columns ? columnNoteCounts[i] : 0;
                int hold = (holdNoteCounts != null && i < columns) ? holdNoteCounts[i] : 0;
                var c = barEntries[i].Container;
                if (c.Alpha < 0.99f) c.Show();
                barEntries[i].SetValues(total, hold, maxCount);
            }
        }

        private void updateDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            switch (KpcDisplayModeBindable.Value)
            {
                case EzEnumChartDisplay.Numbers:
                    updateNumbersDisplay(columnNoteCounts, holdNoteCounts);
                    break;

                case EzEnumChartDisplay.BarChart:
                    updateBarChartDisplay(columnNoteCounts, holdNoteCounts);
                    break;
            }
        }

        private void rebuildForModeIfNeeded(int columns)
        {
            if (currentColumnCount == columns)
                return;

            // If increasing column count, add new entries. If decreasing, hide extras.
            if (columns > currentColumnCount)
            {
                switch (KpcDisplayModeBindable.Value)
                {
                    case EzEnumChartDisplay.Numbers:
                        numberEntries.EnsureCapacity(columns);

                        for (int i = currentColumnCount; i < columns; i++)
                        {
                            var entry = new NumberColumnEntry(i);
                            numberEntries.Add(entry);
                            columnNotesContainer?.Add(entry.Container);
                        }

                        break;

                    case EzEnumChartDisplay.BarChart:
                        barEntries.EnsureCapacity(columns);

                        for (int i = currentColumnCount; i < columns; i++)
                        {
                            var entry = new BarChartColumnEntry(i);
                            barEntries.Add(entry);
                            columnNotesContainer?.Add(entry.Container);
                        }

                        break;
                }
            }
            else
            {
                switch (KpcDisplayModeBindable.Value)
                {
                    case EzEnumChartDisplay.Numbers:
                        if (numberEntries.Count > columns)
                        {
                            for (int i = numberEntries.Count - 1; i >= columns; i--)
                            {
                                var toRemove = numberEntries[i];
                                columnNotesContainer?.Remove(toRemove.Container, true);
                                numberEntries.RemoveAt(i);
                            }
                        }

                        break;

                    case EzEnumChartDisplay.BarChart:
                        if (barEntries.Count > columns)
                        {
                            for (int i = barEntries.Count - 1; i >= columns; i--)
                            {
                                var toRemove = barEntries[i];
                                columnNotesContainer?.Remove(toRemove.Container, true);
                                barEntries.RemoveAt(i);
                            }
                        }

                        break;
                }
            }

            currentColumnCount = columns;
        }

        private void updateNumbersDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            rebuildForModeIfNeeded(columnNoteCounts.Count);

            int visible = currentColumnCount;

            // 必须使用方法补0
            if (columnNoteCounts.Count >= visible && columnNoteCounts.ContainsKey(0))
            {
                for (int i = 0; i < visible; i++)
                {
                    int total = columnNoteCounts.GetValueOrDefault(i);
                    int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                    var c = numberEntries[i].Container;
                    if (c.Alpha < 0.99f) c.Show();
                    numberEntries[i].SetValues(total, hold);
                }

                return;
            }

            int idx = 0;

            // Avoid LINQ allocations by sorting keys into a small array (columns are small).
            int[] keys = new int[columnNoteCounts.Count];
            int kptr = 0;
            foreach (int k in columnNoteCounts.Keys)
                keys[kptr++] = k;
            Array.Sort(keys);

            foreach (int key in keys)
            {
                if (idx >= visible)
                    break;

                int hold = holdNoteCounts?.GetValueOrDefault(key) ?? 0;
                var c = numberEntries[idx].Container;
                if (c.Alpha < 0.99f) c.Show();
                numberEntries[idx].SetValues(columnNoteCounts.GetValueOrDefault(key), hold);
                idx++;
            }

            // Zero-fill remaining visible slots.
            for (int i = idx; i < visible; i++)
            {
                var c = numberEntries[i].Container;
                if (c.Alpha < 0.99f) c.Show();
                numberEntries[i].SetValues(0, 0);
            }
        }

        private void updateBarChartDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            rebuildForModeIfNeeded(columnNoteCounts.Count);

            int visible = currentColumnCount;

            int maxCount = 0;

            for (int i = 0; i < visible; i++)
            {
                int total = columnNoteCounts.GetValueOrDefault(i);
                int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                int sum = total + hold;
                if (sum > maxCount)
                    maxCount = sum;
            }

            if (maxCount == 0)
            {
                // Nothing to show: zero-fill visible slots.
                for (int i = 0; i < visible; i++)
                {
                    var c = barEntries[i].Container;
                    if (c.Alpha < 0.99f) c.Show();
                }

                for (int i = 0; i < visible; i++)
                    barEntries[i].SetValues(0, 0, 1);

                return;
            }

            for (int i = 0; i < visible; i++)
            {
                int total = columnNoteCounts.GetValueOrDefault(i);
                int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                var c = barEntries[i].Container;
                if (c.Alpha < 0.99f) c.Show();
                barEntries[i].SetValues(total, hold, maxCount);
            }
        }

        #region ColumnEntry

        private class NumberColumnEntry
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
        }

        private class BarChartColumnEntry
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
        }

        #endregion

        // protected override void Dispose(bool isDisposing)
        // {
        //     if (isDisposing)
        //     {
        //         KpcDisplayModeBindable.UnbindAll();
        //         Current.UnbindAll();
        //         releaseState();
        //     }
        //
        //     base.Dispose(isDisposing);
        // }
    }
}
