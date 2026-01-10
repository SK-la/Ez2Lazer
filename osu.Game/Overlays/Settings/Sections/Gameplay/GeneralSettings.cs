// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        private SettingsEnumDropdown<EzMUGHitMode> hitMode = null!;
        private SettingsEnumDropdown<EnumHealthMode> healthMode = null!;
        private SettingsCheckbox poorHitResultCheckbox = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Ez2ConfigManager ezConfig)
        {
            healthMode = new SettingsEnumDropdown<EnumHealthMode>
            {
                ClassicDefault = EnumHealthMode.Lazer,
                Current = ezConfig.GetBindable<EnumHealthMode>(Ez2Setting.CustomHealthMode),
                LabelText = EzLocalizationManager.HealthMode,
                TooltipText = EzLocalizationManager.HealthModeTooltip,
                Keywords = new[] { "mania" }
            };
            poorHitResultCheckbox = new SettingsCheckbox
            {
                Current = ezConfig.GetBindable<bool>(Ez2Setting.CustomPoorHitResult),
                LabelText = EzLocalizationManager.PoorHitResult,
                TooltipText = EzLocalizationManager.PoorHitResultTooltip,
                Keywords = new[] { "mania" }
            };

            Children = new Drawable[]
            {
                new SettingsEnumDropdown<ScoringMode>
                {
                    ClassicDefault = ScoringMode.Classic,
                    LabelText = GameplaySettingsStrings.ScoreDisplayMode,
                    Current = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode),
                    Keywords = new[] { "scoring" }
                },
                hitMode = new SettingsEnumDropdown<EzMUGHitMode>
                {
                    ClassicDefault = EzMUGHitMode.EZ2AC,
                    LabelText = EzLocalizationManager.HitMode,
                    TooltipText = EzLocalizationManager.HitModeTooltip,
                    Current = ezConfig.GetBindable<EzMUGHitMode>(Ez2Setting.HitMode),
                    Keywords = new[] { "mania" }
                },
                healthMode,
                poorHitResultCheckbox,
                new SettingsSlider<double>
                {
                    LabelText = EzLocalizationManager.AccuracyCutoffS,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffS),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    Keywords = new[] { "mania" }
                },
                new SettingsSlider<double>
                {
                    LabelText = EzLocalizationManager.AccuracyCutoffA,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffA),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    Keywords = new[] { "mania" }
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitMode.Current.BindValueChanged(mode =>
                healthMode.Alpha = mode.NewValue == EzMUGHitMode.O2Jam ? 1 : 0, true);
        }
    }
}
