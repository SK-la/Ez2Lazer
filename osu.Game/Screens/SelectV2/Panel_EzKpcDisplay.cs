// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public Bindable<KpcDisplayMode> KpcDisplayMode { get; } = new Bindable<KpcDisplayMode>(SelectV2.KpcDisplayMode.BarChart);

        /// <summary>
        /// Maximum bar height in pixels. Can be set externally.
        /// Default aligned to SpreadDisplay max height.
        /// </summary>
        public float MaxBarHeight { get; set; } = 6f;

        private readonly FillFlowContainer columnNotesContainer;
        private readonly OsuSpriteText? headerText;
        private readonly Box backgroundBox;

        private int currentColumnCount;
        private bool modeChanged;
        private readonly List<NumberColumnEntry> numberEntries = new List<NumberColumnEntry>();
        private readonly List<BarChartColumnEntry> barEntries = new List<BarChartColumnEntry>();

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        public EzKpcDisplay()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                Masking = true,
                CornerRadius = 5,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    backgroundBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.6f),
                    },
                    new GridContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding { Horizontal = 8f },
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
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            FinishTransforms(true);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Initialize bindable from config and hook up visibility/builder logic here (same lifecycle as SpreadDisplay).
            KpcDisplayMode.Value = ezConfig.GetBindable<KpcDisplayMode>(Ez2Setting.KpcDisplayMode).Value;

            // Ensure we rebuild entries when the display mode changes and toggle header visibility.
            KpcDisplayMode.BindValueChanged(mode =>
            {
                modeChanged = true;

                if (headerText != null)
                {
                    if (mode.NewValue == SelectV2.KpcDisplayMode.Numbers)
                        headerText.Show();
                    else
                        headerText.Hide();
                }
                // Adjust background to match SpreadDisplay-like style in BarChart mode.
                if (backgroundBox != null)
                {
                    if (mode.NewValue == SelectV2.KpcDisplayMode.BarChart)
                    {
                        backgroundBox.Colour = Colour4.White;
                        backgroundBox.Alpha = 0.06f;
                    }
                    else
                    {
                        backgroundBox.Colour = Colour4.Black;
                        backgroundBox.Alpha = 0.6f;
                    }
                }
            }, false);

            // Initial header visibility based on current mode.
            if (headerText != null)
            {
                if (KpcDisplayMode.Value == SelectV2.KpcDisplayMode.Numbers)
                    headerText.Show();
                else
                    headerText.Hide();
            }

            // Apply initial background style based on current mode.
            if (backgroundBox != null)
            {
                if (KpcDisplayMode.Value == SelectV2.KpcDisplayMode.BarChart)
                {
                    backgroundBox.Colour = Colour4.White;
                    backgroundBox.Alpha = 0.06f;
                }
                else
                {
                    backgroundBox.Colour = Colour4.Black;
                    backgroundBox.Alpha = 0.6f;
                }
            }
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
                var normalized = new Dictionary<int, int>(Math.Max(0, kc));
                var normalizedHold = new Dictionary<int, int>(Math.Max(0, kc));

                for (int i = 0; i < kc; i++)
                {
                    normalized[i] = columnNoteCounts.GetValueOrDefault(i);
                    normalizedHold[i] = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                }

                columnNoteCounts = normalized;
                holdNoteCounts = normalizedHold;
            }
            else
            {
                // When keyCount is not provided, normalize sparse key dictionaries into
                // a continuous 0..maxKey range so callers can index by column.
                if (columnNoteCounts.Count > 0)
                {
                    int maxKey = 0;
                    foreach (var k in columnNoteCounts.Keys)
                        if (k > maxKey) maxKey = k;

                    int kc = maxKey + 1;
                    var normalized = new Dictionary<int, int>(Math.Max(0, kc));
                    var normalizedHold = new Dictionary<int, int>(Math.Max(0, kc));

                    for (int i = 0; i < kc; i++)
                    {
                        normalized[i] = columnNoteCounts.GetValueOrDefault(i);
                        normalizedHold[i] = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                    }

                    columnNoteCounts = normalized;
                    holdNoteCounts = normalizedHold;
                }
            }

            updateDisplay(columnNoteCounts, holdNoteCounts);
        }

        private void updateDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            switch (KpcDisplayMode.Value)
            {
                case SelectV2.KpcDisplayMode.Numbers:
                    updateNumbersDisplay(columnNoteCounts, holdNoteCounts);
                    break;

                case SelectV2.KpcDisplayMode.BarChart:
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
                columnNotesContainer.Clear();
                numberEntries.Clear();
                barEntries.Clear();
                modeChanged = false;
            }

            if (currentColumnCount == columns)
                return;

            // If increasing column count, add new entries. If decreasing, hide extras.
            if (columns > currentColumnCount)
            {
                switch (KpcDisplayMode.Value)
                {
                    case SelectV2.KpcDisplayMode.Numbers:
                        for (int i = currentColumnCount; i < columns; i++)
                        {
                            var entry = new NumberColumnEntry(i);
                            numberEntries.Add(entry);
                            columnNotesContainer.Add(entry.Container);
                        }

                        break;

                    case SelectV2.KpcDisplayMode.BarChart:
                        for (int i = currentColumnCount; i < columns; i++)
                        {
                            var entry = new BarChartColumnEntry(i, MaxBarHeight, false);
                            barEntries.Add(entry);
                            columnNotesContainer.Add(entry.Container);
                        }

                        break;
                }
            }
            else
            {
                switch (KpcDisplayMode.Value)
                {
                    case SelectV2.KpcDisplayMode.Numbers:
                        for (int i = columns; i < numberEntries.Count; i++)
                            numberEntries[i].Container.Hide();

                        break;

                    case SelectV2.KpcDisplayMode.BarChart:
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
                    numberEntries[i].Container.Show();
                    numberEntries[i].SetValues(total, hold);
                }

                return;
            }

            int idx = 0;

            foreach (var kvp in columnNoteCounts.OrderBy(k => k.Key))
            {
                if (idx >= visible)
                    break;

                int hold = holdNoteCounts?.GetValueOrDefault(kvp.Key) ?? 0;
                numberEntries[idx].Container.Show();
                numberEntries[idx].SetValues(kvp.Value, hold);
                idx++;
            }

            // Zero-fill remaining visible slots.
            for (int i = idx; i < visible; i++)
            {
                numberEntries[i].Container.Show();
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
                    barEntries[i].Container.Show();

                for (int i = 0; i < visible; i++)
                    barEntries[i].SetValues(0, 0, 1);

                return;
            }

            for (int i = 0; i < visible; i++)
            {
                int total = columnNoteCounts.GetValueOrDefault(i);
                int hold = holdNoteCounts?.GetValueOrDefault(i) ?? 0;
                barEntries[i].Container.Show();
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
            // Align width/spacing with SpreadDisplay maximums for consistent visuals and layout.
            private const float bar_width = 7f;
            private const float bar_spacing = 5f;
            private readonly float maxBarHeight;
            private static readonly Color4 hold_note_color = Color4Extensions.FromHex("#FFD39B");

            public readonly Container Container;
            private readonly Box regularBox;
            private readonly Box holdBox;
            private readonly OsuSpriteText? valueText;

            private int lastTotalNotes = int.MinValue;
            private int lastHoldNotes = int.MinValue;
            private int lastMaxCount = int.MinValue;

            public BarChartColumnEntry(int index, float maxBarHeight, bool showText = false)
            {
                this.maxBarHeight = maxBarHeight;

                // Match SpreadDisplay's largest dot: width 7, height 12 (maxBarHeight).
                Container = new Container
                {
                    Size = new Vector2(bar_width, this.maxBarHeight),
                    Margin = new MarginPadding { Right = bar_spacing },
                };

                // Create a masked bar area with corner radius to achieve capsule appearance.
                var barArea = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Size = new Vector2(bar_width, this.maxBarHeight),
                    Margin = new MarginPadding { Bottom = 0 },
                    Masking = true,
                    CornerRadius = bar_width / 2f, // pill shape: radius = half width (matches reference)
                };

                regularBox = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    Colour = Color4Extensions.FromHex("#4DA6FF"), // regular notes: blue
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

                barArea.AddRange(new Drawable[] { regularBox, holdBox });

                // Build children list and only create text drawables when requested to avoid allocations/layout.
                var children = new List<Drawable>
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = this.maxBarHeight,
                        Colour = Color4.Gray.Opacity(0.2f),
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Margin = new MarginPadding { Bottom = 0 }
                    },
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

                float totalHeight = maxCount > 0 ? (float)totalNotes / maxCount * maxBarHeight : 0;
                float regularHeight = maxCount > 0 ? (float)regularNotes / maxCount * maxBarHeight : 0;

                // Smoothly animate height/margin/text changes to avoid visual jitter when data updates.
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
