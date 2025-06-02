using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.LAsEzExtensions;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens
{
    public partial class EzColumnColorTab : EditorSidebarSection
    {
        private readonly List<int> availableKeyModes = new List<int> { 0, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };
        private FillFlowContainer columnsContainer = null!;
        private Bindable<int> keyModeSelection = new Bindable<int>();
        private Bindable<bool> colorSettingsEnabled = new BindableBool(true);
        private BindableColour4 aColorValue = new BindableColour4();
        private BindableColour4 bColorValue = new BindableColour4();
        private BindableColour4 s1ColorValue = new BindableColour4();
        private BindableColour4 s2ColorValue = new BindableColour4();

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public EzColumnColorTab()
            : base("EZ Colour Settings")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            keyModeSelection = ezSkinConfig.GetBindable<int>(EzSkinSetting.SelectedKeyMode);
            colorSettingsEnabled = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            // 设置双向绑定
            aColorValue = createColorBindable(EzSkinSetting.AColorValue);
            bColorValue = createColorBindable(EzSkinSetting.BColorValue);
            s1ColorValue = createColorBindable(EzSkinSetting.Special1ColorValue);
            s2ColorValue = createColorBindable(EzSkinSetting.Special2ColorValue);

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(2),
                    Children = new Drawable[]
                    {
                        new SettingsCheckbox
                        {
                            LabelText = "Enable (启动设置)",
                            TooltipText = "当切换tab栏或点保存按钮后，当前颜色设置会变成设置默认值，影响重置目标\n" +
                                          "When switching or save tabs , the current setting becomes the default value, affecting the reset target",
                            // Scale = new Vector2(0.9f),
                            Current = colorSettingsEnabled,
                        },
                        // 基础颜色选择器
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Margin = new MarginPadding(5f),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = "Base Colors (基础颜色)",
                                    Margin = new MarginPadding { Bottom = 5 },
                                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14)
                                }.WithUnderline(),
                                SettingsColourExtensions.CreateStyledSettingsColour("A", aColorValue),
                                SettingsColourExtensions.CreateStyledSettingsColour("B", bColorValue),
                                SettingsColourExtensions.CreateStyledSettingsColour("S1", s1ColorValue),
                                SettingsColourExtensions.CreateStyledSettingsColour("S2", s2ColorValue),
                            }
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Margin = new MarginPadding(5f),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = "Key Mode (键位数)",
                                    Margin = new MarginPadding { Bottom = 5 },
                                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                                }.WithUnderline(),
                                new SettingsDropdown<int>
                                {
                                    Current = keyModeSelection,
                                    Items = availableKeyModes
                                },
                                new Box
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 2,
                                    Colour = Color4.Gray.Opacity(0.5f),
                                },
                                // 列颜色设置容器
                                columnsContainer = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(2)
                                },
                            }
                        },
                        new SettingsButton
                        {
                            Action = () =>
                            {
                                ezSkinConfig.Save();
                                skinManager.CurrentSkinInfo.TriggerChange();
                            },
                        }.WithTwoLineText("(保存颜色设置)", "Save Color Settings")
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            colorSettingsEnabled.ValueChanged += e =>
            {
                ezSkinConfig.SetValue(EzSkinSetting.ColorSettingsEnabled, e.NewValue);
                ezSkinConfig.Save();
                skinManager.CurrentSkinInfo.TriggerChange();
            };
            aColorValue.ValueChanged += e => updateBaseColorSettings(e.NewValue, EzSkinSetting.AColorValue, "A");
            bColorValue.ValueChanged += e => updateBaseColorSettings(e.NewValue, EzSkinSetting.BColorValue, "B");
            s1ColorValue.ValueChanged += e => updateBaseColorSettings(e.NewValue, EzSkinSetting.Special1ColorValue, "S1");
            s2ColorValue.ValueChanged += e => updateBaseColorSettings(e.NewValue, EzSkinSetting.Special2ColorValue, "S2");
            keyModeSelection.ValueChanged += e => updateColumnsForKeyMode(e.NewValue);

            updateColumnsForKeyMode(keyModeSelection.Value);
        }

        private BindableColour4 createColorBindable(EzSkinSetting setting)
        {
            var configBindable = ezSkinConfig.GetBindable<Colour4>(setting);
            var result = new BindableColour4(configBindable.Value);

            configBindable.BindValueChanged(e => result.Value = e.NewValue);
            result.BindValueChanged(e => configBindable.Value = e.NewValue);

            return result;
        }

        private void updateBaseColorSettings(Colour4 newColor, EzSkinSetting setting, string colorType)
        {
            ezSkinConfig.SetValue(setting.ToString(), newColor.ToHex());

            foreach (int keyMode in availableKeyModes)
            {
                for (int columnIndex = 0; columnIndex < keyMode; columnIndex++)
                {
                    string keyName = $"{keyMode}K_{columnIndex}";
                    string fullKey = $"{EzSkinSetting.ColumnColorPrefix}:{keyName}";
                    string typeKey = $"{fullKey}_type";

                    if (ezSkinConfig.Get<string>(typeKey) == colorType)
                    {
                        ezSkinConfig.SetValue(fullKey, newColor.ToHex());
                    }
                }
            }

            foreach (var selector in columnsContainer.ChildrenOfType<EzColorButtonSelector>())
            {
                selector.SetColorMapping(colorType, newColor);
            }

            ezSkinConfig.Save();
            Invalidate();
        }

        #region 创建选择器

        private void updateColumnsForKeyMode(int keyMode)
        {
            columnsContainer.Clear();

            if (keyMode == 0 || !availableKeyModes.Contains(keyMode))
            {
                columnsContainer.Add(new OsuSpriteText
                {
                    Text = "请先选择键位数模式",
                    Font = OsuFont.GetFont(weight: FontWeight.Bold),
                    Margin = new MarginPadding(5f),
                });
                return;
            }

            columnsContainer.Add(new OsuSpriteText
            {
                Text = $"{keyMode}K 模式列颜色设置",
                Font = OsuFont.GetFont(weight: FontWeight.Bold),
                Margin = new MarginPadding { Bottom = 5 },
            }.WithUnderline());

            var colorMapping = new Dictionary<string, Color4>
            {
                ["A"] = aColorValue.Value,
                ["B"] = bColorValue.Value,
                ["S1"] = s1ColorValue.Value,
                ["S2"] = s2ColorValue.Value
            };

            for (int i = 0; i < keyMode; i++)
            {
                int columnIndex = i;
                string keyName = $"{keyMode}K_{columnIndex}";
                string fullKey = $"{EzSkinSetting.ColumnColorPrefix}:{keyName}";
                string typeKey = $"{fullKey}_type";

                string savedType = ezSkinConfig.Get<string>(typeKey);

                if (string.IsNullOrEmpty(savedType))
                {
                    savedType = columnIndex % 2 == 0 ? "A" : "B";
                    ezSkinConfig.SetValue(typeKey, savedType);
                }

                var selector = new EzColorButtonSelector($"Column {columnIndex + 1}", new[] { "A", "B", "S1", "S2" }, colorMapping);
                selector.Current.Value = savedType;

                selector.Current.ValueChanged += e =>
                {
                    ezSkinConfig.SetValue(typeKey, e.NewValue);

                    Color4 selectedColor = e.NewValue switch
                    {
                        "A" => aColorValue.Value,
                        "B" => bColorValue.Value,
                        "S1" => s1ColorValue.Value,
                        "S2" => s2ColorValue.Value,
                        _ => Color4.White
                    };

                    ezSkinConfig.SetValue(fullKey, selectedColor.ToHex());
                    ezSkinConfig.Save();

                    Invalidate();
                };

                columnsContainer.Add(selector);
            }
        }

        #endregion
    }
}
