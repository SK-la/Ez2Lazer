// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzExperimentalSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Experimental";

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                    Caption = EzSettingsStrings.INPUT_AUDIO_LATENCY_TRACKER,
                    HintText = EzSettingsStrings.INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP,
                })
                {
                    Keywords = new[] { "latency", "audio", "input" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.ExperimentalP2P),
                    Caption = EzSettingsStrings.EXPERIMENTAL_P2P,
                    HintText = EzSettingsStrings.EXPERIMENTAL_P2P_TOOLTIP,
                })
                {
                    Keywords = new[] { "net", "p2p" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzJudgmentDiagEnabled),
                    Caption = EzSettingsStrings.EZ_JUDGMENT_DIAG_ENABLED,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzSubFrameCorrectionEnabled),
                    Caption = EzSettingsStrings.EZ_SUB_FRAME_CORRECTION_ENABLED,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzTimingTraceEnabled),
                    Caption = EzSettingsStrings.EZ_TIMING_TRACE_ENABLED,
                }),
            });
        }
    }
}
