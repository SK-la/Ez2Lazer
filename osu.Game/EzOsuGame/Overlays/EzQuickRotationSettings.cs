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
    public partial class EzQuickRotationSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.QUICK_ROTATION_SECTION_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.QUICK_ROTATION_ENABLED,
                    HintText = EzSettingsStrings.QUICK_ROTATION_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.QuickRotationEnabled),
                })
                {
                    Keywords = new[] { "quick", "rotation", "快速轮换", "抽卡", "card" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.QUICK_ROTATION_DIFFICULTY_TOLERANCE,
                    HintText = EzSettingsStrings.QUICK_ROTATION_DIFFICULTY_TOLERANCE_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.QuickRotationDifficultyTolerance),
                    KeyboardStep = 0.1f,
                })
                {
                    Keywords = new[] { "quick", "rotation", "tolerance", "difficulty", "容差" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.QUICK_ROTATION_CROSS_KEY_MODE,
                    HintText = EzSettingsStrings.QUICK_ROTATION_CROSS_KEY_MODE_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.QuickRotationCrossKeyMode),
                })
                {
                    Keywords = new[] { "quick", "rotation", "mania", "key", "跨键" }
                },
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = EzSettingsStrings.QUICK_ROTATION_CANDIDATE_COUNT,
                    HintText = EzSettingsStrings.QUICK_ROTATION_CANDIDATE_COUNT_TOOLTIP,
                    Current = ezConfig.GetBindable<int>(Ez2Setting.QuickRotationCandidateCount),
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "quick", "rotation", "candidate", "card", "候选" }
                },
            });
        }
    }
}
