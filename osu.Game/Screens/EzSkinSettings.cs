// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens
{
    public partial class EzSkinSettings : EditorSidebarSection
    {
        public Bindable<double> HitPosition { get; } = new BindableNumber<double>(110f)
        {
            MinValue = 0f,
            MaxValue = 300f,
            Precision = 10f,
        };

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

            // 这种刷新会卡，所以先不直接追踪刷新，用按钮手动刷新比较好
            // columnWidth.ValueChanged += _ => RefreshSkin();
            // specialFactor.ValueChanged += _ => RefreshSkin();
            // HitPosition.ValueChanged += _ => RefreshSkin();

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
                        new SettingsSlider<double>
                        {
                            LabelText = "(判定线)Hit Position",
                            Current = HitPosition,
                            KeyboardStep = 0.1f,
                        },
                        new SettingsButton
                        {
                            Action = () =>
                            {
                                RefreshSkin();
                            },
                        }.WithTwoLineText("(刷新并保存皮肤)", "Refresh & Save Skin")
                    }
                }
            };
        }

        public void RefreshSkin()
        {
            skinManager.CurrentSkinInfo.TriggerChange();
        }
    }

    public static class SettingsButtonExtensions
    {
        public static SettingsButton WithTwoLineText(this SettingsButton button, string topText, string bottomText, int fontSize = 14)
        {
            button.Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.AliceBlue,
                        Alpha = 0.1f
                    },
                    // 文本层
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = topText,
                                Font = OsuFont.GetFont(size: fontSize),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre
                            },
                            new OsuSpriteText
                            {
                                Text = bottomText,
                                Font = OsuFont.GetFont(size: fontSize),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre
                            }
                        }
                    }
                }
            };

            return button;
        }
    }

    public enum EditorMode
    {
        Default,
        EzSettings
    }
}
