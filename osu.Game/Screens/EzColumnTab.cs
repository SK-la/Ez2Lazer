using System;
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
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens
{
    public partial class EzColumnTab : EditorSidebarSection
    {
        private static readonly List<int> available_key_modes = new List<int> { 0, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };

        private readonly Dictionary<int, List<EzSelectorColour>> columnSelectorCache = new Dictionary<int, List<EzSelectorColour>>();
        private readonly Dictionary<EzSkinSetting, BindableColour4> colorBindables = new Dictionary<EzSkinSetting, BindableColour4>();

        private FillFlowContainer columnsContainer = null!;
        private FillFlowContainer baseColorsContainer = null!;

        private Bindable<int> keyModeSelection = null!;
        private Bindable<bool> colorSettingsEnabled = null!;
        private Bindable<double> columnBlur = new Bindable<double>();
        private Bindable<double> columnDim = new Bindable<double>();

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        public EzColumnTab()
            : base("EZ Column Settings") { }

        [BackgroundDependencyLoader]
        private void load()
        {
            keyModeSelection = ezSkinConfig.GetBindable<int>(EzSkinSetting.SelectedKeyMode);
            colorSettingsEnabled = ezSkinConfig.GetBindable<bool>(EzSkinSetting.ColorSettingsEnabled);
            columnBlur = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnBlur);
            columnDim = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnDim);

            colorBindables[EzSkinSetting.ColumnTypeA] = createColorBindable(EzSkinSetting.ColumnTypeA);
            colorBindables[EzSkinSetting.ColumnTypeB] = createColorBindable(EzSkinSetting.ColumnTypeB);
            colorBindables[EzSkinSetting.ColumnTypeS] = createColorBindable(EzSkinSetting.ColumnTypeS);
            colorBindables[EzSkinSetting.ColumnTypeE] = createColorBindable(EzSkinSetting.ColumnTypeE);
            colorBindables[EzSkinSetting.ColumnTypeP] = createColorBindable(EzSkinSetting.ColumnTypeP);
            createUI();
            updateKeyModeFromCurrentBeatmap();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            setupEventHandlers();
            updateColumnsType(keyModeSelection.Value);
        }

        private void createUI()
        {
            baseColorsContainer = createBaseColorsContainer();

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
                        new SettingsSlider<double>
                        {
                            LabelText = "Column Dim",
                            TooltipText = "修改面板背景暗化",
                            Current = columnDim,
                            KeyboardStep = 0.01f,
                            DisplayAsPercentage = true
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Column Blur",
                            TooltipText = "修改面板背景虚化",
                            Current = columnBlur,
                            KeyboardStep = 0.01f,
                            DisplayAsPercentage = true
                        },
                        createColorSettingsCheckbox(),
                        baseColorsContainer,
                        createKeyModeSection(),
                        createSaveButton()
                    }
                }
            };
        }

        private SettingsCheckbox createColorSettingsCheckbox()
        {
            return new SettingsCheckbox
            {
                LabelText = "Color Enable\n(着色设置)",
                TooltipText = "仅支持EZ Style Pro皮肤. Only supports EZ Style Pro skin\n" +
                              "切换tab栏或保存后, 将重置默认颜色为当前设置\n" +
                              "Switching tabs or saving will reset the colors to the default values",
                Current = colorSettingsEnabled,
            };
        }

        private FillFlowContainer createBaseColorsContainer()
        {
            return new FillFlowContainer
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
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_A, colorBindables[EzSkinSetting.ColumnTypeA]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_B, colorBindables[EzSkinSetting.ColumnTypeB]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_S, colorBindables[EzSkinSetting.ColumnTypeS]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_E, colorBindables[EzSkinSetting.ColumnTypeE]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_P, colorBindables[EzSkinSetting.ColumnTypeP]),
                }
            };
        }

        private FillFlowContainer createKeyModeSection()
        {
            return new FillFlowContainer
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
                        Items = available_key_modes
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 2,
                        Colour = Color4.DarkGray.Opacity(0.5f),
                    },
                    columnsContainer = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(2)
                    },
                }
            };
        }

        private SettingsButton createSaveButton()
        {
            return new SettingsButton
            {
                Action = () => ezSkinConfig.Save(),
            }.WithTwoLineText("(保存颜色设置)", "Save Color Settings");
        }

        private void setupEventHandlers()
        {
            colorSettingsEnabled.BindValueChanged(onColorSettingsEnabledChanged, true);
            keyModeSelection.BindValueChanged(e => updateColumnsType(e.NewValue));

            // 设置颜色变化事件
            colorBindables[EzSkinSetting.ColumnTypeA].BindValueChanged(e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeA, EzConstants.COLUMN_TYPE_A));
            colorBindables[EzSkinSetting.ColumnTypeB].BindValueChanged(e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeB, EzConstants.COLUMN_TYPE_B));
            colorBindables[EzSkinSetting.ColumnTypeS].BindValueChanged(e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeS, EzConstants.COLUMN_TYPE_S));
            colorBindables[EzSkinSetting.ColumnTypeE].BindValueChanged(e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeE, EzConstants.COLUMN_TYPE_E));
            colorBindables[EzSkinSetting.ColumnTypeP].BindValueChanged(e => updateBaseColour(e.NewValue, EzSkinSetting.ColumnTypeP, EzConstants.COLUMN_TYPE_P));
        }

        private BindableColour4 createColorBindable(EzSkinSetting setting)
        {
            var configBindable = ezSkinConfig.GetBindable<Colour4>(setting);
            var result = new BindableColour4(configBindable.Value);

            configBindable.BindValueChanged(e => result.Value = e.NewValue);
            result.BindValueChanged(e => configBindable.Value = e.NewValue);

            return result;
        }

        private void onColorSettingsEnabledChanged(ValueChangedEvent<bool> e)
        {
            baseColorsContainer.Alpha = e.NewValue ? 1f : 0f;
        }

        private void updateBaseColour(Colour4 newColor, EzSkinSetting setting, string type)
        {
            if (!colorSettingsEnabled.Value)
                return;

            ezSkinConfig.SetValue(setting, newColor);

            foreach (var selector in columnsContainer.ChildrenOfType<EzSelectorColour>())
            {
                selector.SetColorMapping(type, newColor);
            }
        }

        private void updateKeyModeFromCurrentBeatmap()
        {
            if (beatmap.Value?.Beatmap == null)
                return;

            if (beatmap.Value.BeatmapInfo.Ruleset.OnlineID == 3)
            {
                int currentKeyCount = (int)beatmap.Value.Beatmap.Difficulty.CircleSize;

                if (currentKeyCount > 0 && available_key_modes.Contains(currentKeyCount))
                {
                    keyModeSelection.Value = currentKeyCount;
                }
            }
        }

        private void updateColumnsType(int keyMode)
        {
            if (columnSelectorCache.TryGetValue(keyMode, out var cachedSelectors))
            {
                columnsContainer.Clear();
                columnsContainer.AddRange(cachedSelectors);
                return;
            }

            columnsContainer.Clear();

            if (keyMode == 0 || !available_key_modes.Contains(keyMode))
            {
                columnsContainer.Add(new OsuSpriteText
                {
                    Text = "请先选择键位数模式",
                    Font = OsuFont.GetFont(weight: FontWeight.Bold),
                    Margin = new MarginPadding(5f),
                });
                return;
            }

            createColumnSelectors(keyMode);
        }

        private void createColumnSelectors(int keyMode)
        {
            columnsContainer.Add(new OsuSpriteText
            {
                Text = $"{keyMode}K ColumnType 列类型",
                Font = OsuFont.GetFont(weight: FontWeight.SemiBold),
                Margin = new MarginPadding { Bottom = 5 },
            }.WithUnderline());

            var newSelectors = new List<EzSelectorColour>();
            var colorMapping = createColorMapping();
            string[] columnTypes = Enum.GetNames(typeof(EzColumnType));

            for (int i = 0; i < keyMode; i++)
            {
                var selector = createColumnSelector(keyMode, i, columnTypes, colorMapping);
                columnsContainer.Add(selector);
                newSelectors.Add(selector);
            }

            columnSelectorCache[keyMode] = newSelectors;
        }

        private Dictionary<string, Color4> createColorMapping()
        {
            return new Dictionary<string, Color4>
            {
                [EzConstants.COLUMN_TYPE_A] = colorBindables[EzSkinSetting.ColumnTypeA].Value,
                [EzConstants.COLUMN_TYPE_B] = colorBindables[EzSkinSetting.ColumnTypeB].Value,
                [EzConstants.COLUMN_TYPE_S] = colorBindables[EzSkinSetting.ColumnTypeS].Value,
                [EzConstants.COLUMN_TYPE_E] = colorBindables[EzSkinSetting.ColumnTypeE].Value,
                [EzConstants.COLUMN_TYPE_P] = colorBindables[EzSkinSetting.ColumnTypeP].Value
            };
        }

        private EzSelectorColour createColumnSelector(int keyMode, int columnIndex, string[] columnTypes, Dictionary<string, Color4> colorMapping)
        {
            string savedType = ezSkinConfig.GetColumnType(keyMode, columnIndex);

            if (string.IsNullOrEmpty(savedType))
            {
                savedType = EzColumnTypeManager.GetColumnType(keyMode, columnIndex);
                ezSkinConfig.SetColumnType(keyMode, columnIndex, savedType);
            }

            var selector = new EzSelectorColour($"Column {columnIndex + 1}", columnTypes, colorMapping);
            selector.Current.Value = savedType;

            selector.Current.ValueChanged += e =>
            {
                ezSkinConfig.SetColumnType(keyMode, columnIndex, e.NewValue);
            };

            return selector;
        }
    }
}
