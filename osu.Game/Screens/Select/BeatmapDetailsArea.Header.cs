// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Screens.Play.Leaderboards;
using osuTK;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDetailsArea
    {
        public partial class Header : CompositeDrawable
        {
            private WedgeSelector<Selection> tabControl = null!;
            private FillFlowContainer leaderboardControls = null!;
            private FillFlowContainer ezAnalysisControls = null!;

            private ShearedDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
            private ShearedDropdown<LeaderboardSortMode> sortDropdown = null!;
            private ShearedToggleButton selectedModsToggle = null!;

            private ShearedDropdown<string> ezPlayerDropdown = null!;
            private ShearedDropdown<EzRadarDisplayMode> leftRadarDropdown = null!;
            private ShearedDropdown<EzRadarDisplayMode> rightRadarDropdown = null!;

            private Bindable<bool> flowMode = null!;

            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            public IBindable<Selection> Type => tabControl.Current;

            public IBindable<BeatmapLeaderboardScope> Scope => scopeDropdown.Current;

            private readonly Bindable<BeatmapDetailTab> configDetailTab = new Bindable<BeatmapDetailTab>();

            public IBindable<LeaderboardSortMode> Sorting => sortDropdown.Current;

            private readonly Bindable<LeaderboardSortMode> configLeaderboardSortMode = new Bindable<LeaderboardSortMode>();

            public IBindable<bool> FilterBySelectedMods => selectedModsToggle.Active;

            public Bindable<string?> EzAnalysisPlayer { get; } = new Bindable<string?>();

            public Bindable<EzRadarDisplayMode> LeftRadarMode { get; } = new Bindable<EzRadarDisplayMode>(EzRadarDisplayMode.XxySrPattern);

            public Bindable<EzRadarDisplayMode> RightRadarMode { get; } = new Bindable<EzRadarDisplayMode>(EzRadarDisplayMode.Skill);

            [Resolved]
            private EzLocalProfileService? localProfileService { get; set; }

            [Resolved]
            private EzSkillProvider? skillProvider { get; set; }

            [Resolved]
            private EzAnalysisPlayerSelection? ezAnalysisPlayerSelection { get; set; }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, Ez2ConfigManager ezConfig)
            {
                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 5f },
                        Children = new Drawable[]
                        {
                            tabControl = new WedgeSelector<Selection>(20f)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 22,
                                Margin = new MarginPadding { Top = 2f },
                                IsSwitchable = true,
                            },
                            leaderboardControls = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Spacing = new Vector2(5f, 0f),
                                Direction = FillDirection.Horizontal,
                                Padding = new MarginPadding { Left = 258 },
                                Children = new Drawable[]
                                {
                                    selectedModsToggle = new ShearedToggleButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        AutoSizeAxes = Axes.X,
                                        Text = UserInterfaceStrings.SelectedMods,
                                        Height = 30f,
                                        Margin = new MarginPadding { Left = -9.2f },
                                    },
                                    sortDropdown = new ShearedDropdown<LeaderboardSortMode>(BeatmapLeaderboardWedgeStrings.Sort)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.4f,
                                        Items = Enum.GetValues<LeaderboardSortMode>(),
                                    },
                                    scopeDropdown = new ScopeDropdown
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.4f,
                                        Current = { Value = BeatmapLeaderboardScope.Global },
                                    },
                                },
                            },
                            ezAnalysisControls = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Spacing = new Vector2(5f, 0f),
                                Direction = FillDirection.Horizontal,
                                Padding = new MarginPadding { Left = 258 },
                                Alpha = 0,
                                Children = new Drawable[]
                                {
                                    rightRadarDropdown = new RadarModeDropdown(EzSongSelectStrings.EZ_ANALYSIS_RADAR_RIGHT)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.32f,
                                    },
                                    leftRadarDropdown = new RadarModeDropdown(EzSongSelectStrings.EZ_ANALYSIS_RADAR_LEFT)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.32f,
                                    },
                                    ezPlayerDropdown = new ShearedDropdown<string>(EzSongSelectStrings.EZ_ANALYSIS_PLAYER)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.32f,
                                    },
                                },
                            },
                        },
                    },
                };

                config.BindWith(OsuSetting.BeatmapDetailTab, configDetailTab);
                config.BindWith(OsuSetting.BeatmapLeaderboardSortMode, configLeaderboardSortMode);
                config.BindWith(OsuSetting.BeatmapDetailModsFilter, selectedModsToggle.Active);

                flowMode = ezConfig.GetBindable<bool>(Ez2Setting.FlowMode);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                scopeDropdown.Current.Value = tryMapDetailTabToLeaderboardScope(configDetailTab.Value) ?? scopeDropdown.Current.Value;
                scopeDropdown.Current.BindValueChanged(_ => updateConfigDetailTab());

                tabControl.Current.Value = configDetailTab.Value == BeatmapDetailTab.Details ? Selection.Details : Selection.Ranking;

                tabControl.IsItemActivatable = type => !flowMode.Value || !EqualityComparer<Selection>.Default.Equals(type, Selection.Ranking);

                leftRadarDropdown.Current.BindTo(LeftRadarMode);
                rightRadarDropdown.Current.BindTo(RightRadarMode);

                if (ezAnalysisPlayerSelection != null)
                    EzAnalysisPlayer.BindTo(ezAnalysisPlayerSelection.Current);

                refreshPlayerDropdownItems();
                ezPlayerDropdown.Current.BindValueChanged(e => EzAnalysisPlayer.Value = string.IsNullOrWhiteSpace(e.NewValue) ? null : e.NewValue);
                EzAnalysisPlayer.BindValueChanged(e =>
                {
                    if (e.NewValue != null && ezPlayerDropdown.Items.Contains(e.NewValue))
                        ezPlayerDropdown.Current.Value = e.NewValue;
                });

                tabControl.Current.BindValueChanged(v =>
                {
                    leaderboardControls.FadeTo(v.NewValue == Selection.Ranking ? 1 : 0, 300, Easing.OutQuint);
                    ezAnalysisControls.FadeTo(v.NewValue == Selection.EzAnalysis ? 1 : 0, 300, Easing.OutQuint);

                    if (v.NewValue == Selection.EzAnalysis)
                        refreshPlayerDropdownItems();

                    updateConfigDetailTab();
                }, true);

                flowMode.BindValueChanged(e => applyFlowModeState(e.NewValue), true);

                ruleset.BindValueChanged(e =>
                {
                    if (EzBeatmapPreviewModes.IsManiaRuleset(e.NewValue))
                        tabControl.Current.Value = Selection.EzAnalysis;
                }, true);

                scopeDropdown.Current.BindValueChanged(scope =>
                {
                    sortDropdown.Current.Disabled = false;

                    if (scope.NewValue == BeatmapLeaderboardScope.Local)
                    {
                        sortDropdown.Current.BindTo(configLeaderboardSortMode);
                    }
                    else
                    {
                        sortDropdown.Current.UnbindFrom(configLeaderboardSortMode);
                        sortDropdown.Current.Value = LeaderboardSortMode.Score;
                        sortDropdown.Current.Disabled = true;
                    }
                }, true);
            }

            private void refreshPlayerDropdownItems()
            {
                var names = new List<string>();

                var includedWithSkills = localProfileService?.GetPreviouslyIncludedUsernames()
                                                            .Where(playerHasSkillData)
                                                            .ToList()
                                         ?? new List<string>();

                // Offer All whenever any archive player has SSR (All headlines may still be empty until recompute).
                if (playerHasSkillData(EzLocalProfileConstants.ALL_PLAYERS) || includedWithSkills.Count > 0)
                    names.Add(EzLocalProfileConstants.ALL_PLAYERS);

                names.AddRange(includedWithSkills);

                ezPlayerDropdown.Items = names;

                if (names.Count == 0)
                {
                    EzAnalysisPlayer.Value = null;
                    return;
                }

                string? current = EzAnalysisPlayer.Value == null
                    ? null
                    : EzLocalProfileConstants.IsAllPlayersFilter(EzAnalysisPlayer.Value)
                        ? EzLocalProfileConstants.ALL_PLAYERS
                        : EzLocalProfileConstants.NormaliseUsername(EzAnalysisPlayer.Value);

                if (current == null || !names.Contains(current))
                {
                    string preferred = names.Contains(EzLocalProfileConstants.ALL_PLAYERS)
                        ? EzLocalProfileConstants.ALL_PLAYERS
                        : names[0];
                    EzAnalysisPlayer.Value = preferred;
                    ezPlayerDropdown.Current.Value = preferred;
                }
                else
                {
                    EzAnalysisPlayer.Value = current;
                    ezPlayerDropdown.Current.Value = current;
                }
            }

            private bool playerHasSkillData(string username)
                => skillProvider == null || skillProvider.GetPlayerSsrKeyCounts(username).Count > 0;

            private void applyFlowModeState(bool enabled)
            {
                tabControl.SetItemDisabled(Selection.Ranking, enabled);

                if (!enabled)
                    return;

                ScheduleAfterChildren(() =>
                {
                    if (EqualityComparer<Selection>.Default.Equals(tabControl.Current.Value, Selection.Ranking))
                        tabControl.SelectItem(Selection.Details);
                });
            }

            #region Reading / writing state from / to configuration

            private void updateConfigDetailTab()
            {
                switch (tabControl.Current.Value)
                {
                    case Selection.Details:
                        configDetailTab.Value = BeatmapDetailTab.Details;
                        return;

                    case Selection.Ranking:
                        configDetailTab.Value = mapLeaderboardScopeToDetailTab(scopeDropdown.Current.Value);
                        return;

                    case Selection.EzAnalysis:
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(tabControl.Current.Value), tabControl.Current.Value, null);
                }
            }

            private static BeatmapLeaderboardScope? tryMapDetailTabToLeaderboardScope(BeatmapDetailTab tab)
            {
                switch (tab)
                {
                    case BeatmapDetailTab.Local:
                        return BeatmapLeaderboardScope.Local;

                    case BeatmapDetailTab.Country:
                        return BeatmapLeaderboardScope.Country;

                    case BeatmapDetailTab.Global:
                        return BeatmapLeaderboardScope.Global;

                    case BeatmapDetailTab.Friends:
                        return BeatmapLeaderboardScope.Friend;

                    case BeatmapDetailTab.Team:
                        return BeatmapLeaderboardScope.Team;

                    default:
                        return null;
                }
            }

            private static BeatmapDetailTab mapLeaderboardScopeToDetailTab(BeatmapLeaderboardScope scope)
            {
                switch (scope)
                {
                    case BeatmapLeaderboardScope.Local:
                        return BeatmapDetailTab.Local;

                    case BeatmapLeaderboardScope.Country:
                        return BeatmapDetailTab.Country;

                    case BeatmapLeaderboardScope.Global:
                        return BeatmapDetailTab.Global;

                    case BeatmapLeaderboardScope.Friend:
                        return BeatmapDetailTab.Friends;

                    case BeatmapLeaderboardScope.Team:
                        return BeatmapDetailTab.Team;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
                }
            }

            #endregion

            public enum Selection
            {
                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Details))]
                Details,

                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Ranking))]
                Ranking,

                [LocalisableDescription(typeof(EzSongSelectStrings), nameof(EzSongSelectStrings.SELECTION_EZ_ANALYSIS))]
                EzAnalysis = 3,
            }

            private partial class ScopeDropdown : ShearedDropdown<BeatmapLeaderboardScope>
            {
                public ScopeDropdown()
                    : base(BeatmapLeaderboardWedgeStrings.Scope)
                {
                    Items = Enum.GetValues<BeatmapLeaderboardScope>();
                }

                protected override LocalisableString GenerateItemText(BeatmapLeaderboardScope item) => item.GetLocalisableDescription();
            }

            private partial class RadarModeDropdown : ShearedDropdown<EzRadarDisplayMode>
            {
                public RadarModeDropdown(LocalisableString label)
                    : base(label)
                {
                    Items = Enum.GetValues<EzRadarDisplayMode>();
                }

                protected override LocalisableString GenerateItemText(EzRadarDisplayMode item)
                {
                    if (item == EzRadarDisplayMode.Skill)
                        return EzSongSelectStrings.RADAR_MODE_SKILL;

                    var attr = typeof(EzRadarDisplayMode).GetField(item.ToString())
                                                         ?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                                         .OfType<DescriptionAttribute>()
                                                         .FirstOrDefault();
                    return attr?.Description ?? item.ToString();
                }
            }
        }
    }
}
