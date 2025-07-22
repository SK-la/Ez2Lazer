// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2.Components
{
    public partial class KeyModeFilterTabControl : CompositeDrawable
    {
        private readonly ShearedKeyModeTabControl tabControl;
        private readonly ShearedToggleButton multiSelectToggle;
        private Bindable<EzSelectMode> ezMode = null!;
        private readonly BindableBool isMultiSelectMode = new BindableBool();

        // 保存进入多选模式前的单选状态
        private EzSelectMode singleModeBeforeMultiSelect = EzSelectMode.All;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        // 为了保持与现有代码的兼容性，我们需要一个单选模式的 bindable
        public IBindable<EzSelectMode> Current => tabControl.Current;

        // 新增：多选模���的访问接口
        public MultiSelectEzMode MultiSelect { get; } = new MultiSelectEzMode();

        public KeyModeFilterTabControl()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                tabControl = new ShearedKeyModeTabControl("Keys", MultiSelect, isMultiSelectMode)
                {
                    RelativeSizeAxes = Axes.X,
                },
                multiSelectToggle = new ShearedToggleButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Text = "Multi",
                    Height = 30f, // 只设置高度，不设置宽度
                    Margin = new MarginPadding { Right = 10 }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            ezMode = config.GetBindable<EzSelectMode>(OsuSetting.SelectEzMode);
            tabControl.Current.BindTarget = ezMode;

            // 绑定多选开关
            multiSelectToggle.Active.BindTo(isMultiSelectMode);

            // 监听多选模式切换
            isMultiSelectMode.BindValueChanged(multiSelectModeChanged, true);

            // 当多选状态改变时，更新单选状态（用于配置保存）
            MultiSelect.SelectionChanged += () =>
            {
                // 如果只选择了一个模式，同步到单选 bindable
                var selected = MultiSelect.SelectedModes;

                if (selected.Count == 1)
                {
                    ezMode.Value = selected.First();
                }
                else if (selected.Contains(EzSelectMode.All))
                {
                    ezMode.Value = EzSelectMode.All;
                }
            };

            // 当单选状态改变时，同步到多选状态（用于配置加载）
            ezMode.BindValueChanged(e =>
            {
                if (!MultiSelect.SelectedModes.Contains(e.NewValue) || MultiSelect.SelectedModes.Count > 1)
                {
                    MultiSelect.SetSelection(new System.Collections.Generic.HashSet<EzSelectMode> { e.NewValue });
                }
            }, true);
        }

        private void multiSelectModeChanged(ValueChangedEvent<bool> e)
        {
            if (e.NewValue)
            {
                // 开启多选模式：保存当前单选状态
                singleModeBeforeMultiSelect = ezMode.Value;
            }
            else
            {
                // 关闭多选模式：清空多选并恢复到之前的单选状态
                MultiSelect.SetSelection(new System.Collections.Generic.HashSet<EzSelectMode> { singleModeBeforeMultiSelect });
                ezMode.Value = singleModeBeforeMultiSelect;
            }
        }

        private partial class ShearedKeyModeTabControl : OsuTabControl<EzSelectMode>
        {
            private readonly Container labelContainer;
            private readonly Box labelBox;
            private readonly MultiSelectEzMode multiSelectEzMode;
            private readonly IBindable<bool> isMultiSelectMode;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ShearedKeyModeTabControl(LocalisableString label, MultiSelectEzMode multiSelectEzMode, IBindable<bool> isMultiSelectMode)
            {
                this.multiSelectEzMode = multiSelectEzMode;
                this.isMultiSelectMode = isMultiSelectMode;

                RelativeSizeAxes = Axes.X;
                Height = 30;
                Shear = OsuGame.SHEAR;
                CornerRadius = ShearedButton.CORNER_RADIUS;
                Masking = true;

                AddInternal(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Transparent,
                    Name = "Background"
                });

                AddInternal(labelContainer = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    CornerRadius = ShearedButton.CORNER_RADIUS,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        labelBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        new OsuSpriteText
                        {
                            Text = label,
                            Margin = new MarginPadding { Horizontal = 10f, Vertical = 7f },
                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                            Shear = -OsuGame.SHEAR,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        }
                    }
                });

                // 设置Tab容器属性
                TabContainer.Anchor = Anchor.CentreLeft;
                TabContainer.Origin = Anchor.CentreLeft;
                TabContainer.RelativeSizeAxes = Axes.X;
                TabContainer.AutoSizeAxes = Axes.Y;
                TabContainer.Padding = new MarginPadding { Left = 10f, Right = 80f }; // 为右侧开关按钮留空间
                TabContainer.Spacing = new Vector2(5f, 0f);
                TabContainer.Shear = -OsuGame.SHEAR;

                // 清除并添加所有选项
                Clear();

                foreach (var mode in Enum.GetValues<EzSelectMode>())
                {
                    AddItem(mode);
                }

                // 监听多选状态变化，更新 tab 的激活状态
                multiSelectEzMode.SelectionChanged += updateTabActivation;
            }

            private void updateTabActivation()
            {
                // 更新所有 tab 的激活状态以反映多选状态
                foreach (var tabItem in TabContainer.Children.OfType<ShearedKeyModeTabItem>())
                {
                    tabItem.UpdateMultiSelectState();
                }
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                labelBox.Colour = colourProvider.Background3;
                InternalChildren.First().Colour = colourProvider.Background5; // 背景色
            }

            protected override void Update()
            {
                base.Update();
                // ���整Tab容器的位置，避免与标签和开关按钮重叠
                TabContainer.Padding = new MarginPadding
                {
                    Left = labelContainer.DrawWidth + 10f,
                    Right = 80f // 为开关按钮留空间
                };
            }

            protected override TabItem<EzSelectMode> CreateTabItem(EzSelectMode value)
            {
                return new ShearedKeyModeTabItem(value, multiSelectEzMode, isMultiSelectMode);
            }

            protected override Dropdown<EzSelectMode> CreateDropdown() => null!; // 禁用下拉菜单

            private partial class ShearedKeyModeTabItem : TabItem<EzSelectMode>
            {
                private readonly OsuSpriteText text;
                private readonly Box mainBackground;
                private readonly Box hoverLayer;
                private readonly MultiSelectEzMode multiSelectEzMode;
                private readonly IBindable<bool> isMultiSelectMode;

                [Resolved]
                private OverlayColourProvider colourProvider { get; set; } = null!;

                public ShearedKeyModeTabItem(EzSelectMode value, MultiSelectEzMode multiSelectEzMode, IBindable<bool> isMultiSelectMode)
                    : base(value) // 修正：使用参数 value 而不是属性 Value
                {
                    this.multiSelectEzMode = multiSelectEzMode;
                    this.isMultiSelectMode = isMultiSelectMode;

                    AutoSizeAxes = Axes.Both;
                    CornerRadius = ShearedButton.CORNER_RADIUS;
                    Masking = true;
                    Shear = OsuGame.SHEAR;

                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = ShearedButton.CORNER_RADIUS,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                mainBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                hoverLayer = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0f,
                                }
                            }
                        },
                        text = new OsuSpriteText
                        {
                            Text = getDisplayText(value),
                            Font = OsuFont.TorusAlternate.With(size: 14f, weight: FontWeight.Medium),
                            Margin = new MarginPadding { Horizontal = 12, Vertical = 7 },
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Shear = -OsuGame.SHEAR,
                        }
                    };
                }

                public void UpdateMultiSelectState()
                {
                    bool isSelected = multiSelectEzMode.IsSelected(Value);

                    // 直接更新 Active 状态以保持一致性
                    Active.Value = isSelected;

                    // 更新视觉状态
                    if (isSelected)
                    {
                        mainBackground.FadeColour(colourProvider.Colour2, 150, Easing.OutQuint);
                        text.FadeColour(colourProvider.Background6, 150, Easing.OutQuint);
                        hoverLayer.FadeOut(150, Easing.OutQuint);
                    }
                    else
                    {
                        mainBackground.FadeColour(colourProvider.Background5, 150, Easing.OutQuint);
                        text.FadeColour(Colour4.White, 150, Easing.OutQuint);
                        if (!IsHovered)
                            hoverLayer.FadeOut(150, Easing.OutQuint);
                    }
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    // 使用与ShearedDropdown菜单项相同的背景颜色
                    mainBackground.Colour = colourProvider.Background5;
                    hoverLayer.Colour = colourProvider.Background4;
                }

                protected override void OnActivated()
                {
                    mainBackground.FadeColour(colourProvider.Colour2, 150, Easing.OutQuint);
                    text.FadeColour(colourProvider.Background6, 150, Easing.OutQuint);
                    hoverLayer.FadeOut(150, Easing.OutQuint);
                }

                protected override void OnDeactivated()
                {
                    mainBackground.FadeColour(colourProvider.Background5, 150, Easing.OutQuint);
                    text.FadeColour(Colour4.White, 150, Easing.OutQuint);
                    if (!IsHovered)
                        hoverLayer.FadeOut(150, Easing.OutQuint);
                }

                protected override bool OnHover(HoverEvent e)
                {
                    if (!Active.Value)
                    {
                        mainBackground.FadeColour(colourProvider.Background4, 150, Easing.OutQuint);
                        hoverLayer.FadeTo(0.1f, 150, Easing.OutQuint);
                    }

                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    if (!Active.Value)
                    {
                        mainBackground.FadeColour(colourProvider.Background5, 150, Easing.OutQuint);
                        hoverLayer.FadeOut(150, Easing.OutQuint);
                    }

                    base.OnHoverLost(e);
                }

                protected override bool OnClick(ClickEvent e)
                {
                    // 根据开关状态决定是单选还是多选
                    bool isMultiSelect = isMultiSelectMode.Value;
                    multiSelectEzMode.ToggleSelection(Value, isMultiSelect);
                    return true;
                }

                private static string getDisplayText(EzSelectMode mode)
                {
                    return mode switch
                    {
                        EzSelectMode.All => "All",
                        EzSelectMode.Key4 => "4K",
                        EzSelectMode.Key5 => "5K",
                        EzSelectMode.Key6 => "6K",
                        EzSelectMode.Key7 => "7K",
                        EzSelectMode.Key8 => "8K",
                        EzSelectMode.Key9 => "9K",
                        EzSelectMode.Key10 => "10K",
                        EzSelectMode.Key12 => "12K",
                        EzSelectMode.Key14 => "14K",
                        EzSelectMode.Key16 => "16K",
                        EzSelectMode.Key18 => "18K",
                        _ => mode.ToString()
                    };
                }
            }
        }
    }
}
