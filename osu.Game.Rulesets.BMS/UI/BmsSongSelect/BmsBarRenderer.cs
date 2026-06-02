// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public partial class BmsBarRenderer : CompositeDrawable
    {
        private const int visible_rows = 28;
        private const float row_height = 24;

        private readonly BmsBarManager manager;
        private readonly BmsBarContext context;
        private readonly FillFlowContainer listFlow;
        private readonly OsuSpriteText breadcrumbText;
        private readonly OsuSpriteText statusText;
        private readonly OsuSpriteText detailTitle;
        private readonly OsuSpriteText detailBody;

        public BmsBarRenderer(BmsBarManager manager, BmsBarContext context)
        {
            this.manager = manager;
            this.context = context;

            RelativeSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(12, 14, 22, 255),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 72,
                        Padding = new MarginPadding { Horizontal = 24, Vertical = 12 },
                        Children = new Drawable[]
                        {
                            breadcrumbText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                Text = BmsStrings.RAJA_ROOT_LABEL,
                            },
                            statusText = new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Font = OsuFont.Default.With(size: 13),
                                Colour = Colour4.Gray,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Y = -4,
                                Font = OsuFont.Default.With(size: 12),
                                Colour = Colour4.Gray,
                                Text = BmsStrings.RAJA_KEY_HINTS,
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 72, Horizontal = 16, Bottom = 16 },
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Width = 0.58f,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Colour4.Black.Opacity(0.25f),
                                    },
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Padding = new MarginPadding(8),
                                        Child = listFlow = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                        },
                                    },
                                },
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Width = 0.4f,
                                Padding = new MarginPadding { Left = 12 },
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Colour4.White.Opacity(0.06f),
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Padding = new MarginPadding(16),
                                        Spacing = new Vector2(0, 8),
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                                                Text = BmsStrings.RAJA_SELECTION_DETAIL,
                                            },
                                            detailTitle = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                                Text = BmsStrings.RAJA_PLACEHOLDER_DASH,
                                            },
                                            detailBody = new OsuSpriteText
                                            {
                                                Font = OsuFont.Default.With(size: 13),
                                                Colour = Colour4.LightGray,
                                                Text = BmsStrings.RAJA_SELECT_CHART_FOR_ANALYTICS,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            manager.Changed += refresh;
        }

        private void refresh()
        {
            breadcrumbText.Text = manager.Breadcrumb;
            statusText.Text = $"KEY: {context.KeyModeFilter.Current} | 排序: {context.SortPolicy.Mode} | {manager.CurrentBars.Count} 项";
            listFlow.Clear();

            if (manager.CurrentBars.Count == 0)
            {
                listFlow.Add(new OsuSpriteText { Text = BmsStrings.RAJA_EMPTY_LIST, Font = OsuFont.Default });
                updateDetail(null);
                return;
            }

            int start = Math.Max(0, manager.SelectedIndex - visible_rows / 2);
            int end = Math.Min(manager.CurrentBars.Count, start + visible_rows);
            start = Math.Max(0, end - visible_rows);

            for (int i = start; i < end; i++)
            {
                var bar = manager.CurrentBars[i];
                bool selected = i == manager.SelectedIndex;
                int captured = i;
                listFlow.Add(createRow(bar, selected, () =>
                {
                    manager.SetSelectedIndex(captured);
                    manager.OpenSelected();
                }));
            }

            updateDetail(manager.GetSelectedBar());
        }

        private void updateDetail(BmsBar? bar)
        {
            if (bar == null)
            {
                detailTitle.Text = BmsStrings.RAJA_PLACEHOLDER_DASH;
                detailBody.Text = string.Empty;
                return;
            }

            detailTitle.Text = bar.Title;

            if (bar is BmsSongBar song)
            {
                var c = song.Chart;
                string analyticsLine = BmsStrings.RAJA_ANALYTICS_NONE.ToString();

                if (context.Analytics.TryGet(song.PathKey, out BmsAnalyticsRecord record))
                {
                    analyticsLine = BmsStrings.Raja_DetailAnalytics(
                        record.Pp ?? 0,
                        record.XxySr ?? 0,
                        record.AvgKps ?? 0,
                        record.MaxKps ?? 0,
                        record.StarRating ?? 0);
                }

                detailBody.Text = string.Join("\n",
                    BmsStrings.Raja_DetailArtist(c.Artist),
                    BmsStrings.Raja_DetailLevel(c.PlayLevel, c.KeyCount, c.FileName),
                    BmsStrings.Raja_DetailBpm(c.Bpm, c.TotalNotes),
                    BmsStrings.Raja_DetailPath(c.FolderPath),
                    analyticsLine);
            }
            else if (bar is BmsDirectoryBar)
            {
                detailBody.Text = BmsStrings.RAJA_DIRECTORY_ENTER_HINT;
            }
            else
            {
                detailBody.Text = bar.Subtitle;
            }
        }

        private Drawable createRow(BmsBar bar, bool selected, Action onActivate)
        {
            if (bar is BmsSectionLabelBar section)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                    Padding = new MarginPadding { Top = 6, Bottom = 2 },
                    Child = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = Colour4.Gray,
                        Text = section.Title,
                    },
                };
            }

            string suffix = bar is BmsSongBar songBar ? formatAnalytics(songBar) : string.Empty;
            string prefix = bar.IsDirectory ? "▶ " : bar is BmsRandomExecutableBar ? "🎲 " : "♪ ";
            string text = prefix + bar.Title + (string.IsNullOrEmpty(suffix) ? string.Empty : $"    {suffix}");

            return new BmsRajaBarRow(bar, selected, text, onActivate)
            {
                Height = row_height,
            };
        }

        private string formatAnalytics(BmsSongBar song)
        {
            if (!context.Analytics.TryGet(song.PathKey, out BmsAnalyticsRecord record))
                return BmsStrings.RAJA_PLACEHOLDER_DASH.ToString();

            return BmsStrings.Raja_RowAnalyticsSuffix(record.Pp ?? 0, record.XxySr ?? 0, record.AvgKps ?? 0);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    manager.MoveSelection(-1);
                    return true;

                case Key.Down:
                    manager.MoveSelection(1);
                    return true;

                case Key.Enter:
                    manager.OpenSelected();
                    return true;

                case Key.Escape:
                case Key.Left:
                    manager.CloseFolder();
                    return true;
            }

            return base.OnKeyDown(e);
        }
    }

    public partial class BmsRajaBarRow : CompositeDrawable
    {
        private readonly Action onActivate;

        public BmsRajaBarRow(BmsBar bar, bool selected, string displayText, Action onActivate)
        {
            this.onActivate = onActivate;
            RelativeSizeAxes = Axes.X;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = selected ? Colour4.SkyBlue.Opacity(0.4f) : Colour4.Transparent,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding { Left = 10, Right = 10 },
                        Font = OsuFont.Default.With(size: 14),
                        Text = displayText,
                    },
                },
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            onActivate();
            return true;
        }
    }
}
