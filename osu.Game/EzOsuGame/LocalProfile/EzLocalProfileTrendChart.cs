// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Local-profile trend chart aligned with official <c>ProfileLineChart</c>:
    /// Y ticks + grid + hover ball/tooltip + bottom X labels. Axis labels are single values (no ranges).
    /// Requires at least two data points.
    /// </summary>
    public partial class EzLocalProfileTrendChart : CompositeDrawable
    {
        public const float DEFAULT_HEIGHT = 180f;

        private readonly float[] values;
        private readonly string[] xLabels;
        private readonly LocalisableString counterName;
        private readonly IconUsage? xLabelIcon;
        private readonly Func<float, string> formatY;
        private readonly float? forcedMinValue;

        private readonly TrendLineGraph graph;
        private readonly Container<TickText> rowTicksContainer;
        private readonly Container columnTicksContainer;
        private readonly Container<TickLine> rowLinesContainer;
        private readonly Container<TickLine> columnLinesContainer;

        private float actualMin;
        private float actualMax;

        public EzLocalProfileTrendChart(
            float[] values,
            string[] xLabels,
            LocalisableString counterName,
            IconUsage? xLabelIcon = null,
            Func<float, string>? formatY = null,
            float? minValue = null)
        {
            if (values.Length < 2)
                throw new ArgumentException("At least two values expected.", nameof(values));
            if (xLabels.Length != values.Length)
                throw new ArgumentException("xLabels length must match values.", nameof(xLabels));

            this.values = values;
            this.xLabels = xLabels;
            this.counterName = counterName;
            this.xLabelIcon = xLabelIcon;
            this.formatY = formatY ?? defaultFormatY;
            forcedMinValue = minValue;

            RelativeSizeAxes = Axes.X;
            Height = DEFAULT_HEIGHT;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension()
                },
                RowDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize)
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        rowTicksContainer = new Container<TickText>
                        {
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        rowLinesContainer = new Container<TickLine>
                                        {
                                            RelativeSizeAxes = Axes.Both
                                        },
                                        columnLinesContainer = new Container<TickLine>
                                        {
                                            RelativeSizeAxes = Axes.Both
                                        }
                                    }
                                },
                                graph = new TrendLineGraph(counterName, this.formatY)
                                {
                                    RelativeSizeAxes = Axes.Both
                                }
                            }
                        }
                    },
                    new[]
                    {
                        Empty(),
                        columnTicksContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Top = 8 }
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            actualMax = values.Max();
            actualMin = forcedMinValue.HasValue
                ? Math.Min(forcedMinValue.Value, values.Min())
                : values.Min();

            graph.MinValue = forcedMinValue;
            graph.SetSeries(values, xLabels);

            createRowTicks();
            createColumnTicks(colours);
        }

        private void createRowTicks()
        {
            rowTicksContainer.Clear();
            rowLinesContainer.Clear();

            float range = actualMax - actualMin;
            float tickInterval = getTickInterval(range, 6);

            // Start from a tick at or below min that sits on the interval grid.
            float firstTick = actualMin;
            if (tickInterval > 0 && range > 0)
                firstTick = (float)(Math.Floor(actualMin / tickInterval) * tickInterval);

            for (float current = firstTick; current <= actualMax + tickInterval * 0.001f; current += tickInterval)
            {
                if (current < actualMin - tickInterval * 0.001f)
                    continue;

                float y;

                if (actualMin == actualMax)
                    y = current > 1 ? 1 : 0;
                else
                    y = Interpolation.ValueAt(current, 0, 1f, actualMin, actualMax);

                addRowTick(-y, current);

                if (tickInterval <= 0)
                    break;
            }
        }

        private void createColumnTicks(OverlayColourProvider colours)
        {
            columnTicksContainer.Clear();
            columnLinesContainer.Clear();

            int count = values.Length;
            int step = 1;

            if (count > 80)
                step = 12;
            else if (count >= 45)
                step = 3;
            else if (count > 20)
                step = 2;
            else if (count > 12)
                step = 2;

            var metaColour = EzLocalProfileColours.Meta(colours);

            for (int i = 0; i < count; i += step)
            {
                float x = i / (float)(count - 1);
                addColumnTick(x, xLabels[i], metaColour);
            }
        }

        private void addRowTick(float y, float value)
        {
            rowTicksContainer.Add(new TickText
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.CentreRight,
                RelativePositionAxes = Axes.Y,
                Margin = new MarginPadding { Right = 4 },
                Text = formatY(value),
                Font = OsuFont.GetFont(size: 11),
                Y = y
            });

            rowLinesContainer.Add(new TickLine
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.X,
                RelativePositionAxes = Axes.Y,
                Height = 0.1f,
                EdgeSmoothness = Vector2.One,
                Y = y
            });
        }

        private void addColumnTick(float x, string label, Colour4 metaColour)
        {
            Drawable labelDrawable;

            if (xLabelIcon == null)
            {
                labelDrawable = new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = metaColour,
                };
            }
            else
            {
                labelDrawable = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(2, 0),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = label,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                            Colour = metaColour,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new SpriteIcon
                        {
                            Icon = xLabelIcon.Value,
                            Size = new Vector2(9),
                            Colour = metaColour,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    }
                };
            }

            columnTicksContainer.Add(new Container
            {
                Origin = Anchor.TopCentre,
                RelativePositionAxes = Axes.X,
                AutoSizeAxes = Axes.Both,
                X = x,
                Child = labelDrawable,
            });

            columnLinesContainer.Add(new TickLine
            {
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.Y,
                RelativePositionAxes = Axes.X,
                Width = 0.1f,
                EdgeSmoothness = Vector2.One,
                X = x
            });
        }

        private static string defaultFormatY(float value)
        {
            if (Math.Abs(value % 1f) < 0.0001f)
                return ((int)Math.Round(value)).ToString("N0", CultureInfo.InvariantCulture);

            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static float getTickInterval(float range, int maxTicksCount)
        {
            if (range <= 0)
                return 1;

            float exactTickInterval = range / (maxTicksCount - 1);
            double numberOfDigits = Math.Floor(Math.Log10(exactTickInterval));
            double tickBase = Math.Pow(10, numberOfDigits);
            double exactTickMultiplier = exactTickInterval / tickBase;

            double tickMultiplier;

            if (exactTickMultiplier < 1.5)
                tickMultiplier = 1.0;
            else if (exactTickMultiplier < 3)
                tickMultiplier = 2.0;
            else if (exactTickMultiplier < 7)
                tickMultiplier = 5.0;
            else
                tickMultiplier = 10.0;

            float interval = (float)(tickMultiplier * tickBase);
            return Math.Max(interval, range < 1 ? interval : Math.Max(interval, 0.01f));
        }

        private partial class TickText : OsuSpriteText
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Colour = colourProvider.Foreground1;
            }
        }

        private partial class TickLine : Box
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Colour = colourProvider.Background6;
            }
        }

        private partial class TrendLineGraph : LineGraph, IHasCustomTooltip<TrendTooltipContent?>
        {
            private const float fade_duration = 150;

            private readonly LocalisableString counterName;
            private readonly Func<float, string> formatY;
            private readonly CircularContainer movingBall;
            private readonly Container bar;
            private readonly Box ballBg;
            private readonly Box line;

            private string[] labels = Array.Empty<string>();
            private int hoveredIndex = -1;
            private float lastHoverPosition;

            public TrendLineGraph(LocalisableString counterName, Func<float, string> formatY)
            {
                this.counterName = counterName;
                this.formatY = formatY;

                Add(bar = new Container
                {
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Alpha = 0,
                    RelativePositionAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        line = new Box
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Y,
                            Width = 2,
                        },
                        movingBall = new CircularContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(16),
                            Masking = true,
                            BorderThickness = 3,
                            RelativePositionAxes = Axes.Y,
                            Child = ballBg = new Box { RelativeSizeAxes = Axes.Both }
                        }
                    }
                });
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                LineColour = colours.Highlight1;
                ballBg.Colour = colours.Background5;
                movingBall.BorderColour = line.Colour = colours.Highlight1;
            }

            public void SetSeries(float[] seriesValues, string[] seriesLabels)
            {
                labels = seriesLabels;
                DefaultValueCount = seriesValues.Length;
                Values = seriesValues;
                this.FadeIn(fade_duration, Easing.Out);
            }

            public ITooltip<TrendTooltipContent?> GetCustomTooltip() => new TrendTooltip();

            public TrendTooltipContent? TooltipContent
            {
                get
                {
                    if (hoveredIndex < 0 || Values == null)
                        return null;

                    float[] series = Values.ToArray();
                    if (hoveredIndex >= series.Length)
                        return null;

                    return new TrendTooltipContent
                    {
                        Name = counterName,
                        Count = formatY(series[hoveredIndex]),
                        Time = hoveredIndex < labels.Length ? labels[hoveredIndex] : string.Empty,
                    };
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (DefaultValueCount > 1)
                {
                    updateBallPosition(lastHoverPosition = e.MousePosition.X);
                    bar.FadeIn(fade_duration);
                    return true;
                }

                return base.OnHover(e);
            }

            protected override bool OnMouseMove(MouseMoveEvent e)
            {
                if (DefaultValueCount > 1)
                    updateBallPosition(e.MousePosition.X);

                return base.OnMouseMove(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                bar.FadeOut(fade_duration);
                hoveredIndex = -1;
                base.OnHoverLost(e);
            }

            private void updateBallPosition(float mouseXPosition)
            {
                const int duration = 200;
                int index = (int)Math.Clamp(
                    MathF.Round(mouseXPosition / DrawWidth * (DefaultValueCount - 1)),
                    0,
                    DefaultValueCount - 1);

                hoveredIndex = index;
                float y = GetYPosition(Values.ElementAt(index));
                float x = index / (float)(DefaultValueCount - 1);

                movingBall.MoveToY(y, duration, Easing.OutQuint);
                bar.MoveToX(x, duration, Easing.OutQuint);
            }
        }

        private class TrendTooltipContent
        {
            public LocalisableString Name { get; init; }
            public LocalisableString Count { get; init; }
            public LocalisableString Time { get; init; }
        }

        private partial class TrendTooltip : VisibilityContainer, ITooltip<TrendTooltipContent?>
        {
            private readonly OsuSpriteText label;
            private readonly OsuSpriteText counter;
            private readonly OsuSpriteText bottomText;
            private readonly Box background;
            private bool instantMove = true;

            public TrendTooltip()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 10;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(10),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(3, 0),
                                Children = new Drawable[]
                                {
                                    label = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                    },
                                    counter = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
                                        Anchor = Anchor.BottomLeft,
                                        Origin = Anchor.BottomLeft,
                                    }
                                }
                            },
                            bottomText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
                            }
                        }
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                background.Colour = colours.Gray1;
            }

            public void SetContent(TrendTooltipContent? content)
            {
                if (content == null)
                    return;

                label.Text = content.Name;
                counter.Text = content.Count;
                bottomText.Text = content.Time;
            }

            public void Move(Vector2 pos)
            {
                if (instantMove)
                {
                    Position = pos;
                    instantMove = false;
                }
                else
                    this.MoveTo(pos, 200, Easing.OutQuint);
            }

            protected override void PopIn()
            {
                instantMove |= !IsPresent;
                this.FadeIn(200, Easing.OutQuint);
            }

            protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);
        }
    }
}
