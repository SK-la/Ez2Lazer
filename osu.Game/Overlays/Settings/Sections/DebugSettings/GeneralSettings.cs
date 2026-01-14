// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"General";

        [BackgroundDependencyLoader]
        private void load(FrameworkDebugConfigManager config, FrameworkConfigManager frameworkConfig, Ez2ConfigManager ezConfig)
        {
            Add(new SettingsCheckbox
            {
                LabelText = @"Show log overlay",
                Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowLogOverlay)
            });

            Add(new SettingsCheckbox
            {
                LabelText = @"Bypass front-to-back render pass",
                Current = config.GetBindable<bool>(DebugSetting.BypassFrontToBackPass)
            });

            Add(new SettingsCheckbox
            {
                LabelText = EzLocalizationManager.InputAudioLatencyTracker,
                Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                TooltipText = EzLocalizationManager.InputAudioLatencyTrackerTooltip,
                Keywords = new[] { "latency", "audio", "input" }
            });
        }
    }
}
