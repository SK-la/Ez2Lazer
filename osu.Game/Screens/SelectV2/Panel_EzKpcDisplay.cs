// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Buffers;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2
{
    public partial class EzKpcDisplay : CompositeDrawable
    {
        public Bindable<KpcDisplayMode> KpcDisplayModeBindable { get; } = new Bindable<KpcDisplayMode>(KpcDisplayMode.BarChart);

        private readonly List<NumberColumnEntry> numberEntries = new List<NumberColumnEntry>();
        private readonly List<BarChartColumnEntry> barEntries = new List<BarChartColumnEntry>();
        private readonly Box backgroundBox;
        private FillFlowContainer? columnNotesContainer;
        private OsuSpriteText? headerText;

        private int currentColumnCount;
        private bool modeChanged;

        public EzKpcDisplay()
        {
            // Match SpreadDisplay sizing: auto-size horizontally, fill vertically.
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
                    backgroundBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.6f),
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(new GridContainer
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
                Content = new[]
                {
                    new[]
                    {
                        headerText = new OsuSpriteText
                        {
                            Text = "[Notes]",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Colour4.GhostWhite,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft
                        },
                        Empty(),
                        columnNotesContainer = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            AutoSizeAxes = Axes.Both,
                            Spacing = new Vector2(5), // align spacing with SpreadDisplay
                            Padding = new MarginPadding { Horizontal = 0 },
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    }
                }
            });

            KpcDisplayModeBindable.BindValueChanged(mode =>
            {
                modeChanged = true;

                switch (mode.NewValue)
                {
                    case KpcDisplayMode.Numbers:
                    {
                        headerText.Show();
                        backgroundBox.Colour = Colour4.Black.Opacity(0.6f);
                    }
                        break;

                    case KpcDisplayMode.BarChart:
                    {
                        backgroundBox.Colour = Colour4.White.Opacity(0.6f);
                        headerText?.Hide();
                    }
                        break;
                }
            }, true);
        }

        /// <summary>
        /// 更新列音符数量显示
        /// </summary>
        /// <param name="columnNoteCounts">每列的音符数量</param>
        /// <param name="holdNoteCounts">面条数量</param>
        /// <param name="keyCount"></param>
        public void UpdateColumnCounts(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null, int? keyCount = null)
        {
            if (keyCount.HasValue)
            {
                int kc = keyCount.Value;
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

                    updateDisplay(normalized, normalizedHold, kc);
                }
                finally
                {
                    pool.Return(normalized, clearArray: true);
                    pool.Return(normalizedHold, clearArray: true);
                }

                return;
            }
            else
            {
                // When keyCount is not provided, normalize sparse key dictionaries into
                // a continuous 0..maxKey range so callers can index by column.
                if (columnNoteCounts.Count > 0)
                {
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

                        updateDisplay(normalized, normalizedHold, kc);
                    }
                    finally
                    {
                        pool.Return(normalized, clearArray: true);
                        pool.Return(normalizedHold, clearArray: true);
                    }

                    return;
                }
            }

            updateDisplay(columnNoteCounts, holdNoteCounts);
        }

        private void updateDisplay(int[] columnNoteCounts, int[]? holdNoteCounts, int columns)
        {
            switch (KpcDisplayModeBindable.Value)
            {
                case KpcDisplayMode.Numbers:
                    updateNumbersDisplay(columnNoteCounts, holdNoteCounts, columns);
                    break;

                case KpcDisplayMode.BarChart:
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
                case KpcDisplayMode.Numbers:
                    updateNumbersDisplay(columnNoteCounts, holdNoteCounts);
                    break;

                case KpcDisplayMode.BarChart:
                    updateBarChartDisplay(columnNoteCounts, holdNoteCounts);
                    break;
            }
        }

        private void rebuildForModeIfNeeded(int columns)
        {
            // If mode changed, recreate entries for the new mode (mode changes are rare).
            if (modeChanged)
            {
                currentColumnCount = 0;
                columnNotesContainer?.Clear();
                numberEntries.Clear();
                barEntries.Clear();
                modeChanged = false;
            }

            if (currentColumnCount == columns)
                return;

            // If increasing column count, add new entries. If decreasing, hide extras.
            if (columns > currentColumnCount)
            {
                switch (KpcDisplayModeBindable.Value)
                {
                    case KpcDisplayMode.Numbers:
                        numberEntries.EnsureCapacity(columns);

                        for (int i = currentColumnCount; i < columns; i++)
                        {
                            var entry = new NumberColumnEntry(i);
                            numberEntries.Add(entry);
                            columnNotesContainer?.Add(entry.Container);
                        }

                        break;

                    case KpcDisplayMode.BarChart:
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
                    case KpcDisplayMode.Numbers:
                        for (int i = columns; i < numberEntries.Count; i++)
                            numberEntries[i].Container.Hide();

                        break;

                    case KpcDisplayMode.BarChart:
                        for (int i = columns; i < barEntries.Count; i++)
                            barEntries[i].Container.Hide();

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

        private class NumberColumnEntry
        {
            public readonly FillFlowContainer Container;
            private readonly OsuSpriteText? valueText;
            private readonly OsuSpriteText holdText;

            private int lastTotal = int.MinValue;
            private int lastHold = int.MinValue;

            public NumberColumnEntry(int index)
            {
                Container = new FillFlowContainer
                {
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = $"{index + 1}/",
                            Font = OsuFont.GetFont(size: 12),
                            Colour = Color4.Gray,
                        },
                        valueText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.LightCoral,
                        },
                        holdText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 12),
                            Colour = Color4.LightGoldenrodYellow.Darken(0.2f),
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                        }
                    }
                };
            }

            public void SetValues(int total, int hold)
            {
                if (lastTotal != total)
                {
                    lastTotal = total;
                    if (valueText != null) valueText.Text = total.ToString(CultureInfo.InvariantCulture);
                }

                if (lastHold != hold)
                {
                    lastHold = hold;
                    holdText.Text = hold > 0 ? $"/{hold.ToString(CultureInfo.InvariantCulture)} " : " ";
                }
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
            private readonly OsuSpriteText? valueText;

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

                if (showText)
                {
                    var idxText = new OsuSpriteText
                    {
                        Text = (index + 1).ToString(),
                        Font = OsuFont.GetFont(size: 12),
                        Colour = Color4.Gray,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Margin = new MarginPadding { Bottom = 2 }
                    };

                    valueText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 10),
                        Colour = Color4.White,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                    };

                    children.Add(idxText);
                    children.Add(valueText);
                }

                Container.Children = children.ToArray();
            }

            public void SetValues(int totalNotes, int holdNotes, int maxCount)
            {
                // Avoid redundant work (and string allocations) when values are unchanged.
                if (lastTotalNotes == totalNotes && lastHoldNotes == holdNotes && lastMaxCount == maxCount)
                    return;

                lastTotalNotes = totalNotes;
                lastHoldNotes = holdNotes;
                lastMaxCount = maxCount;

                int regularNotes = totalNotes - holdNotes;

                float totalHeight = maxCount > 0 ? (float)totalNotes / maxCount * max_bar_height : 0;
                float regularHeight = maxCount > 0 ? (float)regularNotes / maxCount * max_bar_height : 0;

                const float transition_duration = 200f;
                regularBox.ResizeHeightTo(regularHeight, transition_duration, Easing.OutQuint);

                float holdHeight = Math.Max(0, totalHeight - regularHeight);
                holdBox.ResizeHeightTo(holdHeight, transition_duration, Easing.OutQuint);
                // place holdBox above regular by animating its bottom margin inside the masked bar area
                holdBox.TransformTo(nameof(Drawable.Margin), new MarginPadding { Bottom = regularHeight }, transition_duration, Easing.OutQuint);

                if (valueText != null)
                {
                    valueText.Text = holdNotes > 0
                        ? $"{totalNotes.ToString(CultureInfo.InvariantCulture)}({holdNotes.ToString(CultureInfo.InvariantCulture)})"
                        : totalNotes.ToString(CultureInfo.InvariantCulture);
                    valueText.MoveToY(-(totalHeight + 6), transition_duration, Easing.OutQuint);
                }
            }
        }
    }

    /// <summary>
    /// 显示模式枚举
    /// </summary>
    public enum KpcDisplayMode
    {
        /// <summary>
        /// 数字（默认，最高性能）
        /// </summary>
        Numbers,

        /// <summary>
        /// 柱状图
        /// </summary>
        BarChart
    }
}
