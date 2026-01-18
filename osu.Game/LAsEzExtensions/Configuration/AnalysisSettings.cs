// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Overlays.Settings;
using osu.Game.Screens;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public partial class AnalysisSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Analysis";

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, OsuGameBase game, GameHost host, IPerformFromScreenRunner? performer)
        {
            AddRange(new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = EzLocalizationManager.InputAudioLatencyTracker,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                    TooltipText = EzLocalizationManager.InputAudioLatencyTrackerTooltip,
                    Keywords = new[] { "latency", "audio", "input" }
                }
            });
        }
    }
}
