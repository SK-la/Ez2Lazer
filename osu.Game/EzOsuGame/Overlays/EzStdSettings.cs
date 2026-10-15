// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Play.PlayerSettings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Ez Osu 设置：StdJudgeMode 判定轨与进阶偏移。
    /// </summary>
    /// <remarks>
    /// 判定与分数是两根独立的轴：判定轨只决定判定窗口与计分算法，总分刻度不随它变。
    /// TODO(EZ): 分数刻度开关。现成「分数显示模式」(<c>OsuSetting.ScoreDisplayMode</c>) 的 Classic
    /// 是把量化分按最小二乘拟合近似换算回 classic 刻度；后续应改为直接取 stable 分
    /// （StableClassic 轨的 self-contained 原始总分 / <c>ScoreInfo.LegacyTotalScore</c>），不要拟合。
    /// </remarks>
    public partial class EzStdSettings : EzEnvironmentSettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_STD_SETTINGS_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            var judgementTrack = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.StdJudgeMode);
            var offsetNonMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusNonMania);

            Children = new Drawable[]
            {
                CreateSubmitWarningNote(),
                new SettingsItemV2(new FormEnumDropdown<EzEnumJudgeMode>
                {
                    Caption = EzSettingsStrings.OSU_JUDGEMENT_TRACK,
                    HintText = EzSettingsStrings.OSU_JUDGEMENT_TRACK_TOOLTIP,
                    Current = judgementTrack,
                    Items = new[] { EzEnumJudgeMode.Lazer, EzEnumJudgeMode.StableClassic },
                })
                {
                    Keywords = new[] { "ez", "osu", "std", "classic", "judgement" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.OFFSET_PLUS_NON_MANIA,
                    HintText = EzSettingsStrings.OFFSET_PLUS_NON_MANIA_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current = offsetNonMania,
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:N0} ms",
                    TooltipFormat = BeatmapOffsetControl.GetOffsetExplanatoryText,
                }),
            };

            TrackSubmitWarning(judgementTrack);
            TrackSubmitWarning(offsetNonMania);
        }
    }
}
