// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
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
        public Bindable<double>? VirtualHitPosition;
        private Bindable<double>? columnWidth;
        private Bindable<double>? specialFactor;
        private readonly Bindable<bool> dynamicTracking = new Bindable<bool>(false);

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
            VirtualHitPosition = config.GetBindable<double>(OsuSetting.VirtualHitPosition);

            dynamicTracking.ValueChanged += tracking =>
            {
                // 根据开关状态取消或添加监听器
                if (tracking.NewValue)
                {
                    // 启用动态追踪，添加监听器
                    columnWidth!.ValueChanged += OnSettingsValueChanged;
                    specialFactor!.ValueChanged += OnSettingsValueChanged;
                    VirtualHitPosition!.ValueChanged += OnSettingsValueChanged;
                }
                else
                {
                    // 禁用动态追踪，移除监听器
                    columnWidth!.ValueChanged -= OnSettingsValueChanged;
                    specialFactor!.ValueChanged -= OnSettingsValueChanged;
                    VirtualHitPosition!.ValueChanged -= OnSettingsValueChanged;
                }
            };

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
                        new SettingsCheckbox
                        {
                            LabelText = "(动态刷新)Dynamic Tracking",
                            Current = dynamicTracking,
                            TooltipText = "开启后滑动滑块时会实时刷新保存皮肤，可能导致卡顿"
                        },
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
                            Current = VirtualHitPosition,
                            KeyboardStep = 0.1f,
                        },
                        new SettingsButton
                        {
                            Action = RefreshSkin,
                        }.WithTwoLineText("(刷新并保存皮肤)", "Refresh & Save Skin")
                    }
                }
            };
        }

        private void OnSettingsValueChanged(ValueChangedEvent<double> _) => ScheduleRefresh();

        private ScheduledDelegate? scheduledRefresh;

        /// <summary>
        /// 使用防抖技术，延迟刷新皮肤，避免滑动滑块时频繁刷新导致卡顿
        /// </summary>
        public void ScheduleRefresh()
        {
            scheduledRefresh?.Cancel();
            scheduledRefresh = Scheduler.AddDelayed(RefreshSkin, 50);
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
