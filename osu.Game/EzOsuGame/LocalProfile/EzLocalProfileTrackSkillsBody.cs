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
    /// Track-mode skills: key chips, Overall header, radar, SSR bars, optional history.
    /// </summary>
    public partial class EzLocalProfileTrackSkillsBody : FillFlowContainer
    {
        private readonly string username;

        private readonly BindableInt selectedKeyCount = new BindableInt();
        private readonly Bindable<string?> selectedHistorySkillId = new Bindable<string?>();

        private FillFlowContainer keyChipFlow = null!;
        private Container headerContainer = null!;
        private Container radarContainer = null!;
        private FillFlowContainer skillBarsFlow = null!;
        private Container historyContainer = null!;
        private OsuSpriteText emptyHint = null!;

        [Resolved]
        private EzSkillProvider skillProvider { get; set; } = null!;

        public EzLocalProfileTrackSkillsBody(string username)
        {
            this.username = username;

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
                historyContainer = new Container
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
                selectedHistorySkillId.Value = null;
                refreshSkills();
            }, false);
            selectedHistorySkillId.BindValueChanged(_ => refreshHistory(), false);
            rebuild();
        }

        private void rebuild()
        {
            keyChipFlow.Clear();
            headerContainer.Clear();
            radarContainer.Clear();
            skillBarsFlow.Clear();
            historyContainer.Clear();
            selectedHistorySkillId.Value = null;

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

            headerContainer.Child = new SkillsHeader(keyCount, snapshot);

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
                    () => selectedHistorySkillId.Value =
                        string.Equals(selectedHistorySkillId.Value, skillId, StringComparison.Ordinal) ? null : skillId));
            }

            refreshHistory();
        }

        private void refreshHistory()
        {
            historyContainer.Clear();

            string? skillId = selectedHistorySkillId.Value;
            int keyCount = selectedKeyCount.Value;

            if (string.IsNullOrEmpty(skillId) || keyCount <= 0)
                return;

            var points = skillProvider.GetPlayerSkillHistory(username, keyCount, skillId);
            string displayName = skillId;

            foreach (var def in skillProvider.Registry.GetSystem(EzSkillSystems.PLAYER_SSR)?.Skills
                                ?? Array.Empty<EzSkillDefinition>())
            {
                if (def.SkillId == skillId)
                {
                    displayName = def.DisplayName;
                    break;
                }
            }

            if (points.Count == 0)
            {
                historyContainer.Child = new EzLocalProfileChartCard(
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

            historyContainer.Child = new EzLocalProfileChartCard(
                EzSettingsProfile.LOCAL_PROFILE_SKILL_HISTORY_FOR.Format(displayName),
                new EzLocalProfileLabeledLineChart(values, labels));
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
            public SkillBarRow(string displayName, double value, float ratio, Colour4 accent, Action action)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;
                Action = action;

                Children = new Drawable[]
                {
                    new Container
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
                };
            }
        }
    }
}
