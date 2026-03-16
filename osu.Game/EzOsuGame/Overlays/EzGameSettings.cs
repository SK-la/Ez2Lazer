// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Ez2ConfigManager ezConfig)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.ACCURACY_CUTOFF_S,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffS),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "mania", "acc" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.ACCURACY_CUTOFF_A,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffA),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "mania", "acc" }
                },
            };
        }
    }
}
