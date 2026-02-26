// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public partial class AnalysisSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Analysis";

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.INPUT_AUDIO_LATENCY_TRACKER,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                    HintText = EzSettingsStrings.INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP,
                })
                {
                    Keywords = new[] { "latency", "audio", "input" }
                }
            });
        }
    }
}
