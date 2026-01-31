// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Colour;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Card displaying a single BMS chart (difficulty) in the detail panel.
    /// </summary>
    public partial class BMSChartCard : CompositeDrawable
    {
        public BMSChartCache Chart { get; }
        public Action? Action { get; set; }

        private Box background = null!;
        private Box hoverBox = null!;
        private Color4 normalColour;
        private Color4 selectedColour;

        private bool selected;
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value) return;
                selected = value;
                background.Colour = selected ? selectedColour : normalColour;
            }
        }

        public BMSChartCard(BMSChartCache chart)
        {
            Chart = chart;

            RelativeSizeAxes = Axes.X;
            Height = 50;
            Masking = true;
            CornerRadius = 5;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            normalColour = colours.Gray4;
            selectedColour = colours.Green;

            // Determine difficulty color based on level
            var difficultyColour = Chart.PlayLevel switch
            {
                <= 3 => colours.Green,
                <= 6 => colours.Blue,
                <= 9 => colours.Yellow,
                <= 11 => colours.Red,
                _ => colours.Pink,
            };

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = normalColour,
                },
                hoverBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.White.Opacity(0.1f),
                    Alpha = 0,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 50),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 100),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            // Level badge
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = difficultyColour,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = Chart.PlayLevel.ToString(),
                                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                    },
                                },
                            },
                            // Chart info
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding { Left = 10, Vertical = 5 },
                                Spacing = new Vector2(0, 2),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = string.IsNullOrEmpty(Chart.SubTitle) ? Chart.FileName : Chart.SubTitle,
                                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                        Truncate = true,
                                        RelativeSizeAxes = Axes.X,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = $"{Chart.KeyCount}K{(Chart.HasScratch ? "+S" : "")} | {Chart.TotalNotes} notes",
                                        Font = OsuFont.GetFont(size: 11),
                                        Colour = colours.Gray9,
                                    },
                                },
                            },
                            // BPM info
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(5),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Text = $"BPM {Chart.Bpm:F0}",
                                        Font = OsuFont.GetFont(size: 12),
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Text = FormatDuration(Chart.Duration),
                                        Font = OsuFont.GetFont(size: 11),
                                        Colour = colours.Gray9,
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private static string FormatDuration(double milliseconds)
        {
            var ts = TimeSpan.FromMilliseconds(milliseconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBox.FadeIn(100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBox.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            return true;
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            // Double click to start game immediately
            Action?.Invoke();
            return true;
        }
    }
}
