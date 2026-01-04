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
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Extensions;
using osu.Game.Overlays.Settings;
using osu.Game.Screens;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class EzColumnTab : EditorSidebarSection
    {
        private static readonly List<int> available_key_modes = new List<int> { 0, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };

        private readonly Dictionary<int, List<EzSelectorColour>> columnSelectorCache = new Dictionary<int, List<EzSelectorColour>>();
        private readonly Dictionary<Ez2Setting, BindableColour4> colorBindables = new Dictionary<Ez2Setting, BindableColour4>();

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

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public EzColumnTab()
            : base("EZ Column Settings") { }

        [BackgroundDependencyLoader]
        private void load()
        {
            keyModeSelection = ezSkinConfig.GetBindable<int>(Ez2Setting.SelectedKeyMode);
            colorSettingsEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            columnBlur = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnBlur);
            columnDim = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnDim);

            colorBindables[Ez2Setting.ColumnTypeA] = createColorBindable(Ez2Setting.ColumnTypeA);
            colorBindables[Ez2Setting.ColumnTypeB] = createColorBindable(Ez2Setting.ColumnTypeB);
            colorBindables[Ez2Setting.ColumnTypeS] = createColorBindable(Ez2Setting.ColumnTypeS);
            colorBindables[Ez2Setting.ColumnTypeE] = createColorBindable(Ez2Setting.ColumnTypeE);
            colorBindables[Ez2Setting.ColumnTypeP] = createColorBindable(Ez2Setting.ColumnTypeP);
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
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_A, colorBindables[Ez2Setting.ColumnTypeA]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_B, colorBindables[Ez2Setting.ColumnTypeB]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_S, colorBindables[Ez2Setting.ColumnTypeS]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_E, colorBindables[Ez2Setting.ColumnTypeE]),
                    SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_P, colorBindables[Ez2Setting.ColumnTypeP]),
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
                Action = () =>
                {
                    skinManager.CurrentSkinInfo.TriggerChange();
                    ezSkinConfig.Save();
                },
            }.WithTwoLineText("(保存颜色设置)", "Save Color Settings");
        }

        private void setupEventHandlers()
        {
            colorSettingsEnabled.BindValueChanged(onColorSettingsEnabledChanged, true);
            keyModeSelection.BindValueChanged(e => updateColumnsType(e.NewValue));

            // 设置颜色变化事件
            colorBindables[Ez2Setting.ColumnTypeA].BindValueChanged(e => updateBaseColour(e.NewValue, Ez2Setting.ColumnTypeA, EzConstants.COLUMN_TYPE_A));
            colorBindables[Ez2Setting.ColumnTypeB].BindValueChanged(e => updateBaseColour(e.NewValue, Ez2Setting.ColumnTypeB, EzConstants.COLUMN_TYPE_B));
            colorBindables[Ez2Setting.ColumnTypeS].BindValueChanged(e => updateBaseColour(e.NewValue, Ez2Setting.ColumnTypeS, EzConstants.COLUMN_TYPE_S));
            colorBindables[Ez2Setting.ColumnTypeE].BindValueChanged(e => updateBaseColour(e.NewValue, Ez2Setting.ColumnTypeE, EzConstants.COLUMN_TYPE_E));
            colorBindables[Ez2Setting.ColumnTypeP].BindValueChanged(e => updateBaseColour(e.NewValue, Ez2Setting.ColumnTypeP, EzConstants.COLUMN_TYPE_P));
        }

        private BindableColour4 createColorBindable(Ez2Setting setting)
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

        private void updateBaseColour(Colour4 newColor, Ez2Setting setting, string type)
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
                [EzConstants.COLUMN_TYPE_A] = colorBindables[Ez2Setting.ColumnTypeA].Value,
                [EzConstants.COLUMN_TYPE_B] = colorBindables[Ez2Setting.ColumnTypeB].Value,
                [EzConstants.COLUMN_TYPE_S] = colorBindables[Ez2Setting.ColumnTypeS].Value,
                [EzConstants.COLUMN_TYPE_E] = colorBindables[Ez2Setting.ColumnTypeE].Value,
                [EzConstants.COLUMN_TYPE_P] = colorBindables[Ez2Setting.ColumnTypeP].Value
            };
        }

        private EzSelectorColour createColumnSelector(int keyMode, int columnIndex, string[] columnTypes, Dictionary<string, Color4> colorMapping)
        {
            EzColumnType savedType = ezSkinConfig.GetColumnType(keyMode, columnIndex);

            var selector = new EzSelectorColour($"Column {columnIndex + 1}", columnTypes, colorMapping);
            selector.Current.Value = savedType.ToString();

            selector.Current.ValueChanged += e =>
            {
                if (Enum.TryParse<EzColumnType>(e.NewValue, out var type))
                    ezSkinConfig.SetColumnType(keyMode, columnIndex, type);
            };

            return selector;
        }
    }
}
