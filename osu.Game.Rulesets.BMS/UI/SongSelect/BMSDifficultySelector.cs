// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// 难度切换按钮容器，使用纵向滚动列表展示同曲难度。
    /// </summary>
    public partial class BMSDifficultySelector : CompositeDrawable
    {
        private const float button_spacing = 8;

        private FillFlowContainer<BMSDifficultyButton> difficultyFlow = null!;

        public readonly Bindable<BMSChartCache?> SelectedChart = new Bindable<BMSChartCache?>();

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 220;

            InternalChild = new OsuScrollContainer<Drawable>(Direction.Vertical)
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = true,
                Child = difficultyFlow = new FillFlowContainer<BMSDifficultyButton>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, button_spacing),
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

            if (chartList.Count > 0)
            {
                SelectedChart.Value = chartList.FirstOrDefault(chart => string.Equals(chart.Md5Hash, SelectedChart.Value?.Md5Hash, StringComparison.OrdinalIgnoreCase))
                                      ?? chartList.First();
            }
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
