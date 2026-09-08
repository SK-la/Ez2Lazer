// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
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
    /// Track-mode skills: key chips + rating + dan (left) beside radar with axis
    /// indicators (right); SSR bars below; selecting a skill shows trend + plays.
    /// </summary>
    public partial class EzLocalProfileTrackSkillsBody : FillFlowContainer
    {
        private const int axis_plays_top_n = 15;
        private const int dan_clears_top_n = 15;

        private readonly string username;
        private readonly Bindable<EzLocalProfileDrillScoreRow?>? selectDrillScore;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow>? preloadedDrillScores;

        private readonly BindableInt selectedKeyCount = new BindableInt();
        private readonly Bindable<string?> selectedSkillId = new Bindable<string?>();
        private readonly Bindable<string?> selectedDanSide = new Bindable<string?>();

        private Container overviewContainer = null!;
        private FillFlowContainer keyChipFlow = null!;
        private Container headerSlot = null!;
        private Container radarSlot = null!;
        private FillFlowContainer skillBarsFlow = null!;
        private Container detailContainer = null!;
        private OsuSpriteText emptyHint = null!;

        [Resolved]
        private EzSkillProvider skillProvider { get; set; } = null!;

        public EzLocalProfileTrackSkillsBody(
            string username,
            Bindable<EzLocalProfileDrillScoreRow?>? selectDrillScore = null,
            IReadOnlyList<EzLocalProfileDrillScoreRow>? preloadedDrillScores = null)
        {
            this.username = username;
            this.selectDrillScore = selectDrillScore;
            this.preloadedDrillScores = preloadedDrillScores;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 14);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                overviewContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = SkillsRadarPanel.RequiredHeight,
                    Children = new Drawable[]
                    {
                        // Left: key chips + rating + dan — no background; sibling of radar.
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            RelativeSizeAxes = Axes.X,
                            Width = 0.4f,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 12),
                            Children = new Drawable[]
                            {
                                keyChipFlow = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Full,
                                    Spacing = new Vector2(8),
                                },
                                headerSlot = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                },
                            },
                        },
                        radarSlot = new Container
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.58f,
                        },
                    },
                },
                emptyHint = new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.GetFont(size: 14),
                    Alpha = 0,
                },
                skillBarsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                },
                detailContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedKeyCount.BindValueChanged(_ =>
            {
                clearDetailSelection();
                refreshSkills();
            }, false);
            selectedSkillId.BindValueChanged(_ =>
            {
                refreshSkillBarSelection();
                refreshDetailPanel();
            }, false);
            selectedDanSide.BindValueChanged(_ => refreshDetailPanel(), false);
            rebuild();
        }

        private void clearDetailSelection()
        {
            selectedSkillId.Value = null;
            selectedDanSide.Value = null;
        }

        private void toggleSkill(string skillId)
        {
            if (string.Equals(selectedSkillId.Value, skillId, StringComparison.Ordinal))
            {
                selectedSkillId.Value = null;
                return;
            }

            selectedDanSide.Value = null;
            selectedSkillId.Value = skillId;
        }

        private void toggleDanClears(string side)
        {
            if (string.Equals(selectedDanSide.Value, side, StringComparison.Ordinal))
            {
                selectedDanSide.Value = null;
                return;
            }

            selectedSkillId.Value = null;
            selectedDanSide.Value = side;
        }

        private void rebuild()
        {
            keyChipFlow.Clear();
            headerSlot.Clear();
            radarSlot.Clear();
            skillBarsFlow.Clear();
            detailContainer.Clear();
            clearDetailSelection();

            var keyCounts = skillProvider.GetPlayerSsrKeyCounts(username);

            if (keyCounts.Count == 0)
            {
                emptyHint.Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_EMPTY;
                emptyHint.Show();
                overviewContainer.Hide();
                return;
            }

            emptyHint.Hide();
            overviewContainer.Show();

            foreach (int key in keyCounts)
            {
                keyChipFlow.Add(new KeyChip(key, selectedKeyCount)
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                });
            }

            if (!keyCounts.Contains(selectedKeyCount.Value))
                selectedKeyCount.Value = keyCounts[0];
            else
                refreshSkills();
        }

        private void refreshSkills()
        {
            headerSlot.Clear();
            radarSlot.Clear();
            skillBarsFlow.Clear();

            int keyCount = selectedKeyCount.Value;
            if (keyCount <= 0)
                return;

            var snapshot = skillProvider.GetPlayerSsrSnapshot(username, keyCount);
            var definitions = skillProvider.Registry.GetSystem(EzSkillSystems.PLAYER_SSR)?.Skills
                              ?? Array.Empty<EzSkillDefinition>();

            headerSlot.Child = new SkillsHeader(keyCount, snapshot, username, skillProvider, toggleDanClears, selectedDanSide);

            var radarAxes = definitions.Where(d => d.SkillId != EzSkillIds.Ssr(EzSkillIds.OVERALL)).ToList();
            double radarMax = radarAxes
                              .Select(d => snapshot.Values.GetValueOrDefault(d.SkillId, 0))
                              .DefaultIfEmpty(0)
                              .Max();

            if (radarAxes.Count >= 3 && radarMax > 0)
            {
                var axes = radarAxes
                           .Select(d =>
                           {
                               snapshot.Values.TryGetValue(d.SkillId, out double v);
                               return new SkillsRadarPanel.AxisData(
                                   v,
                                   (float)(v / radarMax),
                                   Colour4.FromHex(d.AccentHex));
                           })
                           .ToList();

                radarSlot.Child = new SkillsRadarPanel(axes)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }

            double barMax = snapshot.Values.Values.DefaultIfEmpty(0).Max();
            if (barMax <= 0)
                barMax = 1;

            foreach (var def in definitions)
            {
                snapshot.Values.TryGetValue(def.SkillId, out double value);
                float ratio = (float)(value / barMax);
                string skillId = def.SkillId;

                skillBarsFlow.Add(new SkillBarRow(
                    skillId,
                    def.DisplayName,
                    value,
                    ratio,
                    Colour4.FromHex(def.AccentHex),
                    selectedSkillId,
                    () => toggleSkill(skillId)));
            }

            refreshDetailPanel();
        }

        private void refreshSkillBarSelection()
        {
            foreach (var row in skillBarsFlow.OfType<SkillBarRow>())
                row.RefreshSelection();
        }

        private void refreshDetailPanel()
        {
            detailContainer.Clear();

            int keyCount = selectedKeyCount.Value;
            if (keyCount <= 0)
                return;

            if (!string.IsNullOrEmpty(selectedDanSide.Value))
            {
                showDanClears(selectedDanSide.Value, keyCount);
                return;
            }

            string? skillId = selectedSkillId.Value;
            if (string.IsNullOrEmpty(skillId))
                return;

            string displayName = resolveDisplayName(skillId);

            detailContainer.Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new[]
                {
                    buildHistoryCard(skillId, keyCount, displayName),
                    buildAxisPlaysCard(skillId, keyCount, displayName),
                },
            };
        }

        private Drawable buildHistoryCard(string skillId, int keyCount, string displayName)
        {
            var points = skillProvider.GetPlayerSkillHistory(username, keyCount, skillId);

            if (points.Count == 0)
            {
                return new EzLocalProfileChartCard(
                    EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_FOR.Format(displayName),
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_EMPTY,
                        Font = OsuFont.GetFont(size: 13),
                    });
            }

            float[] values = points.Select(p => (float)p.Value).ToArray();
            string[] labels = points.Select(p => p.RecordedAt.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture)).ToArray();

            return new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_FOR.Format(displayName),
                new EzLocalProfileLabeledLineChart(values, labels));
        }

        private Drawable buildAxisPlaysCard(string skillId, int keyCount, string displayName)
        {
            var plays = skillProvider
                        .GetAxisPlays(username, keyCount, skillId, EzManiaSkillAlgorithm.VERSION)
                        .Take(axis_plays_top_n)
                        .ToList();

            if (plays.Count == 0)
            {
                return new EzLocalProfileChartCard(
                    EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_FOR.Format(displayName),
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_EMPTY,
                        Font = OsuFont.GetFont(size: 13),
                    });
            }

            var list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            foreach (var play in plays)
            {
                var drill = findDrillRow(play.BeatmapHash);
                string title = formatPlayTitle(drill);

                list.Add(new EzEvidenceScoreRow(
                    title,
                    EzEvidenceScoreRow.FormatAxisMeta(play.AxisValue, play.Accuracy, play.Rate, play.ScoredAt),
                    drill != null && selectDrillScore != null
                        ? () => selectDrillScore.Value = drill
                        : null));
            }

            return new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_FOR.Format(displayName),
                list);
        }

        private void showDanClears(string side, int keyCount)
        {
            string sideLabel = side == DanSkillSystem.SIDE_LN
                ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN.ToString()
                : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC.ToString();

            var clears = skillProvider
                         .GetDanClears(username, keyCount, side, EzDanAlgorithm.VERSION)
                         .Take(dan_clears_top_n)
                         .ToList();

            if (clears.Count == 0)
            {
                detailContainer.Child = new EzLocalProfileChartCard(
                    EzSettingsProfile.LOCAL_PROFILE_DAN_CLEARS_FOR.Format(sideLabel),
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_DAN_CLEARS_EMPTY,
                        Font = OsuFont.GetFont(size: 13),
                    });
                return;
            }

            var list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            foreach (var clear in clears)
            {
                var drill = findDrillRow(clear.BeatmapHash);
                string title = formatPlayTitle(drill);

                list.Add(new EzEvidenceScoreRow(
                    title,
                    EzEvidenceScoreRow.FormatDanMeta(clear.CreditedDan, clear.Accuracy, clear.Rate, clear.ScoredAt),
                    drill != null && selectDrillScore != null
                        ? () => selectDrillScore.Value = drill
                        : null));
            }

            detailContainer.Child = new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_DAN_CLEARS_FOR.Format(sideLabel),
                list);
        }

        private string resolveDisplayName(string skillId)
        {
            foreach (var def in skillProvider.Registry.GetSystem(EzSkillSystems.PLAYER_SSR)?.Skills
                                ?? Array.Empty<EzSkillDefinition>())
            {
                if (def.SkillId == skillId)
                    return def.DisplayName;
            }

            return skillId;
        }

        private EzLocalProfileDrillScoreRow? findDrillRow(string beatmapHash)
        {
            if (string.IsNullOrEmpty(beatmapHash) || preloadedDrillScores == null)
                return null;

            return preloadedDrillScores
                   .Where(r => string.Equals(r.BeatmapHash, beatmapHash, StringComparison.Ordinal))
                   .OrderByDescending(r => r.PpResolved)
                   .ThenByDescending(r => r.Date)
                   .FirstOrDefault();
        }

        private static string formatPlayTitle(EzLocalProfileDrillScoreRow? drill)
        {
            if (drill == null)
                return EzSettingsProfile.LOCAL_PROFILE_AXIS_UNKNOWN_MAP.ToString();

            if (string.IsNullOrEmpty(drill.Artist))
                return string.IsNullOrEmpty(drill.DifficultyName) ? drill.Title : $"{drill.Title} [{drill.DifficultyName}]";

            return string.IsNullOrEmpty(drill.DifficultyName)
                ? $"{drill.Artist} - {drill.Title}"
                : $"{drill.Artist} - {drill.Title} [{drill.DifficultyName}]";
        }

        /// <summary>
        /// Radar chart with per-axis value indicators outside the polygon
        /// (dan badge slot above, numeric value below).
        /// </summary>
        private partial class SkillsRadarPanel : Container
        {
            public const float CHART_SIZE = 168f;
            private const float label_edge_gap = 10f;
            private const float panel_padding = 56f;

            public static float RequiredHeight => CHART_SIZE + panel_padding * 2;

            public readonly record struct AxisData(double Value, float Ratio, Colour4 Accent);

            public SkillsRadarPanel(IReadOnlyList<AxisData> axes)
            {
                Size = new Vector2(RequiredHeight);

                var ratios = axes.Select(a => a.Ratio).ToList();

                var chart = new RadarChart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(CHART_SIZE),
                    AxisCount = ratios.Count,
                };

                var labelLayer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = Size,
                };

                float chartRadius = CHART_SIZE * 0.5f * chart.RadiusRatio;
                int axisCount = Math.Max(3, ratios.Count);

                for (int i = 0; i < axes.Count; i++)
                {
                    float angle = MathF.PI * 2f / axisCount * i - MathF.PI / 2f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 position = direction * (chartRadius + label_edge_gap);

                    labelLayer.Add(new RadarAxisIndicator(axes[i].Value, axes[i].Accent)
                    {
                        Anchor = Anchor.Centre,
                        Origin = originFacingChart(direction),
                        Position = position,
                    });
                }

                Children = new Drawable[]
                {
                    chart,
                    labelLayer,
                };

                chart.SetData(ratios);
            }

            /// <summary>
            /// Origin on the edge toward the chart centre so the indicator grows outward.
            /// </summary>
            private static Anchor originFacingChart(Vector2 direction)
            {
                float ax = MathF.Abs(direction.X);
                float ay = MathF.Abs(direction.Y);

                if (ay >= ax * 1.15f)
                    return direction.Y < 0 ? Anchor.BottomCentre : Anchor.TopCentre;

                if (ax >= ay * 1.15f)
                    return direction.X < 0 ? Anchor.CentreRight : Anchor.CentreLeft;

                bool top = direction.Y < 0;
                bool left = direction.X < 0;

                if (top && left) return Anchor.BottomRight;
                if (top) return Anchor.BottomLeft;
                if (left) return Anchor.TopRight;

                return Anchor.TopLeft;
            }
        }

        /// <summary>
        /// One axis metric: reserved dan/rank badge slot on top, value below.
        /// </summary>
        private partial class RadarAxisIndicator : FillFlowContainer
        {
            private const float dan_slot_size = 22f;

            public RadarAxisIndicator(double value, Colour4 accent)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 2);

                Children = new Drawable[]
                {
                    // Reserved for future per-axis dan / rank badge.
                    new Container
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Width = dan_slot_size,
                        Height = dan_slot_size,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = value.ToString("0.00", CultureInfo.InvariantCulture),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = accent,
                    },
                };
            }
        }

        private partial class SkillsHeader : FillFlowContainer
        {
            public SkillsHeader(
                int keyCount,
                EzPlayerSsrSnapshot snapshot,
                string username,
                EzSkillProvider skillProvider,
                Action<string> onDanSideClick,
                IBindable<string?> selectedDanSide)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 4);

                var children = new List<Drawable>
                {
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_SKILL_RATING.Format(keyCount),
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                    },
                    new OsuSpriteText
                    {
                        Text = snapshot.Overall.ToString("0.00", CultureInfo.InvariantCulture),
                        Font = OsuFont.GetFont(size: 36, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText
                    {
                        Text = buildMeta(snapshot),
                        Font = OsuFont.GetFont(size: 12),
                    },
                };

                var danFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(8),
                    Margin = new MarginPadding { Top = 4 },
                };

                addDanChip(danFlow, skillProvider.GetDan(username, keyCount, DanSkillSystem.SIDE_RC), DanSkillSystem.SIDE_RC, onDanSideClick, selectedDanSide);
                addDanChip(danFlow, skillProvider.GetDan(username, keyCount, DanSkillSystem.SIDE_LN), DanSkillSystem.SIDE_LN, onDanSideClick, selectedDanSide);

                if (danFlow.Children.Count > 0)
                    children.Add(danFlow);

                Children = children;
            }

            private static void addDanChip(
                FillFlowContainer flow,
                EzDanEstimate? estimate,
                string side,
                Action<string> onDanSideClick,
                IBindable<string?> selectedDanSide)
            {
                if (estimate == null || string.IsNullOrEmpty(estimate.Label) || estimate.RawDan < 0)
                    return;

                flow.Add(new DanChip(side, estimate, () => onDanSideClick(side), selectedDanSide)
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                });
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                if (Children.Count >= 2)
                    Children[1].Colour = EzLocalProfileColours.Numeric(colours);
                if (Children.Count >= 3)
                    Children[2].Colour = colours.Content2;
            }

            private static string buildMeta(EzPlayerSsrSnapshot snapshot)
            {
                var parts = new List<string>
                {
                    EzSettingsProfile.LOCAL_PROFILE_SKILL_PLAYS.Format(snapshot.AnalyzedPlays),
                };

                if (snapshot.IsEffectivelyProvisional)
                    parts.Add(EzSettingsProfile.LOCAL_PROFILE_SKILL_PROVISIONAL.ToString());

                if (snapshot.Stale)
                    parts.Add(EzSettingsProfile.LOCAL_PROFILE_SKILL_STALE.ToString());

                return string.Join(" · ", parts);
            }
        }

        private partial class DanChip : OsuClickableContainer
        {
            private readonly string side;
            private readonly EzDanEstimate estimate;
            private readonly IBindable<string?> selectedDanSide;
            private Box background = null!;

            public DanChip(string side, EzDanEstimate estimate, Action action, IBindable<string?> selectedDanSide)
            {
                this.side = side;
                this.estimate = estimate;
                this.selectedDanSide = selectedDanSide.GetBoundCopy();

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
                Action = action;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                string sideLabel = side == DanSkillSystem.SIDE_LN
                    ? EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_LN.ToString()
                    : EzSettingsProfile.LOCAL_PROFILE_DAN_SIDE_RC.ToString();

                string body = $"{sideLabel} {estimate.Label}";
                if (estimate.Clears > 0)
                    body += $" · {estimate.Clears}";

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = EzSettingsProfile.LOCAL_PROFILE_DAN_CHIP_TITLE,
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                                Colour = colours.Content2,
                            },
                            new OsuSpriteText
                            {
                                Text = body,
                                Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                            },
                            new OsuSpriteText
                            {
                                Text = EzSettingsProfile.LOCAL_PROFILE_DAN_CHIP_HINT,
                                Font = OsuFont.GetFont(size: 10),
                                Colour = colours.Content2,
                            },
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                selectedDanSide.BindValueChanged(_ => updateVisual(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateVisual();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateVisual();
                base.OnHoverLost(e);
            }

            private void updateVisual()
            {
                bool active = string.Equals(selectedDanSide.Value, side, StringComparison.Ordinal);
                background.FadeTo(active || IsHovered ? 1f : 0.7f, 80);
            }
        }

        private partial class KeyChip : OsuClickableContainer
        {
            private readonly int keyCount;
            private readonly BindableInt selected;
            private Box background = null!;
            private OsuSpriteText label = null!;

            public KeyChip(int keyCount, BindableInt selected)
            {
                this.keyCount = keyCount;
                this.selected = selected;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5,
                    },
                    new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Child = label = new OsuSpriteText
                        {
                            Text = $"{keyCount}K",
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                        }
                    }
                };

                Action = () => selected.Value = keyCount;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                selected.BindValueChanged(_ => updateVisual(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateVisual();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateVisual();
                base.OnHoverLost(e);
            }

            private void updateVisual()
            {
                bool active = selected.Value == keyCount;
                label.Font = label.Font.With(weight: active ? FontWeight.Bold : FontWeight.SemiBold);
                background.FadeTo(active || IsHovered ? 1f : 0.7f, 80);
            }
        }

        private partial class SkillBarRow : OsuClickableContainer
        {
            private const float name_width = 96f;
            private const float dan_icon_slot_width = 28f;
            private const float value_width = 48f;

            private readonly string skillId;
            private readonly IBindable<string?> selectedSkillId;
            private readonly OsuSpriteText nameText;
            private readonly OsuSpriteText valueText;

            private readonly EzLocalProfileHoverBox background;

            public SkillBarRow(
                string skillId,
                string displayName,
                double value,
                float ratio,
                Colour4 accent,
                IBindable<string?> selectedSkillId,
                Action onClick)
            {
                this.skillId = skillId;
                this.selectedSkillId = selectedSkillId.GetBoundCopy();

                RelativeSizeAxes = Axes.X;
                Height = 28;
                Masking = true;
                CornerRadius = 6;
                Action = onClick;

                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = displayName,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    Width = name_width,
                };
                valueText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = value.ToString("0.00", CultureInfo.InvariantCulture),
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    Width = value_width,
                };

                float leftChrome = name_width + dan_icon_slot_width;

                // name | dan-icon slot | value | bar growing left from the right edge
                Children = new Drawable[]
                {
                    background = new EzLocalProfileHoverBox(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 8 },
                        Children = new Drawable[]
                        {
                            nameText,
                            new Container
                            {
                                // Reserved for future dan / rank icon.
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = name_width,
                                Width = dan_icon_slot_width,
                                RelativeSizeAxes = Axes.Y,
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Left = leftChrome },
                                Children = new Drawable[]
                                {
                                    valueText,
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Padding = new MarginPadding { Left = value_width + 8 },
                                        Child = new EzLocalProfileRoundedBar(ratio, accent, growFromRight: true),
                                    },
                                }
                            },
                        }
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                background.Configure(colours, transparentIdle: true);
                nameText.Colour = EzLocalProfileColours.Plays(colours);
                // Skill SSR uses Highlight1 — never PinkLight (reserved for PP).
                valueText.Colour = EzLocalProfileColours.Numeric(colours);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                selectedSkillId.BindValueChanged(_ => RefreshSelection(), true);
            }

            public void RefreshSelection()
            {
                background.SetSelected(string.Equals(selectedSkillId.Value, skillId, StringComparison.Ordinal));
                background.Refresh(IsHovered);
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.Refresh(true);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.Refresh(false);
                base.OnHoverLost(e);
            }
        }
    }
}
