// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Ez 技能相关阈值（ChartDan / LN 半判定等）。
    /// </summary>
    public partial class EzSkillSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_SKILL_SECTION_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            Add(new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = EzSettingsStrings.SKILL_LN_CHART_MIN_HOLD_OBJECTS,
                HintText = EzSettingsStrings.SKILL_LN_CHART_MIN_HOLD_OBJECTS_TOOLTIP,
                Current = ezConfig.GetBindable<int>(Ez2Setting.SkillLnChartMinHoldObjects),
                KeyboardStep = 1,
            })
            {
                Keywords = new[] { "ez", "skill", "ln", "hold", "chartdan", "dan", "ratio", "技能", "占比", "门槛" }
            });
        }
    }
}
