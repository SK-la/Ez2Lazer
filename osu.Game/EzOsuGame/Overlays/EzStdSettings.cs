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
    /// Ez Osu 设置：StdHitMode 判定轨与进阶偏移。
    /// </summary>
    public partial class EzStdSettings : EzEnvironmentSettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_STD_SETTINGS_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            var judgementTrack = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.StdHitMode);
            var offsetNonMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusNonMania);

            Children = new Drawable[]
            {
                CreateSubmitWarningNote(),
                new SettingsItemV2(new FormEnumDropdown<EzEnumHitMode>
                {
                    Caption = EzSettingsStrings.OSU_JUDGEMENT_TRACK,
                    HintText = EzSettingsStrings.OSU_JUDGEMENT_TRACK_TOOLTIP,
                    Current = judgementTrack,
                    Items = new[] { EzEnumHitMode.Lazer, EzEnumHitMode.StableClassic },
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
