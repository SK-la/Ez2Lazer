// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Play.PlayerSettings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_GAME_SETTINGS_HEADER;

        private readonly Bindable<bool> scoreSubmitWarning = new Bindable<bool>();
        private readonly Bindable<SettingsNote.Data?> warningNote = new Bindable<SettingsNote.Data?>();

        private SettingsItemV2 poorCheckBox = null!;

        private Bindable<double> sAcc = null!;
        private Bindable<double> aAcc = null!;

        private Bindable<EzEnumHitMode> maniaHitModeBindable = null!;
        private Bindable<EzEnumHealthMode> maniaHealthModeBindable = null!;
        private Bindable<bool> bmsPoorHitResultEnable = null!;
        private Bindable<double> offsetManiaBindable = null!;
        private Bindable<double> offsetNonManiaBindable = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            sAcc = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffS);
            aAcc = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffA);
            maniaHitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            maniaHealthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            bmsPoorHitResultEnable = ezConfig.GetBindable<bool>(Ez2Setting.BmsPoorHitResultEnable);
            offsetManiaBindable = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
            offsetNonManiaBindable = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusNonMania);

            Children = new Drawable[]
            {
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current = { BindTarget = warningNote },
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.ACCURACY_CUTOFF_S,
                    Current = sAcc,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "ez", "mania", "acc" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.ACCURACY_CUTOFF_A,
                    Current = aAcc,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "ez", "mania", "acc" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzEnumHitMode>
                {
                    Caption = EzSettingsStrings.HIT_MODE,
                    HintText = EzSettingsStrings.HIT_MODE_TOOLTIP,
                    Current = maniaHitModeBindable,
                })
                {
                    Keywords = new[] { "ez", "mania" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzEnumHealthMode>
                {
                    Caption = EzSettingsStrings.HEALTH_MODE,
                    HintText = EzSettingsStrings.HEALTH_MODE_TOOLTIP,
                    Current = maniaHealthModeBindable,
                })
                {
                    Keywords = new[] { "ez", "mania" }
                },
                poorCheckBox = new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.POOR_HIT_RESULT,
                    HintText = EzSettingsStrings.POOR_HIT_RESULT_TOOLTIP,
                    Current = bmsPoorHitResultEnable,
                })
                {
                    Keywords = new[] { "ez", "mania", "bms" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.OFFSET_PLUS_MANIA,
                    HintText = EzSettingsStrings.OFFSET_PLUS_MANIA_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current = offsetManiaBindable,
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:N0} ms",
                    TooltipFormat = BeatmapOffsetControl.GetOffsetExplanatoryText,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.OFFSET_PLUS_NON_MANIA,
                    HintText = EzSettingsStrings.OFFSET_PLUS_NON_MANIA_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current =  offsetNonManiaBindable,
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:N0} ms",
                    TooltipFormat = BeatmapOffsetControl.GetOffsetExplanatoryText,
                }),
            };

            maniaHealthModeBindable.BindValueChanged(e =>
            {
                switch (e.NewValue)
                {
                    case EzEnumHealthMode.IIDX_HD:
                    case EzEnumHealthMode.LR2_HD:
                    case EzEnumHealthMode.Raja_NM:
                        poorCheckBox.Show();
                        break;

                    default:
                        poorCheckBox.Hide();
                        break;
                }
            }, true);

            maniaHitModeBindable.BindValueChanged(_ => Schedule(updateScoreSubmitWarning));
            maniaHealthModeBindable.BindValueChanged(_ => Schedule(updateScoreSubmitWarning));
            aAcc.BindValueChanged(_ => Schedule(updateScoreSubmitWarning));
            sAcc.BindValueChanged(_ => Schedule(updateScoreSubmitWarning));
            offsetManiaBindable.BindValueChanged(_ => Schedule(updateScoreSubmitWarning));
            offsetNonManiaBindable.BindValueChanged(_ => Schedule(updateScoreSubmitWarning));
            scoreSubmitWarning.BindValueChanged(_ => Schedule(updateScoreSubmitWarning), true);
        }

        private void updateScoreSubmitWarning()
        {
            scoreSubmitWarning.Value = (!maniaHitModeBindable.IsDefault || !maniaHealthModeBindable.IsDefault
                                                                        || !aAcc.IsDefault
                                                                        || !sAcc.IsDefault
                                                                        || !offsetManiaBindable.IsDefault
                                                                        || !offsetNonManiaBindable.IsDefault);

            if (scoreSubmitWarning.Value)
            {
                warningNote.Value = new SettingsNote.Data(EzSettingsStrings.SCORE_SUBMIT_WARNING, SettingsNote.Type.Warning);
            }
            else
            {
                warningNote.Value = null;
            }
        }
    }
}
