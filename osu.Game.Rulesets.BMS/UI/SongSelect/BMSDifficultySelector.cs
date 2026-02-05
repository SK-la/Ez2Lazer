// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// 难度切换按钮容器，横向排列，超过5个难度时启用滚动
    /// </summary>
    public partial class BMSDifficultySelector : CompositeDrawable
    {
        private const int max_visible_difficulties = 5;
        private const float button_spacing = 8;

        private FillFlowContainer<BMSDifficultyButton> difficultyFlow = null!;
        private OsuScrollContainer<Drawable> scrollContainer = null!;

        public readonly Bindable<BMSChartCache?> SelectedChart = new Bindable<BMSChartCache?>();

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 50;

            InternalChild = scrollContainer = new OsuScrollContainer<Drawable>(Direction.Horizontal)
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = difficultyFlow = new FillFlowContainer<BMSDifficultyButton>
                {
                    AutoSizeAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(button_spacing, 0),
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
                }
            };
        }

        /// <summary>
        /// 设置难度列表
        /// </summary>
        public void SetDifficulties(IEnumerable<BMSChartCache> charts)
        {
            difficultyFlow.Clear();

            var chartList = charts.OrderBy(c => c.PlayLevel).ToList();

            foreach (var chart in chartList)
            {
                var button = new BMSDifficultyButton(chart)
                {
                    Action = () => SelectedChart.Value = chart,
                };

                button.SelectedChart.BindTo(SelectedChart);
                difficultyFlow.Add(button);
            }

            // 如果难度数量小于等于5个，固定宽度不滚动
            if (chartList.Count <= max_visible_difficulties)
            {
                scrollContainer.ScrollbarVisible = false;
                difficultyFlow.AutoSizeAxes = Axes.X;
            }
            else
            {
                scrollContainer.ScrollbarVisible = true;
            }

            // 自动选择第一个难度
            if (chartList.Count > 0)
                SelectedChart.Value = chartList.First();
        }

        /// <summary>
        /// 清空难度列表
        /// </summary>
        public void Clear()
        {
            difficultyFlow.Clear();
            SelectedChart.Value = null;
        }
    }
}
