// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// 难度切换按钮，用于左侧面板顶部
    /// </summary>
    public partial class BMSDifficultyButton : OsuClickableContainer
    {
        private readonly BMSChartCache chart;
        private Box background = null!;
        private OsuSpriteText difficultyText = null!;
        private Container clearLamp = null!;

        public readonly Bindable<BMSChartCache?> SelectedChart = new Bindable<BMSChartCache?>();

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BMSDifficultyButton(BMSChartCache chart)
        {
            this.chart = chart;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Width = 120;
            Height = 40;
            Masking = true;
            CornerRadius = 5;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(5),
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Children = new Drawable[]
                    {
                        clearLamp = new Container
                        {
                            Width = 8,
                            RelativeSizeAxes = Axes.Y,
                            Margin = new MarginPadding { Vertical = 5 },
                            CornerRadius = 2,
                            Masking = true,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = getClearLampColour(),
                            }
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            AutoSizeAxes = Axes.None,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            Children = new Drawable[]
                            {
                                difficultyText = new OsuSpriteText
                                {
                                    Text = chart.SubTitle ?? "Unknown",
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                                    Truncate = true,
                                    RelativeSizeAxes = Axes.X,
                                },
                                new OsuSpriteText
                                {
                                    Text = $"★{chart.PlayLevel}",
                                    Font = OsuFont.GetFont(size: 11),
                                    Colour = colourProvider.Content2,
                                }
                            }
                        }
                    }
                }
            };

            SelectedChart.BindValueChanged(selected =>
            {
                bool isSelected = selected.NewValue == chart;
                background.FadeColour(isSelected ? colourProvider.Highlight1 : colourProvider.Background3, 200);
                difficultyText.FadeColour(isSelected ? Color4.White : colourProvider.Content1, 200);
            }, true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (SelectedChart.Value != chart)
                background.FadeColour(colourProvider.Background4, 100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (SelectedChart.Value != chart)
                background.FadeColour(colourProvider.Background3, 200);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            SelectedChart.Value = chart;
            return base.OnClick(e);
        }

        /// <summary>
        /// 根据Clear等级返回对应的颜色
        /// TODO: 从实际成绩数据获取Clear等级
        /// </summary>
        private Color4 getClearLampColour()
        {
            // 暂时返回默认灰色，后续根据成绩数据设置
            // Clear等级颜色参考：
            // - No Play: Gray
            // - Failed: Red
            // - Assist Clear: Purple
            // - Easy Clear: Green
            // - Clear: Blue
            // - Hard Clear: Orange
            // - Full Combo: Yellow/Gold
            // - Perfect: Rainbow/Cyan
            return Color4.Gray;
        }
    }
}
