// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 转盘选曲设置（常规 song select），紧跟在 <see cref="EzScratchAxisSettings"/> 下方，
    /// 复用同一份 L/R 绑定。
    /// </summary>
    public partial class EzScratchAxisSongSelectSettings : FillFlowContainer
    {
        private Bindable<bool> songSelectEnabled = null!;
        private Bindable<bool> invertEnabled = null!;
        private Bindable<double> stepSize = null!;

        public EzScratchAxisSongSelectSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            songSelectEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisSongSelectEnabled);
            invertEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisSongSelectInvert);
            stepSize = ezConfig.GetBindable<double>(Ez2Setting.ScratchAxisSongSelectStep);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SCRATCH_AXIS_SONGSELECT_ENABLED,
                    HintText = EzSettingsStrings.SCRATCH_AXIS_SONGSELECT_ENABLED_TOOLTIP,
                    Current = songSelectEnabled,
                })
                {
                    Keywords = new[] { "ez", "scratch", "songselect", "select", "turntable", "转盘", "选歌" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.SCRATCH_AXIS_SONGSELECT_STEP,
                    HintText = EzSettingsStrings.SCRATCH_AXIS_SONGSELECT_STEP_TOOLTIP,
                    RelativeSizeAxes = Axes.X,
                    Current = stepSize,
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:0.###}",
                })
                {
                    Keywords = new[] { "ez", "scratch", "songselect", "step", "sensitivity", "转盘", "灵敏度" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.SCRATCH_AXIS_SONGSELECT_INVERT,
                    HintText = EzSettingsStrings.SCRATCH_AXIS_SONGSELECT_INVERT_TOOLTIP,
                    Current = invertEnabled,
                })
                {
                    Keywords = new[] { "ez", "scratch", "songselect", "invert", "reverse", "转盘", "反转" }
                },
            };
        }
    }
}
