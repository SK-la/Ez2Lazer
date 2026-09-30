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
    /// 转盘选曲设置（常规 song select），紧跟在 <see cref="EzScratchAxisSettings"/> 下方。
    /// 选曲本身随「使用 Ez2Ac 10k2s1p」启用、每格固定 1/24 圈，因此这里只保留方向反转。
    /// </summary>
    public partial class EzScratchAxisSongSelectSettings : FillFlowContainer
    {
        private Bindable<bool> invertEnabled = null!;

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
            invertEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ScratchAxisSongSelectInvert);

            Children = new Drawable[]
            {
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
