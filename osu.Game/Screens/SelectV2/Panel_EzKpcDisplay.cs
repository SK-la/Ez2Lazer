// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
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
            columnNotesContainer.Clear();

            if (!columnNoteCounts.Any())
                return;

            switch (currentKpcDisplayMode)
            {
                case KpcDisplayMode.Numbers:
                    createNumbersDisplay(columnNoteCounts, holdNoteCounts);
                    break;

                case KpcDisplayMode.BarChart:
                    createBarChartDisplay(columnNoteCounts, holdNoteCounts);
                    break;
            }
        }

        private void createNumbersDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            // 高性能的数字显示模式
            columnNotesContainer.Children = columnNoteCounts
                                            .OrderBy(c => c.Key)
                                            .Select(c =>
                                            {
                                                int holdNotes = holdNoteCounts?.GetValueOrDefault(c.Key) ?? 0;
                                                return new FillFlowContainer
                                                {
                                                    Direction = FillDirection.Horizontal,
                                                    AutoSizeAxes = Axes.Both,
                                                    Children = new Drawable[]
                                                    {
                                                        new OsuSpriteText
                                                        {
                                                            Text = $"{c.Key + 1}/",
                                                            Font = OsuFont.GetFont(size: 14),
                                                            Colour = Color4.Gray,
                                                        },
                                                        new OsuSpriteText
                                                        {
                                                            Text = $"{c.Value}",
                                                            Font = OsuFont.GetFont(size: 16),
                                                            Colour = Color4.LightCoral,
                                                        },
                                                        new OsuSpriteText
                                                        {
                                                            Text = holdNotes > 0 ? $"/{holdNotes} " : " ",
                                                            Font = OsuFont.GetFont(size: 14),
                                                            Colour = Color4.LightGoldenrodYellow.Darken(0.2f),
                                                            Anchor = Anchor.BottomLeft,
                                                            Origin = Anchor.BottomLeft,
                                                        }
                                                    }
                                                };
                                            }).ToArray();
        }

        private void createBarChartDisplay(Dictionary<int, int> columnNoteCounts, Dictionary<int, int>? holdNoteCounts = null)
        {
            var sortedCounts = columnNoteCounts.OrderBy(c => c.Key).ToArray();
            if (!sortedCounts.Any()) return;

            // 计算最大值用于归一化（包括hold notes）
            int maxCount = sortedCounts.Max(c =>
            {
                int holdNotes = holdNoteCounts?.GetValueOrDefault(c.Key) ?? 0;
                return c.Value + holdNotes;
            });
            if (maxCount == 0) return;

            const float max_bar_height = 30f;
            const float bar_width = 20f;
            const float bar_spacing = 2f;

            // 米黄色用于hold notes
            Color4 holdNoteColor = Color4Extensions.FromHex("#FFD39B"); // 米黄色

            columnNotesContainer.Children = sortedCounts
                                            .Select(c =>
                                            {
                                                int totalNotes = c.Value;
                                                int holdNotes = holdNoteCounts?.GetValueOrDefault(c.Key) ?? 0;
                                                int regularNotes = totalNotes - holdNotes; // 普通notes = 总数 - 长按notes

                                                float totalNormalizedHeight = maxCount > 0 ? (float)totalNotes / maxCount * max_bar_height : 0;
                                                float regularNormalizedHeight = maxCount > 0 ? (float)regularNotes / maxCount * max_bar_height : 0;

                                                return new Container
                                                {
                                                    Size = new Vector2(bar_width, max_bar_height + 20), // +20 for text space
                                                    Margin = new MarginPadding { Right = bar_spacing },
                                                    Children = new Drawable[]
                                                    {
                                                        // 柱子背景（可选，用于更好的视觉效果）
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = max_bar_height,
                                                            Colour = Color4.Gray.Opacity(0.2f),
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Margin = new MarginPadding { Bottom = 15 }
                                                        },
                                                        // 普通notes柱（底部）
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = regularNormalizedHeight,
                                                            Colour = Color4.LightCoral,
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Margin = new MarginPadding { Bottom = 15 }
                                                        },
                                                        // 长按notes柱（叠加在普通notes上面）
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = totalNormalizedHeight - regularNormalizedHeight,
                                                            Colour = holdNoteColor,
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Margin = new MarginPadding { Bottom = 15 + regularNormalizedHeight }
                                                        },
                                                        // 列标签
                                                        new OsuSpriteText
                                                        {
                                                            Text = $"{c.Key + 1}",
                                                            Font = OsuFont.GetFont(size: 12),
                                                            Colour = Color4.Gray,
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Margin = new MarginPadding { Bottom = 2 }
                                                        },
                                                        // 数值标签（显示在柱子顶部）
                                                        new OsuSpriteText
                                                        {
                                                            Text = holdNotes > 0 ? $"{totalNotes}({holdNotes})" : totalNotes.ToString(),
                                                            Font = OsuFont.GetFont(size: 10),
                                                            Colour = Color4.White,
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Y = -(totalNormalizedHeight + 17) // 定位在柱子顶部
                                                        }
                                                    }
                                                };
                                            }).ToArray();
        }

        /// <summary>
        /// 清空显示
        /// </summary>
        public void Clear()
        {
            currentColumnCounts = null;
            columnNotesContainer.Clear();
        }
    }
}
