// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens
{
    public partial class EzSkinSettings : EditorSidebarSection
    {
        private Bindable<double>? columnWidth;
        private Bindable<double>? specialFactor;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public EzSkinSettings()
            : base("EZ Skin Settings")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnWidth = config.GetBindable<double>(OsuSetting.ColumnWidth);
            specialFactor = config.GetBindable<double>(OsuSetting.SpecialFactor);

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        new SettingsSlider<double>
                        {
                            LabelText = "(列宽)Column Width",
                            Current = columnWidth,
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "(特殊列倍率)Special Factor",
                            Current = specialFactor,
                            KeyboardStep = 0.1f,
                        },
                        new SettingsButton
                        {
                            Text = "(刷新皮肤)Update Skin No Active",
                            Action = () =>
                            {
                                var currentSkin = skinManager.CurrentSkin.Value;

                                if (currentSkin is LegacySkin legacySkin)
                                {
                                    var info = legacySkin.SkinInfo;
                                    skinManager.CurrentSkinInfo.Value = null;
                                    skinManager.CurrentSkinInfo.Value = info;
                                }
                            }
                        }
                    }
                }
            };
        }
    }

    public enum EditorMode
    {
        Default,
        EzSettings
    }

    public enum SidebarPosition
    {
        Left,
        Right
    }
}
