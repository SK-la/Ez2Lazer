using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
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
    public partial class EzColumnTab : EditorSidebarSection
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

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        public EzColumnTab()
            : base("EZ Column Settings")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            keyModeSelection = ezSkinConfig.GetBindable<int>(EzSkinSetting.SelectedKeyMode);
            colorSettingsEnabled = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            // 设置双向绑定
            aColorValue = createColorBindable(EzSkinSetting.ColumnTypeA);
            bColorValue = createColorBindable(EzSkinSetting.ColumnTypeB);
            s1ColorValue = createColorBindable(EzSkinSetting.ColumnTypeS1);
            s2ColorValue = createColorBindable(EzSkinSetting.ColumnTypeS2);

            var baseColorsContainer = new FillFlowContainer
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
            };

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
                            LabelText = "Color Enable\n(着色设置)",
                            TooltipText = "仅支持EZ Style Pro皮肤. Only supports EZ Style Pro skin\n"
                                          + "切换tab栏或保存后, 将重置默认颜色为当前设置\n" +
                                          "Switching tabs or saving will reset the colors to the default values",
                            // Scale = new Vector2(0.9f),
                            Current = colorSettingsEnabled,
                        },
                        // 基础颜色选择器
                        baseColorsContainer,
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
                                    Colour = Color4.DarkGray.Opacity(0.5f),
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
            updateKeyModeFromCurrentBeatmap();

            colorSettingsEnabled.BindValueChanged(e =>
            {
                ezSkinConfig.SetValue(EzSkinSetting.ColorSettingsEnabled, e.NewValue);
                ezSkinConfig.Save();
                skinManager.Save(skinManager.CurrentSkin.Value);
                baseColorsContainer.Alpha = e.NewValue ? 1f : 0f;
            }, true);
        }

        private void updateKeyModeFromCurrentBeatmap()
        {
            if (beatmap.Value?.Beatmap == null)
                return;

            int currentKeyCount = 0;

            if (beatmap.Value.BeatmapInfo.Ruleset.OnlineID == 3)
            {
                currentKeyCount = (int)beatmap.Value.Beatmap.Difficulty.CircleSize;
            }

            // 如果无法获取键位数或键位数不在可用列表中，则不更改
            if (currentKeyCount > 0 && availableKeyModes.Contains(currentKeyCount))
                keyModeSelection.Value = currentKeyCount;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            aColorValue.ValueChanged += e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeA, "A");
            bColorValue.ValueChanged += e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeB, "B");
            s1ColorValue.ValueChanged += e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeS1, "S1");
            s2ColorValue.ValueChanged += e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeS2, "S2");
            keyModeSelection.ValueChanged += e => updateColumnsType(e.NewValue);

            updateColumnsType(keyModeSelection.Value);
        }

        private BindableColour4 createColorBindable(EzSkinSetting setting)
        {
            var configBindable = ezSkinConfig.GetBindable<Colour4>(setting);
            var result = new BindableColour4(configBindable.Value);

            configBindable.BindValueChanged(e => result.Value = e.NewValue);
            result.BindValueChanged(e => configBindable.Value = e.NewValue);

            return result;
        }

        private void updateBaseColour(Colour4 newColor, EzSkinSetting setting, string colorType)
        {
            ezSkinConfig.SetValue(setting, newColor);

            foreach (int keyMode in availableKeyModes)
            {
                for (int columnIndex = 0; columnIndex < keyMode; columnIndex++)
                {
                    string currentType = ezSkinConfig.GetColumnType(keyMode, columnIndex);

                    if (currentType == colorType)
                    {
                        ezSkinConfig.SetColumnType(keyMode, columnIndex, colorType);
                    }
                }
            }

            foreach (var selector in columnsContainer.ChildrenOfType<EzSelectorColour>())
            {
                selector.SetColorMapping(colorType, newColor);
            }

            ezSkinConfig.Save();
            Invalidate();
        }

        #region 创建选择器

        private void updateColumnsType(int keyMode)
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
                Text = $"{keyMode}K ColumnType 列类型",
                Font = OsuFont.GetFont(weight: FontWeight.SemiBold),
                Margin = new MarginPadding { Bottom = 5 },
            }.WithUnderline());

            var colorMapping = new Dictionary<string, Color4>
            {
                ["A"] = aColorValue.Value,
                ["B"] = bColorValue.Value,
                ["S1"] = s1ColorValue.Value,
                ["S2"] = s2ColorValue.Value
            };

            string[] currentColumnTypes = new string[keyMode];

            for (int i = 0; i < keyMode; i++)
            {
                currentColumnTypes[i] = ezSkinConfig.GetColumnType(keyMode, i);
            }

            for (int i = 0; i < keyMode; i++)
            {
                int columnIndex = i;
                string savedType = currentColumnTypes[columnIndex];

                if (string.IsNullOrEmpty(savedType))
                {
                    savedType = EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
                    currentColumnTypes[columnIndex] = savedType;
                    ezSkinConfig.SetColumnType(keyMode, columnIndex, savedType);
                }

                var selector = new EzSelectorColour($"Column {columnIndex + 1}", new[] { "A", "B", "S1", "S2" }, colorMapping);
                selector.Current.Value = savedType;

                selector.Current.ValueChanged += e =>
                {
                    ezSkinConfig.SetColumnType(keyMode, columnIndex, e.NewValue);
                    ezSkinConfig.Save();
                    // Invalidate();
                };

                columnsContainer.Add(selector);
            }
        }

        #endregion
    }
}
