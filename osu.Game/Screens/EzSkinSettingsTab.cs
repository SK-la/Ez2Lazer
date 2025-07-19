// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens
{
    public partial class EzSkinSettings : EditorSidebarSection
    {
        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzSkinSettings()
            : base("EZ Skin Settings") { }

        private static readonly Dictionary<string, string> resource_paths = new Dictionary<string, string>
        {
            ["note"] = @"EzResources\note",
            ["Stage"] = @"EzResources\Stage"
        };

        private static readonly Dictionary<bool, (Color4 Color, string TopText, string BottomText)> position_mode_config = new Dictionary<bool, (Color4 Color, string TopText, string BottomText)>
        {
            [true] = (new Color4(0.2f, 0.4f, 0.8f, 0.3f), "强制刷新, 并切换至 绝对位置", "Refresh, Switch to Absolute"),
            [false] = (new Color4(0.8f, 0.2f, 0.4f, 0.3f), "强制刷新, 并切换至 相对位置", "Refresh, Switch to Relative")
        };

        private readonly Bindable<bool> globalHitPosition = new BindableBool();
        private readonly Bindable<EzSelectorNameSet> globalTextureName = new Bindable<EzSelectorNameSet>((EzSelectorNameSet)4);
        private readonly Bindable<string> selectedNoteSet = new Bindable<string>();
        private readonly Bindable<string> selectedStageSet = new Bindable<string>();

        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();

        private SettingsButton refreshSkinButton = null!;
        private ScheduledDelegate? scheduledRefresh;
        private bool isAbsolutePosition = true;

        public Bindable<double> HitPosition => ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
        public Bindable<double> ColumnWidth => ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
        public Bindable<double> SpecialFactor => ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
        public Bindable<double> NoteHeightScaleToWidth => ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);

        [BackgroundDependencyLoader]
        private void load()
        {
            globalHitPosition.Value = ezSkinConfig.GetBindable<bool>(EzSkinSetting.GlobalHitPosition).Value;
            globalTextureName.Value = (EzSelectorNameSet)ezSkinConfig.GetBindable<int>(EzSkinSetting.GlobalTextureName).Value;
            loadFolderSets("note");
            loadFolderSets("Stage");

            // 设置默认值
            setDefaultSelection(selectedNoteSet, availableNoteSets, ezSkinConfig.Get<string>(EzSkinSetting.NoteSetName));
            setDefaultSelection(selectedStageSet, availableStageSets, ezSkinConfig.Get<string>(EzSkinSetting.StageName));
            createUI();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            globalTextureName.BindValueChanged(OnNameSetChanged);
            selectedNoteSet.BindValueChanged(e => ezSkinConfig.SetValue(EzSkinSetting.NoteSetName, e.NewValue));
            selectedStageSet.BindValueChanged(e => ezSkinConfig.SetValue(EzSkinSetting.StageName, e.NewValue));
        }

        // public Bindable<float> GetNoteSize(int keyMode, int columnIndex, int x = 0)
        // {
        //     bool isSpecialColumn = ezSkinConfig.GetColumnType(keyMode, columnIndex) == "S";
        //     double baseWidth = ColumnWidth.Value;
        //     double specialFactor = SpecialFactor.Value;
        //     double columnWidth = (baseWidth * (isSpecialColumn ? specialFactor : 1.0));
        //     NoteSize.Value = (float)columnWidth;
        //
        //     if (x != 0)
        //         return NoteSize;
        //
        //     double heightScale = NoteHeightScaleToWidth.Value;
        //     NoteSize.Value = (float)(columnWidth * heightScale);
        //     return new Bindable<float>((float)(columnWidth * heightScale));
        // }

        private void setDefaultSelection(Bindable<string> bindable, List<string> availableItems, string configuredValue)
        {
            if (!string.IsNullOrEmpty(configuredValue) && availableItems.Contains(configuredValue))
            {
                bindable.Value = configuredValue;
            }
            else if (availableItems.Count > 1)
            {
                bindable.Value = availableItems[1];
            }
        }

        private void createUI()
        {
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
                        new EzGlobalTextureNameSelector
                        {
                            LabelText = "Global Texture Name",
                            TooltipText = "(全局纹理名称)统一修改当前皮肤中所有组件的纹理名称\n"
                                          + "Set a global texture name for all components in the current skin",
                            Current = globalTextureName,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "(面板套图)Stage Set",
                            TooltipText = "统一指定Stage Bottom, 关联实时BPM\n"
                                          + "Set a stage set for Stage Bottom, related to real-time BPM",
                            Current = selectedStageSet,
                            Items = availableStageSets,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "(Note套图)Note Set",
                            TooltipText = "统一指定note套图, 含note和打击光效\n"
                                          + "Set a note set for all notes and hit effects",
                            Current = selectedNoteSet,
                            Items = availableNoteSets,
                        },
                        new SettingsEnumDropdown<EzColumnWidthStyle>
                        {
                            LabelText = "Column Width Style(列宽风格)",
                            TooltipText = "不完善！全局总列宽=设置值×10\n"
                                          + "Global Total Column Width = Configured Value × 10",
                            Current = ezSkinConfig.GetBindable<EzColumnWidthStyle>(EzSkinSetting.ColumnWidthStyle),
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Column Width (轨道宽度)",
                            TooltipText = "设置每列的宽度",
                            Current = ColumnWidth,
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Special Factor (特殊轨道倍率)",
                            TooltipText = "特殊列关联S1类型, 可自定义"
                                          + "\nSpecial columns are associated with S1 type, customizable",
                            Current = SpecialFactor,
                            KeyboardStep = 0.1f,
                        },
                        new SettingsCheckbox
                        {
                            LabelText = "Global HitPosition",
                            TooltipText = "全局判定线位置开关",
                            Current = globalHitPosition,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Hit Position (可视判定线位置)",
                            TooltipText = "设置判定线位置",
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition),
                            KeyboardStep = 1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Note Height Scale (note 高度比例)",
                            TooltipText = "统一修改note的高度的比例\n"
                                          + "Fixed Height for square notes",
                            Current = NoteHeightScaleToWidth,
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Note Track Line",
                            TooltipText = "note两侧辅助轨道线的高度\n"
                                          + "note side auxiliary track line height",
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteTrackLineHeight),
                        },
                        refreshSkinButton = new SettingsButton
                        {
                            Action = RefreshSkin,
                            TooltipText = "1、强制刷新、保存皮肤,\n"
                                          + "2、切换判定线标高为 绝对/相对位置,\n"
                                          + "3、切换EzCom组件资源为 默认/设定 \n"
                                          + "1. Refresh & Save Skin, \n"
                                          + "2. Switch HitPosition to Absolute/Relative Position,\n"
                                          + "3. Switch EzCom's Sprite to Default/Configured",
                        }.WithTwoLineText("强制刷新, 并切换 绝对/相对位置", "Switch Absolute/Relative", 16)
                    }
                }
            };
        }

        #region 追踪处理

        private void OnNameSetChanged(ValueChangedEvent<EzSelectorNameSet> textureName)
        {
            updateAllEzTextureNames(textureName.NewValue);
        }

        public void ScheduleRefresh()
        {
            scheduledRefresh?.Cancel();
            scheduledRefresh = Scheduler.AddDelayed(RefreshSkin, 50);
        }

        public void RefreshSkin()
        {
            isAbsolutePosition = !isAbsolutePosition;
            skinManager.CurrentSkinInfo.TriggerChange();
            // skinManager.Save(skinManager.CurrentSkin.Value);
            updateButtonAppearance();
        }

        private void updateButtonAppearance()
        {
            var config = position_mode_config[isAbsolutePosition];

            var box = refreshSkinButton.ChildrenOfType<Box>().FirstOrDefault();
            box?.FadeColour(config.Color, 200);

            var textContainer = refreshSkinButton.ChildrenOfType<FillFlowContainer>().FirstOrDefault();
            var texts = textContainer?.ChildrenOfType<OsuSpriteText>().ToArray();

            if (texts?.Length >= 2)
            {
                texts[0].Text = config.TopText;
                texts[1].Text = config.BottomText;
            }
        }

        #endregion

        #region 刷新所有EzComponent的纹理名称

        private void updateAllEzTextureNames(EzSelectorNameSet textureName)
        {
            var root = findRootDrawable();

            var scoreTexts = root.ChildrenOfType<EzScoreText>();
            var comboTexts = root.ChildrenOfType<EzComboText>();
            var hitResultScores = root.ChildrenOfType<EzComHitResultScore>();

            foreach (var scoreText in scoreTexts)
                scoreText.FontName.Value = textureName;

            foreach (var comboText in comboTexts)
                comboText.FontName.Value = textureName;

            foreach (var hitResultScore in hitResultScores)
                hitResultScore.NameDropdown.Value = textureName;
        }

        private Drawable findRootDrawable()
        {
            var root = this as Drawable;
            while (root.Parent != null)
                root = root.Parent;
            return root;
        }

        #endregion

        private void loadFolderSets(string type)
        {
            List<string> targetList = type.Equals("note", StringComparison.OrdinalIgnoreCase) ? availableNoteSets : availableStageSets;
            targetList.Clear();

            if (!resource_paths.TryGetValue(type, out string? relativePath))
            {
                Logger.Log($"Unknown resource type: {type}", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            try
            {
                string? dataFolderPath = storage.GetFullPath(relativePath);
                // Debug.Assert(!string.IsNullOrEmpty(dataFolderPath));

                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    Logger.Log($"EzSkinTab create {type} Path: {dataFolderPath}");
                }

                string[] directories = Directory.GetDirectories(dataFolderPath);
                targetList.AddRange(directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);

                Logger.Log($"Found {targetList.Count} {type} sets in {dataFolderPath}", LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"EzSkinTab Load {type} FolderSets Error");
            }
        }
    }

    #region 拓展按钮的多行文本显示

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

    public partial class EzGlobalTextureNameSelector : EzSelectorEnumList;

    #endregion
}
