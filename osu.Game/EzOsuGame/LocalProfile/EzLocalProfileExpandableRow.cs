// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileExpandableRow : CompositeDrawable
    {
        private readonly string header;
        private readonly IReadOnlyList<EzLocalProfileManiaColumnStats> columns;
        private Container detailFlow = null!;
        private SpriteIcon chevron = null!;
        private bool expanded;

        public EzLocalProfileExpandableRow(EzLocalProfileManiaKeyStats keyStats, IReadOnlyList<EzLocalProfileManiaColumnStats> columns)
        {
            this.columns = columns;
            header =
                $"{keyStats.KeyCount}K  ·  {keyStats.TotalKeys:N0} keys  ·  avg {keyStats.AvgKps.ToString("0.00", CultureInfo.InvariantCulture)}  ·  max {keyStats.MaxKps.ToString("0.00", CultureInfo.InvariantCulture)}  ·  {keyStats.ScoreCount} plays";

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    new HeaderButton(toggle)
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 36,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Horizontal = 12 },
                            Children = new Drawable[]
                            {
                                chevron = new SpriteIcon
                                {
                                    Icon = FontAwesome.Solid.ChevronRight,
                                    Size = new Vector2(12),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Colour = colours.Content2,
                                },
                                new OsuSpriteText
                                {
                                    Text = header,
                                    Font = OsuFont.GetFont(size: 13),
                                    Colour = colours.Content1,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Margin = new MarginPadding { Left = 22 },
                                }
                            }
                        }
                    },
                    detailFlow = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Alpha = 0,
                        Padding = new MarginPadding { Left = 4, Right = 4, Bottom = 4 },
                        Child = new EzLocalProfileColumnTable(columns)
                    }
                }
            };
        }

        private void toggle()
        {
            expanded = !expanded;
            detailFlow.FadeTo(expanded ? 1 : 0, 150, Easing.OutQuint);
            chevron.RotateTo(expanded ? 90 : 0, 150, Easing.OutQuint);
        }

        private partial class HeaderButton : OsuClickableContainer
        {
            private Box background = null!;
            private Colour4 idleColour;
            private Colour4 hoverColour;

            public HeaderButton(System.Action action)
            {
                Action = action;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                idleColour = colours.Background5;
                hoverColour = colours.Background6;

                AddInternal(background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                    Depth = float.MaxValue,
                });
            }

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
            {
                background.FadeColour(hoverColour, 200, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
            {
                background.FadeColour(idleColour, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }

    /// <summary>
    /// Mania key-mode overview: rounded play bars + avg-KPS line graph.
    /// </summary>
    public partial class EzLocalProfileManiaOverview : FillFlowContainer
    {
        public EzLocalProfileManiaOverview(IReadOnlyList<EzLocalProfileManiaKeyStats> keyStats)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 12);

            var ordered = keyStats.OrderBy(k => k.KeyCount).ToList();
            if (ordered.Count == 0)
                return;

            int maxPlays = ordered.Max(k => k.ScoreCount);

            Add(new OsuSpriteText
            {
                Text = EzSettingsStrings.LOCAL_PROFILE_MANIA_PLAYS_BY_KEY,
                Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
            });

            var barFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            foreach (var key in ordered)
            {
                float ratio = maxPlays <= 0 ? 0 : (float)key.ScoreCount / maxPlays;
                barFlow.Add(new KeyPlayBarRow($"{key.KeyCount}K", key.ScoreCount, ratio));
            }

            Add(barFlow);

            Add(new OsuSpriteText
            {
                Text = EzSettingsStrings.LOCAL_PROFILE_MANIA_AVG_KPS_LINE,
                Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                Margin = new MarginPadding { Top = 4 },
            });

            Add(new EzLocalProfileLabeledLineChart(
                ordered.Select(k => (float)k.AvgKps).ToArray(),
                ordered.Select(k => $"{k.KeyCount}K").ToArray())
            {
                RelativeSizeAxes = Axes.X,
            });
        }

        private partial class KeyPlayBarRow : Container
        {
            private readonly string label;
            private readonly int plays;
            private readonly float ratio;

            public KeyPlayBarRow(string label, int plays, float ratio)
            {
                this.label = label;
                this.plays = plays;
                this.ratio = ratio;
                RelativeSizeAxes = Axes.X;
                Height = 20;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = label,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Width = 36,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 40, Right = 48 },
                        Child = new EzLocalProfileRoundedBar(ratio),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = plays.ToString("N0"),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = colours.Content2,
                    }
                };
            }
        }
    }
}
