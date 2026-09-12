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
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Track-mode skills: key chips + rating beside radar; RC|LN DualPanel; SSR bars; skill detail.
    /// </summary>
    public partial class EzLocalProfileTrackSkillsBody : FillFlowContainer
    {
        private const int axis_plays_top_n = 15;

        private readonly string username;
        private readonly Bindable<EzLocalProfileDrillScoreRow?>? selectDrillScore;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow>? preloadedDrillScores;

        private readonly BindableInt selectedKeyCount = new BindableInt();
        private readonly Bindable<string?> selectedSkillId = new Bindable<string?>();

        private Container overviewContainer = null!;
        private FillFlowContainer keyChipFlow = null!;
        private Container headerSlot = null!;
        private Container radarSlot = null!;
        private EzHUDDanDualPanel danPanel = null!;
        private EzLocalProfileDanClearsPanel danClearsPanel = null!;
        private FillFlowContainer skillBarsFlow = null!;
        private Container detailContainer = null!;
        private OsuSpriteText emptyHint = null!;

        [Resolved]
        private EzSkillProvider skillProvider { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        public EzLocalProfileTrackSkillsBody(string username,
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
                                danPanel = new EzHUDDanDualPanel
                                {
                                    RelativeSizeAxes = Axes.X,
                                },
                                emptyHint = new OsuSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.GetFont(size: 14),
                                    Alpha = 0,
                                },
                            },
                        },
                        radarSlot = new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.58f,
                        },
                    },
                },
                danClearsPanel = new EzLocalProfileDanClearsPanel(
                    username,
                    selectedKeyCount,
                    preloadedDrillScores,
                    selectDrillScore != null
                        ? drill => selectDrillScore.Value = drill
                        : null)
                {
                    RelativeSizeAxes = Axes.X,
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

            // Detach shared SongSelect player selection before applying archive filter username.
            danPanel.TargetUsername.UnbindBindings();
            danPanel.TargetUsername.Value = username;
            danPanel.KeyCount.BindTo(selectedKeyCount);
            danPanel.DataSource.Value = EzDanPanelDataSource.Player;
            danPanel.DualLayout.Value = EzDanPanelDualLayout.Auto;
            danPanel.ShowClearCounts.Value = true;

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

            // Defer first build one frame so Track mount does not hitch the same frame as Insights/drill staging.
            Scheduler.AddDelayed(rebuild, 0);
        }

        private void clearDetailSelection()
        {
            selectedSkillId.Value = null;
        }

        private void toggleSkill(string skillId)
        {
            if (string.Equals(selectedSkillId.Value, skillId, StringComparison.Ordinal))
            {
                selectedSkillId.Value = null;
                return;
            }

            selectedSkillId.Value = skillId;
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
                danPanel.Hide();
                danClearsPanel.Hide();
                return;
            }

            emptyHint.Hide();
            overviewContainer.Show();
            danPanel.Show();
            danClearsPanel.Show();
            Scheduler.AddDelayed(() => danClearsPanel.Refresh(), 0);

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
            var modeEntries = skillProvider.GetSkillModeEntries(username, keyCount);

            headerSlot.Child = new SkillsHeader(keyCount, snapshot);

            double radarMax = modeEntries.Select(static e => e.Value).DefaultIfEmpty(0).Max();

            if (modeEntries.Count >= 3 && radarMax > 0)
            {
                var axes = modeEntries
                           .Select(e =>
                           {
                               var axis = EzMinaSkillAxisExtensions.TryParse(e.SkillId, out var parsed)
                                   ? parsed
                                   : EzMinaSkillAxis.Stream;
                               return new SkillsRadarPanel.AxisData(
                                   axis,
                                   e.Value,
                                   (float)(e.Value / radarMax),
                                   Colour4.FromHex(e.AccentHex),
                                   keyCount,
                                   EzDanSide.Rc,
                                   e.DisplayName);
                           })
                           .ToList();

                radarSlot.Child = new SkillsRadarPanel(axes)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }

            double barMax = modeEntries.Select(static e => e.Value).DefaultIfEmpty(0).Max();
            barMax = Math.Max(barMax, snapshot.Overall);
            if (barMax <= 0)
                barMax = 1;

            if (snapshot.Overall >= EzPatternRatings.DISPLAY_MIN)
            {
                var overallMeta = EzMinaSkillAxis.Overall.Meta();
                string overallSkillId = EzMinaSkillAxis.Overall.ToSsrSkillId();

                skillBarsFlow.Add(new SkillBarRow(
                    overallSkillId,
                    overallMeta.DisplayName,
                    snapshot.Overall,
                    (float)(snapshot.Overall / barMax),
                    Colour4.FromHex(overallMeta.AccentHex),
                    selectedSkillId,
                    () => toggleSkill(overallSkillId)));
            }

            foreach (var entry in modeEntries)
            {
                float ratio = (float)(entry.Value / barMax);
                string skillId = entry.SkillId;

                skillBarsFlow.Add(new SkillBarRow(
                    skillId,
                    entry.DisplayName,
                    entry.Value,
                    ratio,
                    Colour4.FromHex(entry.AccentHex),
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

            string cardTitle = EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_FOR.Format(displayName);

            if (points.Count < 2)
            {
                return new EzLocalProfileChartCard(
                    cardTitle,
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_EMPTY,
                        Font = OsuFont.GetFont(size: 13),
                    });
            }

            float[] values = points.Select(p => (float)p.Value).ToArray();
            string[] labels = formatHistoryLabels(points);

            return new EzLocalProfileChartCard(
                cardTitle,
                new EzLocalProfileTrendChart(values, labels, displayName));
        }

        private static string[] formatHistoryLabels(IReadOnlyList<EzPlayerSkillHistoryPoint> points)
        {
            var first = points[0].RecordedAt.ToLocalTime();
            var last = points[^1].RecordedAt.ToLocalTime();
            bool spanYears = last.Year != first.Year || (last - first).TotalDays > 300;
            string format = spanYears ? "yyyy-MM" : "MM-dd";

            return points
                   .Select(p => p.RecordedAt.ToLocalTime().ToString(format, CultureInfo.InvariantCulture))
                   .ToArray();
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
                string highlight = play.AxisValue.ToString("0.00", CultureInfo.InvariantCulture);

                if (drill != null)
                {
                    list.Add(new EzLocalProfileScoreNarrowCard(
                        drill,
                        EzLocalProfileDrillMods.Resolve(drill, rulesets),
                        selectDrillScore != null ? () => selectDrillScore.Value = drill : null,
                        highlightValue: highlight));
                }
                else
                {
                    list.Add(new EzEvidenceScoreRow(
                        formatPlayTitle(null),
                        EzEvidenceScoreRow.FormatAxisMeta(play.AxisValue, play.Accuracy, play.Rate, play.ScoredAt)));
                }
            }

            return new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_FOR.Format(displayName),
                list);
        }

        private string resolveDisplayName(string skillId)
        {
            foreach (string systemId in new[] { EzSkillSystems.PLAYER_PATTERN, EzSkillSystems.PLAYER_SSR })
            {
                foreach (var def in skillProvider.Registry.GetSystem(systemId)?.Skills
                                    ?? Array.Empty<EzSkillDefinition>())
                {
                    if (def.SkillId == skillId)
                        return def.DisplayName.ToString();
                }
            }

            if (EzPatternRatings.TryParseSkillId(skillId, out string patternId)
                && EzPlayerPatternAxisExtensions.TryParse(patternId, out var patternAxis))
            {
                return patternAxis.Meta().DisplayName.ToString();
            }

            return EzMinaSkillAxisExtensions.TryParse(skillId, out var axis)
                ? axis.Chip().Name.ToString()
                : skillId;
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

            public readonly record struct AxisData(
                EzMinaSkillAxis Axis,
                double Value,
                float Ratio,
                Colour4 Accent,
                int KeyCount,
                EzDanSide Side,
                LocalisableString Label);

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

                    labelLayer.Add(new RadarAxisIndicator(axes[i])
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

                // Yellow = chart (none on Local Profile); green = player skills.
                chart.SetData(Enumerable.Repeat(0f, ratios.Count).ToList());
                chart.SetSecondaryData(ratios);
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
        /// One axis metric: skill short name + numeric SSR. Skillset dans live on DualPanel only (no SrToRawDan badges).
        /// </summary>
        private partial class RadarAxisIndicator : FillFlowContainer
        {
            public RadarAxisIndicator(SkillsRadarPanel.AxisData data)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 2);

                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = data.Label,
                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                        Colour = data.Accent,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = data.Value.ToString("0.00", CultureInfo.InvariantCulture),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = data.Accent,
                    },
                };
            }
        }

        private partial class SkillsHeader : FillFlowContainer
        {
            public SkillsHeader(int keyCount, EzPlayerSsrSnapshot snapshot)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 4);

                Children = new Drawable[]
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
                LocalisableString displayName,
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
