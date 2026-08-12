// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    /// <summary>
    /// Qwilight-inspired three-pane song select shell.
    /// </summary>
    public partial class BmsSongSelectShell : CompositeDrawable
    {
        private const float source_width = 0.26f;
        private const float list_width = 0.42f;
        private const float row_height = 22;

        private BmsSongSelectNavigator navigator;
        private BmsBarContext context;

        private readonly OsuSpriteText breadcrumbText;
        private readonly OsuSpriteText statusText;
        private readonly OsuSpriteText detailTitle;
        private readonly OsuSpriteText detailBody;
        private readonly FillFlowContainer sourceFlow;
        private readonly FillFlowContainer listFlow;
        private readonly Container sourcePanel;
        private readonly Container listPanel;
        private readonly Container detailPanel;

        public BmsSongSelectShell(BmsSongSelectNavigator navigator, BmsBarContext context)
        {
            this.navigator = navigator;
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
                        Colour = new Color4(10, 12, 18, 255),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 64,
                        Padding = new MarginPadding { Horizontal = 20, Vertical = 10 },
                        Children = new Drawable[]
                        {
                            breadcrumbText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            },
                            statusText = new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Font = OsuFont.Default.With(size: 12),
                                Colour = Colour4.Gray,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Font = OsuFont.Default.With(size: 11),
                                Colour = Colour4.Gray,
                                Text = "↑↓ 选择  ←→ 切换栏  Enter 打开/游玩  Esc 返回  1 键数  2 排序",
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 64, Horizontal = 12, Bottom = 12 },
                        Children = new Drawable[]
                        {
                            sourcePanel = createPane(source_width, Anchor.TopLeft, out sourceFlow),
                            listPanel = createPane(list_width, Anchor.TopLeft, out listFlow, x: source_width + 0.01f),
                            detailPanel = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Width = 1f - source_width - list_width - 0.02f,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Colour4.Black.Opacity(0.3f),
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Padding = new MarginPadding(14),
                                        Spacing = new osuTK.Vector2(0, 8),
                                        Children = new Drawable[]
                                        {
                                            detailTitle = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                                RelativeSizeAxes = Axes.X,
                                            },
                                            detailBody = new OsuSpriteText
                                            {
                                                Font = OsuFont.Default.With(size: 13),
                                                RelativeSizeAxes = Axes.X,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            navigator.Changed += refresh;
            refresh();
        }

        public void Rebind(BmsSongSelectNavigator newNavigator, BmsBarContext newContext)
        {
            navigator.Changed -= refresh;
            navigator = newNavigator;
            context = newContext;
            navigator.Changed += refresh;
            refresh();
        }

        private Container createPane(float width, Anchor anchor, out FillFlowContainer flow, float x = 0)
        {
            flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Width = width,
                X = x,
                RelativePositionAxes = Axes.X,
                Anchor = anchor,
                Origin = anchor,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.28f),
                    },
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(6),
                        Child = flow,
                    },
                },
            };
        }

        private void refresh()
        {
            breadcrumbText.Text = navigator.Breadcrumb;
            statusText.Text = $"KEY: {context.KeyModeFilter.Current} | 排序: {context.SortPolicy.Mode}";

            bool sourceFocused = navigator.FocusPane == BmsSongSelectFocusPane.Source;
            sourcePanel.BorderThickness = sourceFocused ? 2 : 0;
            sourcePanel.BorderColour = Colour4.SkyBlue;
            listPanel.BorderThickness = sourceFocused ? 0 : 2;
            listPanel.BorderColour = Colour4.SkyBlue;

            sourceFlow.Clear();

            for (int i = 0; i < navigator.SourceBars.Count; i++)
            {
                int index = i;
                BmsBar bar = navigator.SourceBars[i];
                bool selected = sourceFocused && index == navigator.SourceIndex;
                sourceFlow.Add(createRow(bar, selected, () =>
                {
                    navigator.SelectSourceIndex(index);
                    navigator.FocusSource();
                    navigator.ActivateSource();
                }));
            }

            listFlow.Clear();

            for (int i = 0; i < navigator.ListBars.Count; i++)
            {
                int index = i;
                BmsBar bar = navigator.ListBars[i];
                bool selected = !sourceFocused && index == navigator.ListIndex;
                listFlow.Add(createRow(bar, selected, () =>
                {
                    navigator.SelectListIndex(index);
                    navigator.FocusList();
                }, doubleActivate: () =>
                {
                    navigator.SelectListIndex(index);
                    navigator.FocusList();
                    navigator.ActivateList();
                    RequestPlay?.Invoke();
                }));
            }

            updateDetail(navigator.GetDetailBar());
        }

        public event Action? RequestPlay;

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
                BmsChartSummary c = song.Summary;
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

                string lampLine = $"Lamp: {context.LampStore.GetLamp(song.BeatmapId)}";

                detailBody.Text = string.Join("\n",
                    BmsStrings.Raja_DetailArtist(c.Artist),
                    BmsStrings.Raja_DetailLevel(c.PlayLevel, c.KeyCount, c.FileName),
                    BmsStrings.Raja_DetailBpm(c.Bpm, c.TotalNotes),
                    lampLine,
                    BmsStrings.Raja_DetailPath(c.FolderPath),
                    analyticsLine);
            }
            else if (bar is BmsMissingChartBar missing)
            {
                detailBody.Text = string.Join("\n",
                    $"表等级: {missing.TableLevel}",
                    BmsStrings.Raja_DetailArtist(missing.Entry.Artist),
                    $"MD5: {missing.Entry.Md5}",
                    $"SHA256: {missing.Entry.Sha256}",
                    "状态: 未导入（本地曲库无匹配）");
            }
            else if (bar is BmsTableBar table)
            {
                detailBody.Text = string.Join("\n",
                    $"等级数: {table.Table.Levels.Count}",
                    string.IsNullOrEmpty(table.Table.Tag) ? "-" : $"Tag: {table.Table.Tag}",
                    string.IsNullOrEmpty(table.Table.Url) ? "-" : table.Table.Url);
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

        private Drawable createRow(BmsBar bar, bool selected, Action onActivate, Action? doubleActivate = null)
        {
            if (bar is BmsSectionLabelBar section)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 18,
                    Padding = new MarginPadding { Top = 6, Bottom = 2 },
                    Child = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                        Colour = Colour4.Gray,
                        Text = section.Title,
                    },
                };
            }

            Colour4 textColour = Colour4.White;
            string lamp = "·";
            string level = string.Empty;
            string title = bar.Title;
            string artist = string.Empty;

            if (bar is BmsSongBar song)
            {
                level = song.Summary.PlayLevel.ToString();
                artist = song.Summary.Artist;
                title = string.IsNullOrWhiteSpace(song.Summary.Title) ? song.Summary.FileName : song.Summary.Title;
                lamp = lampGlyph(context.LampStore.GetLamp(song.BeatmapId));
            }
            else if (bar is BmsMissingChartBar missing)
            {
                textColour = Colour4.Gray;
                level = missing.TableLevel;
                artist = "未导入";
                title = missing.Title;
                lamp = "×";
            }
            else if (bar.IsDirectory)
            {
                title = "▶ " + title;
            }

            return new BmsDenseListRow(selected, lamp, level, title, artist, textColour, onActivate, doubleActivate)
            {
                Height = row_height,
            };
        }

        private static string lampGlyph(BmsClearLamp clearLamp) => clearLamp switch
        {
            BmsClearLamp.Max or BmsClearLamp.Perfect or BmsClearLamp.FullCombo => "◆",
            BmsClearLamp.ExHard or BmsClearLamp.Hard => "●",
            BmsClearLamp.Normal or BmsClearLamp.Easy => "○",
            BmsClearLamp.AssistEasy or BmsClearLamp.LightAssistEasy => "△",
            BmsClearLamp.Failed => "×",
            _ => "·",
        };

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    if (navigator.FocusPane == BmsSongSelectFocusPane.Source)
                        navigator.MoveSource(-1);
                    else
                        navigator.MoveList(-1);
                    return true;

                case Key.Down:
                    if (navigator.FocusPane == BmsSongSelectFocusPane.Source)
                        navigator.MoveSource(1);
                    else
                        navigator.MoveList(1);
                    return true;

                case Key.Left:
                    if (navigator.FocusPane == BmsSongSelectFocusPane.List)
                        navigator.FocusSource();
                    else
                        navigator.GoBack();
                    return true;

                case Key.Right:
                    if (navigator.FocusPane == BmsSongSelectFocusPane.Source)
                    {
                        if (navigator.GetSelectedSourceBar() is BmsDirectoryBar)
                            navigator.ActivateSource();
                        else
                            navigator.FocusList();
                    }

                    return true;

                case Key.Enter:
                    if (navigator.FocusPane == BmsSongSelectFocusPane.Source)
                    {
                        navigator.ActivateSource();
                    }
                    else
                    {
                        navigator.ActivateList();

                        if (navigator.GetSelectedSong() != null)
                            RequestPlay?.Invoke();
                    }

                    return true;

                case Key.Escape:
                    navigator.GoBack();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                navigator.Changed -= refresh;

            base.Dispose(isDisposing);
        }
    }

    public partial class BmsDenseListRow : CompositeDrawable
    {
        private readonly Action onActivate;
        private readonly Action? doubleActivate;
        private double lastClickTime;

        public BmsDenseListRow(
            bool selected,
            string lamp,
            string level,
            string title,
            string artist,
            Colour4 textColour,
            Action onActivate,
            Action? doubleActivate = null)
        {
            this.onActivate = onActivate;
            this.doubleActivate = doubleActivate;
            RelativeSizeAxes = Axes.X;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = selected ? Colour4.SkyBlue.Opacity(0.35f) : Colour4.Transparent,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 18),
                            new Dimension(GridSizeMode.Absolute, 48),
                            new Dimension(GridSizeMode.Distributed),
                            new Dimension(GridSizeMode.Relative, 0.32f),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 12),
                                    Colour = textColour,
                                    Text = lamp,
                                    Padding = new MarginPadding { Left = 4 },
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 12),
                                    Colour = textColour.Opacity(0.85f),
                                    Text = level,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 13),
                                    Colour = textColour,
                                    Text = title,
                                    Truncate = true,
                                    RelativeSizeAxes = Axes.X,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 12),
                                    Colour = textColour.Opacity(0.7f),
                                    Text = artist,
                                    Truncate = true,
                                    RelativeSizeAxes = Axes.X,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            double now = Clock.CurrentTime;

            if (doubleActivate != null && now - lastClickTime < 350)
            {
                doubleActivate.Invoke();
                lastClickTime = 0;
                return true;
            }

            lastClickTime = now;
            onActivate();
            return true;
        }
    }
}
