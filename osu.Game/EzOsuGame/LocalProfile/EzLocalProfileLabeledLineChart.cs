// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Line chart with per-point X labels and value labels.
    /// Optional <see cref="IconUsage"/> is drawn with <see cref="SpriteIcon"/> (do not interpolate IconUsage into strings).
    /// </summary>
    public partial class EzLocalProfileLabeledLineChart : Container
    {
        private readonly float[] values;
        private readonly string[] labels;
        private readonly IconUsage? labelIcon;

        public EzLocalProfileLabeledLineChart(float[] values, string[] labels, IconUsage? labelIcon = null)
        {
            this.values = values;
            this.labels = labels;
            this.labelIcon = labelIcon;

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
            var metaColour = EzLocalProfileColours.Meta(colours);

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
                    Colour = EzLocalProfileColours.Numeric(colours),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });

                string label = i < labels.Length ? labels[i] : string.Empty;

                xLabels.Add(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = cellWidth,
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2, 0),
                        Children = createLabelContent(label, metaColour),
                    }
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

        private Drawable[] createLabelContent(string label, Colour4 colour)
        {
            var text = new OsuSpriteText
            {
                Text = label,
                Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                Colour = colour,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            if (labelIcon == null)
                return new Drawable[] { text };

            return new Drawable[]
            {
                text,
                new SpriteIcon
                {
                    Icon = labelIcon.Value,
                    Size = new Vector2(9),
                    Colour = colour,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
            };
        }
    }
}
