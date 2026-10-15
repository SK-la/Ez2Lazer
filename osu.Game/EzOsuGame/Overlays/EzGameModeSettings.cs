// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring.Bonus;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Play.PlayerSettings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameModeSettings : EzEnvironmentSettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_GAME_SETTINGS_HEADER;

        private SettingsItemV2 poorCheckBox = null!;

        private Bindable<double> sAcc = null!;
        private Bindable<double> aAcc = null!;

        private Bindable<EzEnumJudgeMode> maniaJudgeModeBindable = null!;
        private Bindable<EzEnumHealthMode> maniaHealthModeBindable = null!;
        private Bindable<bool> bmsPoorHitResultEnable = null!;
        private Bindable<EzEnumJudgePrecedence> judgePrecedenceBindable = null!;
        private Bindable<double> offsetManiaBindable = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            sAcc = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffS);
            aAcc = ezConfig.GetBindable<double>(Ez2Setting.AccuracyCutoffA);
            maniaJudgeModeBindable = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);
            maniaHealthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            bmsPoorHitResultEnable = ezConfig.GetBindable<bool>(Ez2Setting.BmsPoorHitResultEnable);
            judgePrecedenceBindable = ezConfig.GetBindable<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence);
            offsetManiaBindable = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);

            Children = new Drawable[]
            {
                CreateSubmitWarningNote(),
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
                new SettingsItemV2(new FormEnumDropdown<EzEnumJudgeMode>
                {
                    Caption = EzSettingsStrings.HIT_MODE,
                    HintText = EzSettingsStrings.HIT_MODE_TOOLTIP,
                    Current = maniaJudgeModeBindable,
                    // StableClassic 是给后续共享 classic 预留的档，现行 mania classic 仍是 Classic。
                    Items = Enum.GetValues<EzEnumJudgeMode>().Where(m => m != EzEnumJudgeMode.StableClassic),
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
                    Keywords = new[] { "ez", "mania", "bms", "poor" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzEnumJudgePrecedence>
                {
                    Caption = EzSettingsStrings.JUDGE_PRECEDENCE,
                    HintText = EzSettingsStrings.JUDGE_PRECEDENCE_TOOLTIP,
                    Current = judgePrecedenceBindable,
                })
                {
                    Keywords = new[] { "ez", "mania", "bms", "judge" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzScoreBonusTendency>
                {
                    Caption = EzSettingsStrings.SCORE_BONUS_TENDENCY,
                    HintText = EzSettingsStrings.SCORE_BONUS_TENDENCY_TOOLTIP,
                    Current = ezConfig.GetBindable<EzScoreBonusTendency>(Ez2Setting.ScoreBonusTendency),
                })
                {
                    Keywords = new[] { "ez", "mania", "bonus", "miss", "附加分" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.SCORE_BONUS_CROSS,
                    HintText = EzSettingsStrings.SCORE_BONUS_CROSS_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.ScoreBonusCrossMs),
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:N0} ms",
                })
                {
                    Keywords = new[] { "ez", "bonus", "boundary", "分界", "44" }
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
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SKIP_EMPTY_EDGE_COLUMNS,
                    HintText = EzSettingsStrings.SKIP_EMPTY_EDGE_COLUMNS_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns),
                })
                {
                    Keywords = new[] { "ez", "skip", "empty", "column" }
                },
                new EzScratchAxisSettings(),
            };

            maniaHealthModeBindable.BindValueChanged(e =>
            {
                switch (e.NewValue)
                {
                    case EzEnumHealthMode.IIDX_HD:
                    case EzEnumHealthMode.LR2_HD:
                    case EzEnumHealthMode.Raja_HD:
                        poorCheckBox.Show();
                        break;

                    default:
                        poorCheckBox.Hide();
                        break;
                }
            }, true);

            TrackSubmitWarning(maniaJudgeModeBindable);
            TrackSubmitWarning(maniaHealthModeBindable);
            TrackSubmitWarning(judgePrecedenceBindable);
            TrackSubmitWarning(aAcc);
            TrackSubmitWarning(sAcc);
            TrackSubmitWarning(offsetManiaBindable);
        }
    }
}
