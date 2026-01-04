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
        public enum DisplayMode
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
        private DisplayMode currentDisplayMode = DisplayMode.Numbers;
        private Dictionary<int, int>? currentColumnCounts;

        /// <summary>
        /// 当前显示模式
        /// </summary>
        public DisplayMode CurrentDisplayMode
        {
            get => currentDisplayMode;
            set
            {
                if (currentDisplayMode == value)
                    return;

                currentDisplayMode = value;

                // 如果有数据，立即重新渲染
                if (currentColumnCounts != null)
                {
                    updateDisplay(currentColumnCounts);
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
        public void UpdateColumnCounts(Dictionary<int, int> columnNoteCounts)
        {
            currentColumnCounts = columnNoteCounts;
            updateDisplay(columnNoteCounts);
        }

        private void updateDisplay(Dictionary<int, int> columnNoteCounts)
        {
            columnNotesContainer.Clear();

            if (!columnNoteCounts.Any())
                return;

            switch (currentDisplayMode)
            {
                case DisplayMode.Numbers:
                    createNumbersDisplay(columnNoteCounts);
                    break;

                case DisplayMode.BarChart:
                    createBarChartDisplay(columnNoteCounts);
                    break;
            }
        }

        private void createNumbersDisplay(Dictionary<int, int> columnNoteCounts)
        {
            // 高性能的数字显示模式
            columnNotesContainer.Children = columnNoteCounts
                                            .OrderBy(c => c.Key)
                                            .Select(c => new FillFlowContainer
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
                                                        Text = $"{c.Value} ",
                                                        Font = OsuFont.GetFont(size: 16),
                                                        Colour = Color4.LightCoral,
                                                    }
                                                }
                                            }).ToArray();
        }

        private void createBarChartDisplay(Dictionary<int, int> columnNoteCounts)
        {
            var sortedCounts = columnNoteCounts.OrderBy(c => c.Key).ToArray();
            if (!sortedCounts.Any()) return;

            // 计算最大值用于归一化
            int maxCount = sortedCounts.Max(c => c.Value);
            if (maxCount == 0) return;

            const float max_bar_height = 30f;
            const float bar_width = 20f;
            const float bar_spacing = 2f;

            columnNotesContainer.Children = sortedCounts
                                            .Select(c =>
                                            {
                                                float normalizedHeight = maxCount > 0 ? (float)c.Value / maxCount * max_bar_height : 0;

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
                                                        // 实际数据柱
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = normalizedHeight,
                                                            Colour = Color4.LightCoral,
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Margin = new MarginPadding { Bottom = 15 }
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
                                                            Text = c.Value.ToString(),
                                                            Font = OsuFont.GetFont(size: 10),
                                                            Colour = Color4.White,
                                                            Anchor = Anchor.BottomCentre,
                                                            Origin = Anchor.BottomCentre,
                                                            Y = -(normalizedHeight + 17) // 定位在柱子顶部
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
