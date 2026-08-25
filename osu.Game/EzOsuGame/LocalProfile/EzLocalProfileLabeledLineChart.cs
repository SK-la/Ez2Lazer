// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Line chart with per-point X labels and value labels.
    /// </summary>
    public partial class EzLocalProfileLabeledLineChart : Container
    {
        private readonly float[] values;
        private readonly string[] labels;

        public EzLocalProfileLabeledLineChart(float[] values, string[] labels)
        {
            this.values = values;
            this.labels = labels;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            int count = values.Length;
            if (count == 0)
            {
                Height = 40;
                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 };
                return;
            }

            var valueLabels = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 16,
                Direction = FillDirection.Horizontal,
                Padding = new MarginPadding { Horizontal = 8, Top = 6 },
            };

            var xLabels = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 18,
                Direction = FillDirection.Horizontal,
                Padding = new MarginPadding { Horizontal = 8, Bottom = 6 },
            };

            float cellWidth = 1f / count;

            for (int i = 0; i < count; i++)
            {
                string valueText = values[i] % 1f == 0
                    ? ((int)values[i]).ToString("N0", CultureInfo.InvariantCulture)
                    : values[i].ToString("0.00", CultureInfo.InvariantCulture);

                valueLabels.Add(new TruncatingSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Width = cellWidth,
                    Text = valueText,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    Colour = colours.Highlight1,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });

                xLabels.Add(new TruncatingSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Width = cellWidth,
                    Text = i < labels.Length ? labels[i] : string.Empty,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    Colour = colours.Content2,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background5,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        valueLabels,
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 56,
                            Padding = new MarginPadding { Horizontal = 8 },
                            Child = new LineGraph
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Vertical = 4 },
                                MinValue = 0,
                                LineColour = colours.Highlight1,
                                Values = values,
                            }
                        },
                        xLabels,
                    }
                }
            };
        }
    }
}
