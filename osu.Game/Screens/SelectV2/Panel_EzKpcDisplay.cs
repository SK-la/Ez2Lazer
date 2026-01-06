// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2
{
    public partial class EzKpcDisplay : CompositeDrawable
    {
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

        private readonly FillFlowContainer columnNotesContainer;
        private KpcDisplayMode currentKpcDisplayMode = KpcDisplayMode.Numbers;
        private Dictionary<int, int>? currentColumnCounts;
        private Dictionary<int, int>? currentHoldNoteCounts;

        private int currentColumnCount;
        private readonly List<NumberColumnEntry> numberEntries = new List<NumberColumnEntry>();
        private readonly List<BarChartColumnEntry> barEntries = new List<BarChartColumnEntry>();

        /// <summary>
        /// 当前显示模式
        /// </summary>
        public KpcDisplayMode CurrentKpcDisplayMode
        {
            get => currentKpcDisplayMode;
            set
            {
                if (currentKpcDisplayMode == value)
                    return;

                currentKpcDisplayMode = value;

                // 如果有数据，立即重新渲染
                if (currentColumnCounts != null)
                {
                    updateDisplay(currentColumnCounts, currentHoldNoteCounts);
                }
            }
        }

        public EzKpcDisplay()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = columnNotesContainer = new FillFlowContainer
            {
                Direction = FillDirection.Horizontal,
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
        }

        /// <summary>
        /// 更新列音符数量显示
        /// </summary>
        /// <param name="columnNoteCounts">每列的音符数量</param>
        public void UpdateColumnCounts(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            currentColumnCounts = columnNoteCounts;
            currentHoldNoteCounts = holdNoteCounts;
            updateDisplay(columnNoteCounts, holdNoteCounts);
        }

        private void updateDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            int columns = columnNoteCounts.Count;
            if (columns == 0)
            {
                currentColumnCount = 0;
                columnNotesContainer.Clear();
                numberEntries.Clear();
                barEntries.Clear();
                return;
            }

            switch (currentKpcDisplayMode)
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
            if (currentColumnCount == columns)
                return;

            currentColumnCount = columns;
            columnNotesContainer.Clear();
            numberEntries.Clear();
            barEntries.Clear();

            switch (currentKpcDisplayMode)
            {
                case KpcDisplayMode.Numbers:
                    for (int i = 0; i < columns; i++)
                    {
                        var entry = new NumberColumnEntry(i);
                        numberEntries.Add(entry);
                        columnNotesContainer.Add(entry.Container);
                    }

                    break;

                case KpcDisplayMode.BarChart:
                    for (int i = 0; i < columns; i++)
                    {
                        var entry = new BarChartColumnEntry(i);
                        barEntries.Add(entry);
                        columnNotesContainer.Add(entry.Container);
                    }

                    break;
            }
        }

        private void updateNumbersDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            rebuildForModeIfNeeded(columnNoteCounts.Count);

            // Expect 0..N-1 keys (PanelBeatmap normalizes). Fall back to ordered keys if needed.
            if (columnNoteCounts.Count == numberEntries.Count && columnNoteCounts.ContainsKey(0))
            {
                for (int i = 0; i < numberEntries.Count; i++)
                {
                    int total = columnNoteCounts.GetValueOrDefault(i);
                    int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                    numberEntries[i].SetValues(total, hold);
                }
            }
            else
            {
                int idx = 0;
                foreach (var kvp in columnNoteCounts.OrderBy(k => k.Key))
                {
                    if (idx >= numberEntries.Count)
                        break;

                    int hold = holdNoteCounts?.GetValueOrDefault(kvp.Key) ?? 0;
                    numberEntries[idx].SetValues(kvp.Value, hold);
                    idx++;
                }
            }
        }

        private void updateBarChartDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            rebuildForModeIfNeeded(columnNoteCounts.Count);

            int maxCount = 0;
            for (int i = 0; i < currentColumnCount; i++)
            {
                int total = columnNoteCounts.GetValueOrDefault(i);
                int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                int sum = total + hold;
                if (sum > maxCount)
                    maxCount = sum;
            }

            if (maxCount == 0)
            {
                // Nothing to show.
                for (int i = 0; i < barEntries.Count; i++)
                    barEntries[i].SetValues(0, 0, 1);
                return;
            }

            for (int i = 0; i < barEntries.Count; i++)
            {
                int total = columnNoteCounts.GetValueOrDefault(i);
                int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                barEntries[i].SetValues(total, hold, maxCount);
            }
        }

        /// <summary>
        /// 清空显示
        /// </summary>
        public void Clear()
        {
            currentColumnCounts = null;
            columnNotesContainer.Clear();
            numberEntries.Clear();
            barEntries.Clear();
            currentColumnCount = 0;
        }

        private class NumberColumnEntry
        {
            public readonly FillFlowContainer Container;
            private readonly OsuSpriteText valueText;
            private readonly OsuSpriteText holdText;

            private int lastTotal = int.MinValue;
            private int lastHold = int.MinValue;

            public NumberColumnEntry(int index)
            {
                Container = new FillFlowContainer
                {
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = $"{index + 1}/",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.Gray,
                        },
                        valueText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 16),
                            Colour = Color4.LightCoral,
                        },
                        holdText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14),
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
                    valueText.Text = total.ToString(CultureInfo.InvariantCulture);
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
            private const float max_bar_height = 30f;
            private const float bar_width = 20f;
            private const float bar_spacing = 2f;
            private static readonly Color4 hold_note_color = Color4Extensions.FromHex("#FFD39B");

            public readonly Container Container;
            private readonly Box regularBox;
            private readonly Box holdBox;
            private readonly OsuSpriteText valueText;

            private int lastTotalNotes = int.MinValue;
            private int lastHoldNotes = int.MinValue;
            private int lastMaxCount = int.MinValue;

            public BarChartColumnEntry(int index)
            {
                Container = new Container
                {
                    Size = new Vector2(bar_width, max_bar_height + 20),
                    Margin = new MarginPadding { Right = bar_spacing },
                };

                regularBox = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Colour = Color4.LightCoral,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Margin = new MarginPadding { Bottom = 15 }
                };

                holdBox = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Colour = hold_note_color,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                };

                valueText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 10),
                    Colour = Color4.White,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                };

                Container.Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = max_bar_height,
                        Colour = Color4.Gray.Opacity(0.2f),
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Margin = new MarginPadding { Bottom = 15 }
                    },
                    regularBox,
                    holdBox,
                    new OsuSpriteText
                    {
                        Text = (index + 1).ToString(),
                        Font = OsuFont.GetFont(size: 12),
                        Colour = Color4.Gray,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Margin = new MarginPadding { Bottom = 2 }
                    },
                    valueText,
                };
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

                regularBox.Height = regularHeight;

                holdBox.Height = totalHeight - regularHeight;
                holdBox.Margin = new MarginPadding { Bottom = 15 + regularHeight };

                valueText.Text = holdNotes > 0
                    ? $"{totalNotes.ToString(CultureInfo.InvariantCulture)}({holdNotes.ToString(CultureInfo.InvariantCulture)})"
                    : totalNotes.ToString(CultureInfo.InvariantCulture);
                valueText.Y = -(totalHeight + 17);
            }
        }
    }
}
