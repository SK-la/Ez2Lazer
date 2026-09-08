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
    /// Track-mode skills: key chips, Overall header, radar, SSR bars, axis supporting plays, optional history.
    /// </summary>
    public partial class EzLocalProfileTrackSkillsBody : FillFlowContainer
    {
        private const int axis_plays_top_n = 15;

        private readonly string username;
        private readonly Bindable<EzLocalProfileDrillScoreRow?>? selectDrillScore;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow>? preloadedDrillScores;

        private readonly BindableInt selectedKeyCount = new BindableInt();
        private readonly Bindable<string?> selectedAxisSkillId = new Bindable<string?>();
        private readonly Bindable<string?> selectedHistorySkillId = new Bindable<string?>();

        private FillFlowContainer keyChipFlow = null!;
        private Container headerContainer = null!;
        private Container radarContainer = null!;
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
                keyChipFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(8),
                },
                emptyHint = new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.GetFont(size: 14),
                    Alpha = 0,
                },
                headerContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                radarContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 220,
                    Alpha = 0,
                },
                skillBarsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
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
            selectedAxisSkillId.BindValueChanged(_ => refreshDetailPanel(), false);
            selectedHistorySkillId.BindValueChanged(_ => refreshDetailPanel(), false);
            rebuild();
        }

        private void clearDetailSelection()
        {
            selectedAxisSkillId.Value = null;
            selectedHistorySkillId.Value = null;
        }

        private void toggleAxisPlays(string skillId)
        {
            if (string.Equals(selectedAxisSkillId.Value, skillId, StringComparison.Ordinal))
            {
                selectedAxisSkillId.Value = null;
                return;
            }

            selectedHistorySkillId.Value = null;
            selectedAxisSkillId.Value = skillId;
        }

        private void toggleHistory(string skillId)
        {
            if (string.Equals(selectedHistorySkillId.Value, skillId, StringComparison.Ordinal))
            {
                selectedHistorySkillId.Value = null;
                return;
            }

            selectedAxisSkillId.Value = null;
            selectedHistorySkillId.Value = skillId;
        }

        private void rebuild()
        {
            keyChipFlow.Clear();
            headerContainer.Clear();
            radarContainer.Clear();
            skillBarsFlow.Clear();
            detailContainer.Clear();
            clearDetailSelection();

            var keyCounts = skillProvider.GetPlayerSsrKeyCounts(username);

            if (keyCounts.Count == 0)
            {
                emptyHint.Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_EMPTY;
                emptyHint.Show();
                radarContainer.Hide();
                return;
            }

            emptyHint.Hide();

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
            headerContainer.Clear();
            radarContainer.Clear();
            skillBarsFlow.Clear();

            int keyCount = selectedKeyCount.Value;
            if (keyCount <= 0)
                return;

            var snapshot = skillProvider.GetPlayerSsrSnapshot(username, keyCount);
            var definitions = skillProvider.Registry.GetSystem(EzSkillSystems.PLAYER_SSR)?.Skills
                              ?? Array.Empty<EzSkillDefinition>();

            headerContainer.Child = new SkillsHeader(keyCount, snapshot, username, skillProvider);

            var radarAxes = definitions.Where(d => d.SkillId != EzSkillIds.Ssr(EzSkillIds.OVERALL)).ToList();
            double radarMax = radarAxes
                              .Select(d => snapshot.Values.GetValueOrDefault(d.SkillId, 0))
                              .DefaultIfEmpty(0)
                              .Max();

            if (radarAxes.Count >= 3 && radarMax > 0)
            {
                var ratios = radarAxes
                             .Select(d =>
                             {
                                 snapshot.Values.TryGetValue(d.SkillId, out double v);
                                 return (float)(v / radarMax);
                             })
                             .ToList();

                var chart = new RadarChart
                {
                    RelativeSizeAxes = Axes.Both,
                    AxisCount = ratios.Count,
                };

                radarContainer.Child = chart;
                chart.SetData(ratios);
                radarContainer.Show();
            }
            else
            {
                radarContainer.Hide();
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
                    def.DisplayName,
                    value,
                    ratio,
                    Colour4.FromHex(def.AccentHex),
                    () => toggleAxisPlays(skillId),
                    () => toggleHistory(skillId)));
            }

            refreshDetailPanel();
        }

        private void refreshDetailPanel()
        {
            detailContainer.Clear();

            int keyCount = selectedKeyCount.Value;
            if (keyCount <= 0)
                return;

            if (!string.IsNullOrEmpty(selectedAxisSkillId.Value))
            {
                showAxisPlays(selectedAxisSkillId.Value, keyCount);
                return;
            }

            if (!string.IsNullOrEmpty(selectedHistorySkillId.Value))
                showHistory(selectedHistorySkillId.Value, keyCount);
        }

        private void showAxisPlays(string skillId, int keyCount)
        {
            string displayName = resolveDisplayName(skillId);
            var plays = skillProvider
                        .GetAxisPlays(username, keyCount, skillId, EzManiaSkillAlgorithm.VERSION)
                        .Take(axis_plays_top_n)
                        .ToList();

            if (plays.Count == 0)
            {
                detailContainer.Child = new EzLocalProfileChartCard(
                    EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_FOR.Format(displayName),
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_EMPTY,
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

            detailContainer.Child = new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_AXIS_PLAYS_FOR.Format(displayName),
                list);
        }

        private void showHistory(string skillId, int keyCount)
        {
            var points = skillProvider.GetPlayerSkillHistory(username, keyCount, skillId);
            string displayName = resolveDisplayName(skillId);

            if (points.Count == 0)
            {
                detailContainer.Child = new EzLocalProfileChartCard(
                    EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY,
                    new OsuSpriteText
                    {
                        Text = EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_EMPTY,
                        Font = OsuFont.GetFont(size: 13),
                    });
                return;
            }

            float[] values = points.Select(p => (float)p.Value).ToArray();
            string[] labels = points.Select(p => p.RecordedAt.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture)).ToArray();

            detailContainer.Child = new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_FOR.Format(displayName),
                new EzLocalProfileLabeledLineChart(values, labels));
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

        private partial class SkillsHeader : FillFlowContainer
        {
            public SkillsHeader(int keyCount, EzPlayerSsrSnapshot snapshot, string username, EzSkillProvider skillProvider)
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

                addDanChip(danFlow, skillProvider.GetDan(username, keyCount, DanSkillSystem.SIDE_RC), DanSkillSystem.SIDE_RC);
                addDanChip(danFlow, skillProvider.GetDan(username, keyCount, DanSkillSystem.SIDE_LN), DanSkillSystem.SIDE_LN);

                if (danFlow.Children.Count > 0)
                    children.Add(danFlow);

                Children = children;
            }

            private static void addDanChip(FillFlowContainer flow, EzDanEstimate? estimate, string side)
            {
                if (estimate == null || string.IsNullOrEmpty(estimate.Label) || estimate.RawDan < 0)
                    return;

                flow.Add(new DanChip(side, estimate)
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                });
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
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

        private partial class DanChip : Container
        {
            private readonly string side;
            private readonly EzDanEstimate estimate;

            public DanChip(string side, EzDanEstimate estimate)
            {
                this.side = side;
                this.estimate = estimate;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
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
                    new Box
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
                                Text = EzSettingsProfile.LOCAL_PROFILE_DAN_HEURISTIC_HINT,
                                Font = OsuFont.GetFont(size: 10),
                                Colour = colours.Content2,
                            },
                        }
                    }
                };
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

        private partial class SkillBarRow : Container
        {
            private const float trend_width = 44f;

            public SkillBarRow(
                string displayName,
                double value,
                float ratio,
                Colour4 accent,
                Action onAxisClick,
                Action onTrendClick)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;

                Children = new Drawable[]
                {
                    new OsuClickableContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Right = trend_width },
                        Action = onAxisClick,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Horizontal = 4 },
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = displayName,
                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                    Width = 96,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Left = 104, Right = 56 },
                                    Child = new EzLocalProfileRoundedBar(ratio, accent),
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Text = value.ToString("0.00", CultureInfo.InvariantCulture),
                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                },
                            }
                        }
                    },
                    new OsuClickableContainer
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        RelativeSizeAxes = Axes.Y,
                        Width = trend_width,
                        Action = onTrendClick,
                        Child = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = EzSettingsProfile.LOCAL_PROFILE_AXIS_TREND,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                        }
                    }
                };
            }
        }
    }
}
