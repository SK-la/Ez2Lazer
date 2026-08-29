// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileOverlay
    {
        private readonly Bindable<EzLocalProfileDrillScoreRow?> currentDrillScore = new Bindable<EzLocalProfileDrillScoreRow?>();
        private string drillSearchQuery = string.Empty;

        private void refreshDrillContent(EzLocalProfileSnapshot snapshot, int rulesetId)
        {
            var allScores = profileService.LoadDrillScores(rulesetId);
            var filteredScores = EzLocalProfileScoreDrillQuery.Filter(allScores, drillSearchQuery);

            var searchBox = new EzLocalProfileScoreSearchBox();
            if (!string.IsNullOrEmpty(drillSearchQuery))
                searchBox.Text = drillSearchQuery;

            searchBox.Current.BindValueChanged(e =>
            {
                drillSearchQuery = e.NewValue ?? string.Empty;
                Schedule(refreshContent);
            });

            var selector = new EzLocalProfileScoreSelector { Current = { BindTarget = currentDrillScore } };
            selector.SetScores(filteredScores);

            var detailColumn = new EzLocalProfileScoreDetailColumn(currentDrillScore, allScores);

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsStrings.LOCAL_PROFILE_SECTION_SCORE_DRILL,
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        searchBox,
                        filteredScores.Count == 0
                            ? new OsuSpriteText
                            {
                                Text = EzSettingsStrings.LOCAL_PROFILE_DRILL_NO_MATCHES,
                                Font = OsuFont.GetFont(size: 14),
                            }
                            : new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Absolute, EzLocalProfileScoreSelector.WIDTH),
                                    new Dimension(),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        selector,
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Margin = new MarginPadding { Left = 12 },
                                            Child = detailColumn,
                                        },
                                    },
                                },
                            }
                    }
                }));

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsStrings.LOCAL_PROFILE_SECTION_TRENDS,
                new EzLocalProfileScoreTrendPanel(currentDrillScore)));
        }
    }
}
