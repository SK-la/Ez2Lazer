// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Ez / Track switch shown under the mania ruleset selector.
    /// </summary>
    public partial class EzLocalProfileAnalysisSystemSelector : OverlaySortTabControl<EzLocalProfileAnalysisSystem>
    {
        public EzLocalProfileAnalysisSystemSelector()
        {
            Title = EzSettingsProfile.LOCAL_PROFILE_ANALYSIS_SYSTEM;
            // OsuTabControl already adds all enum values automatically.
            Current.Value = EzLocalProfileAnalysisSystem.Ez;
        }
    }
}
