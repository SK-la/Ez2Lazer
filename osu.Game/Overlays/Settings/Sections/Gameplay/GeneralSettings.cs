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
                LabelText = "O2Jam Health System",
                TooltipText = "Only for O2Jam HitMode. 只用于O2Jam模式。"
            };
            poorHitResultCheckbox = new SettingsCheckbox
            {
                Current = ezConfig.GetBindable<bool>(Ez2Setting.CustomPoorHitResult),
                LabelText = "Poor HitResult System",
                TooltipText = "Added a strict penalty for wrong presses outside the Miss range. "
                              + "will significantly increase the difficulty. Recommended for Ez2Ac and IIDX modes"
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
                    LabelText = "Hit Mode",
                    Current = ezConfig.GetBindable<EzMUGHitMode>(Ez2Setting.HitMode),
                    Keywords = new[] { "scoring" }
                },
                healthMode,
                poorHitResultCheckbox,
                new SettingsSlider<double>
                {
                    LabelText = "Accuracy Cutoff S",
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffS),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                },
                new SettingsSlider<double>
                {
                    LabelText = "Accuracy Cutoff A",
                    Current = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffA),
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitMode.Current.BindValueChanged(mode =>
                healthMode.Alpha = mode.NewValue == EzMUGHitMode.O2Jam ? 1 : 0, true);
        }
    }
}
