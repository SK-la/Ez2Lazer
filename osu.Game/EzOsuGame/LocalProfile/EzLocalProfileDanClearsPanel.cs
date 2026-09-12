// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Local Profile Track: full-width dan Clear list (RC|LN). Not part of HUD DualPanel.
    /// Each side uses the same collapsible header pattern as Ez mode key-stat rows.
    /// </summary>
    public partial class EzLocalProfileDanClearsPanel : CompositeDrawable
    {
        private const int clears_top_n = 15;

        private readonly string username;
        private readonly IBindable<int> keyCount;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow>? drillScores;
        private readonly Action<EzLocalProfileDrillScoreRow>? onSelectDrill;

        private FillFlowContainer sidesFlow = null!;
        private OsuSpriteText emptyHint = null!;
        private readonly LayoutValue sizeLayout = new LayoutValue(Invalidation.DrawSize);

        [Resolved]
        private EzSkillProvider skillProvider { get; set; } = null!;

        public EzLocalProfileDanClearsPanel(
            string username,
            BindableInt keyCount,
            IReadOnlyList<EzLocalProfileDrillScoreRow>? drillScores = null,
            Action<EzLocalProfileDrillScoreRow>? onSelectDrill = null)
        {
            this.username = username;
            this.keyCount = keyCount.GetBoundCopy();
            this.drillScores = drillScores;
            this.onSelectDrill = onSelectDrill;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            AddLayout(sizeLayout);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChildren = new Drawable[]
            {
                emptyHint = new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.GetFont(size: 13),
                    Colour = colours.Content2,
                    Text = EzSettingsProfile.LOCAL_PROFILE_DAN_CLEARS_EMPTY,
                    Alpha = 0,
                },
                sidesFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            keyCount.BindValueChanged(_ => refresh(), true);
        }

        protected override void Update()
        {
            base.Update();

            if (!sizeLayout.IsValid)
            {
                sizeLayout.Validate();
                reflowRowWidths();
            }
        }

        public void Refresh() => refresh();

        private void refresh()
        {
            if (LoadState < LoadState.Ready)
            {
                Schedule(refresh);
                return;
            }

            sidesFlow.Clear();

            int keys = keyCount.Value;

            if (keys <= 0 || string.IsNullOrWhiteSpace(username))
            {
                emptyHint.Show();
                sidesFlow.Hide();
                return;
            }

            var rc = skillProvider.GetDanClears(username, keys, EzDanSide.Rc.ToId(), EzDanAlgorithm.VERSION);
            var ln = skillProvider.GetDanClears(username, keys, EzDanSide.Ln.ToId(), EzDanAlgorithm.VERSION);

            if (rc.Count == 0 && ln.Count == 0)
            {
                emptyHint.Show();
                sidesFlow.Hide();
                return;
            }

            emptyHint.Hide();
            sidesFlow.Show();

            if (rc.Count > 0)
                sidesFlow.Add(buildSideSection(EzDanSide.Rc, EzHUDDanDualPanel.RC_ACCENT, rc));

            if (ln.Count > 0)
                sidesFlow.Add(buildSideSection(EzDanSide.Ln, EzHUDDanDualPanel.LN_ACCENT, ln));

            reflowRowWidths();
        }

        private Drawable buildSideSection(EzDanSide side, Colour4 accent, IReadOnlyList<EzDanClearEvidenceRow> clears)
        {
            string sideName = side == EzDanSide.Ln
                ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN.ToString()
                : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC.ToString();

            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(8),
            };

            int shown = Math.Min(clears.Count, clears_top_n);

            foreach (var clear in clears.Take(clears_top_n))
            {
                var drill = findDrill(clear.BeatmapHash);
                rows.Add(new EzEvidenceScoreRow(
                    resolveTitle(drill),
                    EzEvidenceScoreRow.FormatDanMeta(clear.CreditedDan, clear.Accuracy, clear.Rate, clear.ScoredAt),
                    drill != null && onSelectDrill != null ? () => onSelectDrill(drill) : null)
                {
                    Width = 160,
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.Y,
                });
            }

            return new CollapsibleClearSide(
                EzSettingsProfile.LOCAL_PROFILE_DAN_CLEARS_FOR.Format(sideName).ToString(),
                shown,
                accent,
                rows);
        }

        private void reflowRowWidths()
        {
            float width = DrawWidth;
            if (width <= 0)
                width = 400;

            int cols = width switch
            {
                < 280 => 1,
                < 420 => 2,
                < 560 => 3,
                _ => 4,
            };

            const float gap = 8f;
            float colWidth = Math.Max(80, (width - gap * (cols - 1)) / cols);

            foreach (var rows in sidesFlow.Children.OfType<CollapsibleClearSide>().Select(s => s.RowsFlow))
            {
                foreach (var child in rows)
                    child.Width = colWidth;
            }
        }

        private EzLocalProfileDrillScoreRow? findDrill(string beatmapHash)
        {
            if (string.IsNullOrEmpty(beatmapHash) || drillScores == null)
                return null;

            return drillScores
                   .Where(r => string.Equals(r.BeatmapHash, beatmapHash, StringComparison.Ordinal))
                   .OrderByDescending(r => r.PpResolved)
                   .ThenByDescending(r => r.Date)
                   .FirstOrDefault();
        }

        private static string resolveTitle(EzLocalProfileDrillScoreRow? drill)
        {
            if (drill == null)
                return EzSettingsProfile.LOCAL_PROFILE_AXIS_UNKNOWN_MAP.ToString();

            if (string.IsNullOrEmpty(drill.Artist))
            {
                return string.IsNullOrEmpty(drill.DifficultyName)
                    ? drill.Title
                    : $"{drill.Title} [{drill.DifficultyName}]";
            }

            return string.IsNullOrEmpty(drill.DifficultyName)
                ? $"{drill.Artist} - {drill.Title}"
                : $"{drill.Artist} - {drill.Title} [{drill.DifficultyName}]";
        }

        /// <summary>Same fold-header pattern as <see cref="EzLocalProfileExpandableRow"/> (Ez mode key stats).</summary>
        private partial class CollapsibleClearSide : CompositeDrawable
        {
            private readonly string title;
            private readonly int clearCount;
            private readonly Colour4 accent;
            private readonly FillFlowContainer rows;

            private Container detailFlow = null!;
            private SpriteIcon chevron = null!;
            private bool expanded;

            public FillFlowContainer RowsFlow => rows;

            public CollapsibleClearSide(string title, int clearCount, Colour4 accent, FillFlowContainer rows)
            {
                this.title = title;
                this.clearCount = clearCount;
                this.accent = accent;
                this.rows = rows;

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
                    Spacing = new Vector2(0, 8),
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
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Margin = new MarginPadding { Left = 22 },
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(10, 0),
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = title,
                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                                                Colour = accent,
                                            },
                                            new OsuSpriteText
                                            {
                                                Text = clearCount.ToString("N0"),
                                                Font = OsuFont.GetFont(size: 13),
                                                Colour = colours.Content2,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                        detailFlow = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0,
                            Padding = new MarginPadding { Left = 4, Right = 4, Bottom = 4 },
                            Child = rows,
                        },
                    },
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

                public HeaderButton(Action action)
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

                protected override bool OnHover(HoverEvent e)
                {
                    background.FadeColour(hoverColour, 200, Easing.OutQuint);
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    background.FadeColour(idleColour, 200, Easing.OutQuint);
                    base.OnHoverLost(e);
                }
            }
        }
    }
}
