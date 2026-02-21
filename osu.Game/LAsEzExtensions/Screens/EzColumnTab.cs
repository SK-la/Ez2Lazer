// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        private Bindable<int> columnTypeListSelectBindable = null!;
        private Bindable<bool> colorSettingsEnabled = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public EzColumnTab()
            : base("EZ Column Settings") { }

        [BackgroundDependencyLoader]
        private void load()
        {
            colorSettingsEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            columnTypeListSelectBindable = ezSkinConfig.GetBindable<int>(Ez2Setting.ColumnTypeListSelect);

            colorBindables[Ez2Setting.ColumnTypeA] = createColorBindable(Ez2Setting.ColumnTypeA);
            colorBindables[Ez2Setting.ColumnTypeB] = createColorBindable(Ez2Setting.ColumnTypeB);
            colorBindables[Ez2Setting.ColumnTypeS] = createColorBindable(Ez2Setting.ColumnTypeS);
            colorBindables[Ez2Setting.ColumnTypeE] = createColorBindable(Ez2Setting.ColumnTypeE);
            colorBindables[Ez2Setting.ColumnTypeP] = createColorBindable(Ez2Setting.ColumnTypeP);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.MANIA_PSEUDO_3D_ROTATION,
                        TooltipText = EzLocalizationManager.MANIA_PSEUDO_3D_ROTATION_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaPseudo3DRotation),
                        KeyboardStep = 1f,
                        DisplayAsPercentage = false
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.STAGE_BACKGROUND_DIM,
                        TooltipText = EzLocalizationManager.STAGE_BACKGROUND_DIM_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnDim),
                        KeyboardStep = 0.01f,
                        DisplayAsPercentage = true
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzLocalizationManager.STAGE_BACKGROUND_BLUR,
                        TooltipText = EzLocalizationManager.STAGE_BACKGROUND_BLUR_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnBlur),
                        KeyboardStep = 0.01f,
                        DisplayAsPercentage = true
                    },
                    new SettingsCheckbox
                    {
                        LabelText = EzLocalizationManager.STAGE_PANEL,
                        TooltipText = EzLocalizationManager.STAGE_PANEL_TOOLTIP,
                        Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.StagePanelEnabled),
                    },
                    new SettingsCheckbox
                    {
                        LabelText = EzLocalizationManager.COLOUR_ENABLE_BUTTON,
                        TooltipText = EzLocalizationManager.COLOUR_ENABLE_BUTTON_TOOLTIP,
                        Current = colorSettingsEnabled,
                    },
                    baseColorsContainer = new FillFlowContainer
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
                                Current = columnTypeListSelectBindable,
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
                    },
                    new SettingsButton
                    {
                        Text = EzLocalizationManager.SAVE_COLOUR_BUTTON,
                        TooltipText = EzLocalizationManager.SAVE_COLOUR_BUTTON_TOOLTIP,
                        Action = () =>
                        {
                            skinManager.CurrentSkinInfo.TriggerChange();
                            ezSkinConfig.Save();
                        },
                    }
                }
            };

            updateKeyModeFromCurrentBeatmap();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            colorSettingsEnabled.BindValueChanged(onColorSettingsEnabledChanged, true);
            columnTypeListSelectBindable.BindValueChanged(e => updateColumnsType(e.NewValue), true);

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

            result.BindTo(configBindable);

            return result;
        }

        private void onColorSettingsEnabledChanged(ValueChangedEvent<bool> e)
        {
            if (e.NewValue)
            {
                baseColorsContainer.Show();
                updateColumnsType(columnTypeListSelectBindable.Value);
            }
            else
                baseColorsContainer.Hide();
        }

        private void updateBaseColour(Colour4 newColor, Ez2Setting setting, string type)
        {
            if (!colorSettingsEnabled.Value)
                return;

            // BindTo 已经处理了双向绑定，不需要手动 SetValue
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
                    columnTypeListSelectBindable.Value = currentKeyCount;
                }
            }
        }

        private void updateColumnsType(int keyModeForList)
        {
            if (columnSelectorCache.TryGetValue(keyModeForList, out var cachedSelectors))
            {
                // 使用 Clear(false) 避免释放子控件
                columnsContainer.Clear(false);
                columnsContainer.AddRange(cachedSelectors);

                // 更新缓存选择器的颜色映射
                var colorMapping = createColorMapping();

                foreach (var selector in cachedSelectors)
                {
                    selector.UpdateColorMapping(colorMapping);
                }

                return;
            }

            // 清理旧内容但不释放（可能是其他键位模式的缓存）
            columnsContainer.Clear(false);

            if (keyModeForList == 0 || !available_key_modes.Contains(keyModeForList))
            {
                columnsContainer.Add(new OsuSpriteText
                {
                    Text = "请先选择键位数模式",
                    Font = OsuFont.GetFont(weight: FontWeight.Bold),
                    Margin = new MarginPadding(5f),
                });
                return;
            }

            createColumnSelectors(keyModeForList);
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
