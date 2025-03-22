// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsEnumDropdown<ScoringMode>
                {
                    ClassicDefault = ScoringMode.Classic,
                    LabelText = GameplaySettingsStrings.ScoreDisplayMode,
                    Current = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode),
                    Keywords = new[] { "scoring" }
                },
                new SettingsEnumDropdown<HitWindows.HitMode>
                {
                    ClassicDefault = HitWindows.HitMode.Ez2AcStyle,
                    LabelText = GameplaySettingsStrings.HitMode,
                    Current = config.GetBindable<HitWindows.HitMode>(OsuSetting.HitMode),
                    Keywords = new[] { "scoring" }
                },
                new SettingsSlider<double>
                {
                    LabelText = GameplaySettingsStrings.AccuracyCutoffS,
                    Current = config.GetBindable<double>(OsuSetting.AccuracyCutoffS),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                },
                new SettingsSlider<double>
                {
                    LabelText = GameplaySettingsStrings.AccuracyCutoffA,
                    Current = config.GetBindable<double>(OsuSetting.AccuracyCutoffA),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                },
                new SettingsCheckbox
                {
                    LabelText = GraphicsSettingsStrings.HitLighting,
                    Current = config.GetBindable<bool>(OsuSetting.HitLighting)
                },
                new SettingsCheckbox
                {
                    LabelText = GameplaySettingsStrings.StarFountains,
                    Current = config.GetBindable<bool>(OsuSetting.StarFountains)
                },
            };
        }
    }
}
