// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// 左侧信息面板，包含难度切换按钮、谱面信息和成绩
    /// </summary>
    public partial class BMSInfoPanel : CompositeDrawable
    {
        private BMSDifficultySelector difficultySelector = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private OsuSpriteText genreText = null!;
        private OsuSpriteText bpmText = null!;
        private OsuSpriteText chartTitleText = null!;
        private OsuSpriteText chartMetaText = null!;
        private OsuSpriteText chartStatsText = null!;
        private OsuSpriteText chartExtraText = null!;
        private Container scoreContainer = null!;

        public readonly Bindable<BMSSongCache?> SelectedSong = new Bindable<BMSSongCache?>();
        public readonly Bindable<BMSChartCache?> SelectedChart = new Bindable<BMSChartCache?>();

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 20, Vertical = 10 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                        Alpha = 0.9f,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 15),
                        Padding = new MarginPadding(15),
                        Children = new Drawable[]
                        {
                            // 难度切换按钮区域
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 220,
                                Child = difficultySelector = new BMSDifficultySelector
                                {
                                    RelativeSizeAxes = Axes.Both,
                                }
                            },
                            // 分隔线
                            new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 2,
                                Colour = colourProvider.Background3,
                            },
                            // 谱面信息区域
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                                Children = new Drawable[]
                                {
                                    titleText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                                        Text = "Select a song",
                                    },
                                    artistText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 18),
                                        Colour = colourProvider.Content2,
                                        Text = string.Empty,
                                    },
                                    chartTitleText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                                        Text = string.Empty,
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(15, 0),
                                        Children = new Drawable[]
                                        {
                                            genreText = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14),
                                                Colour = colourProvider.Content2,
                                                Text = string.Empty,
                                            },
                                            bpmText = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14),
                                                Colour = colourProvider.Content2,
                                                Text = string.Empty,
                                            }
                                        }
                                    },
                                    chartMetaText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                        Text = string.Empty,
                                    },
                                    chartStatsText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                        Text = string.Empty,
                                    },
                                    chartExtraText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                        Text = string.Empty,
                                    }
                                }
                            },
                            // 分隔线
                            new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 2,
                                Colour = colourProvider.Background3,
                            },
                            // 成绩区域
                            scoreContainer = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Child = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 14),
                                    Colour = colourProvider.Content2,
                                    Text = "No scores yet",
                                }
                            }
                        }
                    }
                }
            };

            difficultySelector.SelectedChart.BindTo(SelectedChart);

            SelectedSong.BindValueChanged(onSongChanged, true);
            SelectedChart.BindValueChanged(onChartChanged, true);
        }

        private void onSongChanged(ValueChangedEvent<BMSSongCache?> e)
        {
            if (e.NewValue == null)
            {
                titleText.Text = "Select a song";
                artistText.Text = string.Empty;
                genreText.Text = string.Empty;
                bpmText.Text = string.Empty;
                chartTitleText.Text = string.Empty;
                chartMetaText.Text = string.Empty;
                chartStatsText.Text = string.Empty;
                chartExtraText.Text = string.Empty;
                difficultySelector.Clear();
                return;
            }

            var song = e.NewValue;
            titleText.Text = string.IsNullOrWhiteSpace(song.Title) ? "Unknown Title" : song.Title;
            artistText.Text = string.IsNullOrWhiteSpace(song.Artist) ? "Unknown Artist" : song.Artist;
            genreText.Text = $"Genre: {(string.IsNullOrWhiteSpace(song.Genre) ? "Unknown" : song.Genre)}";

            // 设置难度列表
            difficultySelector.SetDifficulties(song.Charts);
        }

        private void onChartChanged(ValueChangedEvent<BMSChartCache?> e)
        {
            if (e.NewValue == null)
            {
                bpmText.Text = string.Empty;
                chartTitleText.Text = string.Empty;
                chartMetaText.Text = string.Empty;
                chartStatsText.Text = string.Empty;
                chartExtraText.Text = string.Empty;
                return;
            }

            var chart = e.NewValue;
            bpmText.Text = $"BPM: {BMSChartDisplayFormatter.GetBpmText(chart)}";
            chartTitleText.Text = $"难度: {BMSChartDisplayFormatter.GetDifficultyTitle(chart)}";
            chartMetaText.Text = $"模式: {BMSChartDisplayFormatter.GetModeText(chart)} | {BMSChartDisplayFormatter.GetLevelText(chart)} | Rank {chart.Rank}";
            chartStatsText.Text = $"{chart.TotalNotes} Notes | Keysounds {chart.KeysoundFiles.Count} | Total {chart.Total:F0}";
            chartExtraText.Text = $"时长: {BMSChartDisplayFormatter.GetDurationText(chart.Duration)} | {BMSChartDisplayFormatter.GetFlagsText(chart)} | 文件: {chart.FileName}";

            // TODO: 加载并显示该难度的成绩
            updateScores(chart);
        }

        private void updateScores(BMSChartCache chart)
        {
            // TODO: 实现成绩显示
            // 这里需要从成绩数据库或文件中加载该难度的历史成绩
            scoreContainer.Child = new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 14),
                Colour = colourProvider.Content2,
                Text = "No scores yet",
            };
        }
    }
}
