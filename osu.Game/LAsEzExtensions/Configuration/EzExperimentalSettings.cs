// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public partial class EzExperimentalSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "EzExperimental";

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzLocalizationManager.InputAudioLatencyTracker,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                    HintText = EzLocalizationManager.InputAudioLatencyTrackerTooltip,
                })
                {
                    Keywords = new[] { "latency", "audio", "input" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzLocalizationManager.IExperimentalP2P,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.ExperimentalP2P),
                    HintText = EzLocalizationManager.IExperimentalP2PTooltip,
                })
                {
                    Keywords = new[] { "latency", "audio", "input", "overlay" }
                }
            });
        }
    }
}
