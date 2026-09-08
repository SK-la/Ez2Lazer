// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
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
        private EzLocalProfileAnalysisSystemSelector analysisSystemSelector = null!;
        private OsuDropdown<string> playerDropdown = null!;
        private CancellationTokenSource? contentLoadCts;

        [Resolved]
        private EzLocalProfileService profileService { get; set; } = null!;

        [Resolved]
        private LoginOverlay? loginOverlay { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> gameRuleset { get; set; } = null!;

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
                                        // Match Insights section chrome so Background5 controls read as filled.
                                        Colour = ColourProvider.Background4,
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
            // Background4 parent (same as Insights section): dropdown + Ez/Track chips keep
            // their own Background5 idle / hover colours and stay visible without restyling OsuDropdown.
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
                        Colour = ColourProvider.Background4,
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
                                },
                                new Container
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    AutoSizeAxes = Axes.X,
                                    Height = 40,
                                    Child = analysisSystemSelector = new EzLocalProfileAnalysisSystemSelector
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Current = { BindTarget = analysisSystem },
                                        Alpha = 0,
                                    }
                                },
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            applyActiveRulesetOrDefault();
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
            analysisSystemSelector.Alpha = mania ? 1 : 0;

            if (!mania && analysisSystem.Value != EzLocalProfileAnalysisSystem.Ez)
                analysisSystem.Value = EzLocalProfileAnalysisSystem.Ez;
        }

        protected override void PopIn()
        {
            base.PopIn();
            applyActiveRulesetOrDefault();
            profileService.ReloadFromDisk();
        }

        /// <summary>
        /// Prefer the game's current ruleset when opening; fall back to osu! (id 0) if unavailable.
        /// </summary>
        private void applyActiveRulesetOrDefault()
        {
            RulesetInfo? preferred = null;
            var active = gameRuleset.Value;

            if (active != null)
            {
                preferred = rulesets.AvailableRulesets.FirstOrDefault(r => r.OnlineID == active.OnlineID)
                            ?? rulesets.GetRuleset(active.OnlineID);
            }

            ruleset.Value = preferred
                            ?? rulesets.GetRuleset(0)
                            ?? rulesets.AvailableRulesets.First();
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
            contentLoadCts?.Cancel();
            contentLoadCts?.Dispose();
            contentLoadCts = new CancellationTokenSource();
            var token = contentLoadCts.Token;

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
            string player = selectedPlayer.Value;
            var rulesetStats = snapshot.RulesetStats.FirstOrDefault(s => s.RulesetId == rulesetId);
            bool mania = rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID;
            bool trackMode = mania && analysisSystem.Value == EzLocalProfileAnalysisSystem.Track;

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_CAREER,
                new EzLocalProfileCareerBody(rulesetStats, snapshot.GradeCounts.Where(g => g.RulesetId == rulesetId))));

            if (!mania)
            {
                contentFlow.Add(new EzLocalProfileSection(
                    EzSettingsProfile.LOCAL_PROFILE_SECTION_MODE_DATA,
                    new EzLocalProfileModeDataBody(snapshot, rulesetId)));

                // Non-mania drill is usually small; still load off UI thread.
                var drillHost = createDeferredSectionHost();
                contentFlow.Add(new EzLocalProfileSection(EzSettingsProfile.LOCAL_PROFILE_SECTION_SCORE_DRILL, drillHost));
                contentFlow.Add(new EzLocalProfileSection(
                    EzSettingsProfile.LOCAL_PROFILE_SECTION_TRENDS,
                    new EzLocalProfileScoreTrendPanel(currentDrillScore)));

                beginDeferredDrillLoad(token, rulesetId, player, drillHost);
                return;
            }

            var insightsHost = createDeferredSectionHost();
            contentFlow.Add(new EzLocalProfileSection(EzSettingsProfile.LOCAL_PROFILE_SECTION_TRACK_INSIGHTS, insightsHost));

            Container skillsOrModeHost;

            if (trackMode)
            {
                skillsOrModeHost = createDeferredSectionHost();
                contentFlow.Add(new EzLocalProfileSection(EzSettingsProfile.LOCAL_PROFILE_SECTION_TRACK_SKILLS, skillsOrModeHost));
            }
            else
            {
                skillsOrModeHost = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Child = new EzLocalProfileModeDataBody(snapshot, rulesetId),
                };
                contentFlow.Add(new EzLocalProfileSection(EzSettingsProfile.LOCAL_PROFILE_SECTION_MODE_DATA, skillsOrModeHost));
            }

            var drillHostMania = createDeferredSectionHost();
            contentFlow.Add(new EzLocalProfileSection(EzSettingsProfile.LOCAL_PROFILE_SECTION_SCORE_DRILL, drillHostMania));
            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_TRENDS,
                new EzLocalProfileScoreTrendPanel(currentDrillScore)));

            Task.Run(() => profileService.LoadDrillScores(rulesetId, player), token).ContinueWith(task => Schedule(() =>
            {
                if (token.IsCancellationRequested || task.IsCanceled)
                    return;

                if (task.IsFaulted)
                {
                    insightsHost.Child = createLoadFailedHint();
                    if (trackMode)
                        skillsOrModeHost.Child = createLoadFailedHint();
                    drillHostMania.Child = createLoadFailedHint();
                    return;
                }

                var drills = task.GetResultSafely();

                insightsHost.Child = new EzLocalProfileInsightsBody(player, currentDrillScore, drills);

                if (trackMode)
                    skillsOrModeHost.Child = new EzLocalProfileTrackSkillsBody(player, currentDrillScore, drills);

                drillHostMania.Child = new EzLocalProfileScoreDrillPanel(currentDrillScore, drillSearchQuery, drills);
            }), token);
        }

        private void beginDeferredDrillLoad(CancellationToken token, int rulesetId, string player, Container drillHost)
        {
            Task.Run(() => profileService.LoadDrillScores(rulesetId, player), token).ContinueWith(task => Schedule(() =>
            {
                if (token.IsCancellationRequested || task.IsCanceled)
                    return;

                if (task.IsFaulted)
                {
                    drillHost.Child = createLoadFailedHint();
                    return;
                }

                drillHost.Child = new EzLocalProfileScoreDrillPanel(currentDrillScore, drillSearchQuery, task.GetResultSafely());
            }), token);
        }

        private static Container createDeferredSectionHost() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 80,
                Child = new LoadingSpinner
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    State = { Value = Visibility.Visible },
                },
            },
        };

        private static OsuSpriteText createLoadFailedHint() => new OsuSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_EMPTY,
            Font = OsuFont.GetFont(size: 14),
        };

        protected override void Dispose(bool isDisposing)
        {
            contentLoadCts?.Cancel();
            contentLoadCts?.Dispose();
            contentLoadCts = null;
            base.Dispose(isDisposing);
        }
    }
}
