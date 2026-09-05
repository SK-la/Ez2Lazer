// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileOverlay : FullscreenOverlay<EzLocalProfileHeader>
    {
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();
        private FillFlowContainer contentFlow = null!;
        private Container emptyStateContainer = null!;
        private OverlayRulesetSelector rulesetSelector = null!;

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
            Child = new OsuScrollContainer
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
                            AutoSizeAxes = Axes.Y,
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
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = HORIZONTAL_PADDING,
                                        Vertical = 10
                                    },
                                    Child = rulesetSelector = new OverlayRulesetSelector
                                    {
                                        Current = { BindTarget = ruleset }
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
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.Value = rulesets.GetRuleset(0) ?? rulesets.AvailableRulesets.First();
            ruleset.BindValueChanged(_ => Schedule(refreshContent), true);

            profileService.Snapshot.BindValueChanged(s => Schedule(() =>
            {
                Header.UpdateMeta(s.NewValue);
                refreshContent();
            }), true);

            API.LocalUser.BindValueChanged(u => Schedule(() => Header.UpdateUsername(u.NewValue.Username)), true);
        }

        protected override void PopIn()
        {
            base.PopIn();
            profileService.ReloadFromDisk();
            refreshContent();
        }

        private void refreshContent()
        {
            contentFlow.Clear();

            var snapshot = profileService.Snapshot.Value;

            if (!snapshot.HasData)
            {
                emptyStateContainer.Show();
                return;
            }

            emptyStateContainer.Hide();

            int rulesetId = ruleset.Value?.OnlineID ?? 0;
            var rulesetStats = snapshot.RulesetStats.FirstOrDefault(s => s.RulesetId == rulesetId);

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_CAREER,
                new EzLocalProfileCareerBody(rulesetStats, snapshot.GradeCounts.Where(g => g.RulesetId == rulesetId))));

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_MODE_DATA,
                new EzLocalProfileModeDataBody(snapshot, rulesetId)));

            refreshDrillContent(snapshot, rulesetId);
        }
    }
}
