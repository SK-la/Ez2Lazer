// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileOverlay
    {
        private readonly Bindable<EzLocalProfileDrillScoreRow?> currentDrillScore = new Bindable<EzLocalProfileDrillScoreRow?>();
        private readonly Bindable<string> drillSearchQuery = new Bindable<string>(string.Empty);

        private void refreshDrillContent(EzLocalProfileSnapshot snapshot, int rulesetId)
        {
            var allScores = profileService.LoadDrillScores(rulesetId);

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_SCORE_DRILL,
                new EzLocalProfileScoreDrillPanel(currentDrillScore, drillSearchQuery, allScores)));

            contentFlow.Add(new EzLocalProfileSection(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_TRENDS,
                new EzLocalProfileScoreTrendPanel(currentDrillScore)));
        }
    }
}
