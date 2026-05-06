// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// 右侧列表的难度卡，参考PanelBeatmap设计，左侧模式图标改为Clear等级颜色灯
    /// </summary>
    public partial class BMSDifficultyCard : OsuClickableContainer
    {
        private const float height = 60;
        private const float clear_lamp_width = 12;

        private readonly BMSChartCache chart;
        private Box background = null!;
        private Box clearLamp = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText levelText = null!;
        private OsuSpriteText notesText = null!;

        public readonly Bindable<BMSChartCache?> SelectedChart = new Bindable<BMSChartCache?>();

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public BMSDifficultyCard(BMSChartCache chart)
        {
            this.chart = chart;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = height;
            Masking = true;
            CornerRadius = 5;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // Clear等级颜色灯
                        clearLamp = new Box
                        {
                            Width = clear_lamp_width,
                            RelativeSizeAxes = Axes.Y,
                            Colour = BmsClearLampColour.ForBestScore(null),
                        },
                        // 内容区域
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = clear_lamp_width + 10, Right = 10 },
                            Direction = FillDirection.Vertical,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Spacing = new Vector2(0, 4),
                            Children = new Drawable[]
                            {
                                // 顶部：难度名和Level
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8, 0),
                                    Children = new Drawable[]
                                    {
                                        difficultyText = new OsuSpriteText
                                        {
                                            Text = chart.SubTitle ?? "Unknown",
                                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                        },
                                        levelText = new OsuSpriteText
                                        {
                                            Text = $"★{chart.PlayLevel}",
                                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                            Colour = colourProvider.Highlight1,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        }
                                    }
                                },
                                // 底部：物量信息
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(12, 0),
                                    Children = new Drawable[]
                                    {
                                        notesText = new OsuSpriteText
                                        {
                                            Text = $"Notes: {chart.TotalNotes}",
                                            Font = OsuFont.GetFont(size: 12),
                                            Colour = colourProvider.Content2,
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = $"BPM: {chart.Bpm:F0}",
                                            Font = OsuFont.GetFont(size: 12),
                                            Colour = colourProvider.Content2,
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            SelectedChart.BindValueChanged(selected =>
            {
                bool isSelected = selected.NewValue == chart;
                background.FadeColour(isSelected ? colourProvider.Background4 : colourProvider.Background3, 200);
                difficultyText.FadeColour(isSelected ? Color4.White : colourProvider.Content1, 200);
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!string.IsNullOrEmpty(chart.Md5Hash))
            {
                var best = BmsLocalScoreQueries.GetBestLocalScore(realm, chart.Md5Hash);
                clearLamp.Colour = BmsClearLampColour.ForBestScore(best);
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (SelectedChart.Value != chart)
            {
                // 手动实现亮化效果
                var bgColour = colourProvider.Background4;
                var lighterColour = new Color4(
                    Math.Min(bgColour.R * 1.1f, 1f),
                    Math.Min(bgColour.G * 1.1f, 1f),
                    Math.Min(bgColour.B * 1.1f, 1f),
                    bgColour.A);
                background.FadeColour(lighterColour, 100);
            }

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
    }
}
