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
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileOverlay : FullscreenOverlay<EzLocalProfileHeader>
    {
        private const float player_filter_row_height = 64;

        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();
        private readonly Bindable<string> selectedPlayer = new Bindable<string>(EzLocalProfileConstants.ALL_PLAYERS);
        private readonly Bindable<EzLocalProfileAnalysisSystem> analysisSystem = new Bindable<EzLocalProfileAnalysisSystem>(EzLocalProfileAnalysisSystem.Ez);

        private FillFlowContainer contentFlow = null!;
        private Container emptyStateContainer = null!;
        private Container analysisSystemRow = null!;
        private OsuDropdown<string> playerDropdown = null!;

        [Resolved]
        private EzLocalProfileService profileService { get; set; } = null!;

        [Resolved]
        private LoginOverlay? loginOverlay { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        public EzLocalProfileOverlay()
            : base(OverlayColourScheme.Pink)
        {
        }

        protected override EzLocalProfileHeader CreateHeader() => new EzLocalProfileHeader
        {
            OpenAccount = () =>
            {
                Hide();
                loginOverlay?.Show();
            }
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            // Dropdown sits above the scroll layer so the open menu is not masked, and uses a fixed-height
            // chrome with Masking=false so the menu paints over content instead of vertically centering
            // inside a clipped Autosize row.
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[]
                {
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = player_filter_row_height,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = ColourProvider.Background5,
                                    }
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = ColourProvider.Background5,
                                        },
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Children = new Drawable[]
                                            {
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Padding = new MarginPadding
                                                    {
                                                        Horizontal = HORIZONTAL_PADDING,
                                                        Vertical = 10
                                                    },
                                                    Child = new OverlayRulesetSelector
                                                    {
                                                        Current = { BindTarget = ruleset }
                                                    }
                                                },
                                                analysisSystemRow = new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Padding = new MarginPadding
                                                    {
                                                        Horizontal = HORIZONTAL_PADDING,
                                                        Bottom = 10
                                                    },
                                                    Alpha = 0,
                                                    Child = new EzLocalProfileAnalysisSystemSelector
                                                    {
                                                        Current = { BindTarget = analysisSystem }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = HORIZONTAL_PADDING,
                                        Vertical = 20
                                    },
                                    Children = new Drawable[]
                                    {
                                        emptyStateContainer = new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Alpha = 0,
                                            Child = new EzLocalProfileEmptyState()
                                        },
                                        contentFlow = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 16),
                                        }
                                    }
                                }
                            }
                        }
                    },
                    createPlayerFilterLayer()
                }
            };
        }

        private Drawable createPlayerFilterLayer()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = player_filter_row_height,
                Masking = false,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourProvider.Background5,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Masking = false,
                        Padding = new MarginPadding
                        {
                            Horizontal = HORIZONTAL_PADDING,
                            Vertical = 10
                        },
                        // TopLeft on the horizontal flow axis so the label stays put when the menu grows.
                        Child = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(12, 0),
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    AutoSizeAxes = Axes.X,
                                    // Match OsuDropdownHeader height so the label lines up with the closed control.
                                    Height = 40,
                                    Child = new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = EzSettingsProfile.LOCAL_PROFILE_SELECT_PLAYER,
                                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                                    }
                                },
                                playerDropdown = new OsuDropdown<string>
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Width = 280,
                                    Current = { BindTarget = selectedPlayer },
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.Value = rulesets.GetRuleset(0) ?? rulesets.AvailableRulesets.First();
            ruleset.BindValueChanged(_ => Schedule(() =>
            {
                updateAnalysisSystemVisibility();
                refreshContent();
            }), true);
            selectedPlayer.BindValueChanged(_ => Schedule(refreshContent));
            analysisSystem.BindValueChanged(_ => Schedule(refreshContent));

            profileService.Snapshot.BindValueChanged(s => Schedule(() =>
            {
                Header.UpdateMeta(s.NewValue);
                refreshPlayerDropdownItems(s.NewValue);
                refreshContent();
            }), true);

            API.LocalUser.BindValueChanged(u => Schedule(() => Header.UpdateUsername(u.NewValue.Username)), true);
            updateAnalysisSystemVisibility();
        }

        private void updateAnalysisSystemVisibility()
        {
            bool mania = (ruleset.Value?.OnlineID ?? -1) == EzLocalProfileConstants.MANIA_RULESET_ID;
            analysisSystemRow.Alpha = mania ? 1 : 0;

            if (!mania && analysisSystem.Value != EzLocalProfileAnalysisSystem.Ez)
                analysisSystem.Value = EzLocalProfileAnalysisSystem.Ez;
        }

        protected override void PopIn()
        {
            base.PopIn();
            profileService.ReloadFromDisk();
            refreshContent();
        }

        private void refreshPlayerDropdownItems(EzLocalProfileSnapshot archiveSnapshot)
        {
            var items = new List<string> { EzLocalProfileConstants.ALL_PLAYERS };

            foreach (string name in archiveSnapshot.IncludedUsernames
                                                   .Select(EzLocalProfileConstants.NormaliseUsername)
                                                   .Distinct(StringComparer.Ordinal)
                                                   .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(name);
            }

            playerDropdown.Items = items;

            if (!items.Contains(selectedPlayer.Value, StringComparer.Ordinal))
                selectedPlayer.Value = EzLocalProfileConstants.ALL_PLAYERS;
        }

        private void refreshContent()
        {
            contentFlow.Clear();
            currentDrillScore.Value = null;

            var snapshot = profileService.LoadDisplaySnapshot(selectedPlayer.Value);

            if (!snapshot.HasData)
            {
                emptyStateContainer.Show();
                return;
            }

            emptyStateContainer.Hide();

            int rulesetId = ruleset.Value?.OnlineID ?? 0;
            var rulesetStats = snapshot.RulesetStats.FirstOrDefault(s => s.RulesetId == rulesetId);
            bool trackMode = rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID
                             && analysisSystem.Value == EzLocalProfileAnalysisSystem.Track;

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_CAREER,
                new EzLocalProfileCareerBody(rulesetStats, snapshot.GradeCounts.Where(g => g.RulesetId == rulesetId))));

            if (trackMode)
            {
                if (string.Equals(selectedPlayer.Value, EzLocalProfileConstants.ALL_PLAYERS, StringComparison.Ordinal))
                {
                    contentFlow.Add(new EzLocalProfileSection(
                        EzSettingsProfile.LOCAL_PROFILE_SECTION_TRACK_INSIGHTS,
                        new OsuSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_NEEDS_PLAYER,
                            Font = OsuFont.GetFont(size: 14),
                        }));

                    contentFlow.Add(new EzLocalProfileSection(
                        EzSettingsProfile.LOCAL_PROFILE_SECTION_TRACK_SKILLS,
                        new OsuSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_NEEDS_PLAYER,
                            Font = OsuFont.GetFont(size: 14),
                        }));
                }
                else
                {
                    contentFlow.Add(new EzLocalProfileSection(
                        EzSettingsProfile.LOCAL_PROFILE_SECTION_TRACK_INSIGHTS,
                        new EzLocalProfileTrackInsightsBody(selectedPlayer.Value, currentDrillScore)));

                    contentFlow.Add(new EzLocalProfileSection(
                        EzSettingsProfile.LOCAL_PROFILE_SECTION_TRACK_SKILLS,
                        new EzLocalProfileTrackSkillsBody(selectedPlayer.Value)));
                }
            }
            else
            {
                contentFlow.Add(new EzLocalProfileSection(
                    EzSettingsProfile.LOCAL_PROFILE_SECTION_MODE_DATA,
                    new EzLocalProfileModeDataBody(snapshot, rulesetId)));
            }

            refreshDrillContent(snapshot, rulesetId);
        }
    }
}
