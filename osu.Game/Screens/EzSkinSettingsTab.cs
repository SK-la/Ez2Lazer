// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Bindable<double>? hitPosition;
        private Bindable<double>? columnWidth;
        private Bindable<double>? specialFactor;
        private readonly Bindable<bool> dynamicTracking = new Bindable<bool>();
        private readonly Bindable<EzSelectorNameSet> globalTextureName = new Bindable<EzSelectorNameSet>((EzSelectorNameSet)4);
        private readonly Bindable<string> selectedNoteSet = new Bindable<string>();
        private readonly List<string> availableNoteSets = new List<string>();

        private SettingsButton refreshSkinButton = null!;
        private bool isAbsolutePosition = true;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzSkinSettings()
            : base("EZ Skin Settings")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            globalTextureName.Value = (EzSelectorNameSet)ezSkinConfig.GetBindable<int>(EzSkinSetting.GlobalTextureName).Value;
            // dynamicTracking.BindTo(ezSkinConfig.GetBindable<bool>(EzSkinSetting.DynamicTracking));
            // NonSquareNoteHeight.ValueChanged += onSettingsValueChanged;

            loadAvailableNoteSets();
            string configuredNoteSet = ezSkinConfig.Get<string>(EzSkinSetting.NoteSetName);
            if (!string.IsNullOrEmpty(configuredNoteSet) && availableNoteSets.Contains(configuredNoteSet))
                selectedNoteSet.Value = configuredNoteSet;
            else if (availableNoteSets.Count > 0)
                selectedNoteSet.Value = availableNoteSets[1];

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
                            LabelText = "Dynamic Tracking\n(动态刷新)",
                            TooltipText = "开启后, 调整滑块时会实时 刷新并保存 皮肤, 可能导致卡顿\n"
                                          + "Enable this to refresh and save the skin in real-time when adjusting sliders, may cause lag",
                            Current = ezSkinConfig.GetBindable<bool>(EzSkinSetting.DynamicTracking),
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Column Width",
                            TooltipText = "设置每列的宽度",
                            Current = columnWidth,
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Special Factor (特殊列倍率)",
                            TooltipText = "设置特殊列的宽度倍率 //未来会联动特殊列颜色定义"
                                          + "\nSet the width factor for special columns",
                            Current = specialFactor,
                            KeyboardStep = 0.1f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Hit Position",
                            TooltipText = "设置判定线位置",
                            Current = hitPosition,
                            KeyboardStep = 1f,
                        },
                        new EzGlobalTextureNameSelector
                        {
                            LabelText = "Global Texture Name",
                            TooltipText = "(全局纹理名称)统一修改当前皮肤中所有组件的纹理名称\n"
                                          + "Set a global texture name for all components in the current skin",
                            Current = globalTextureName,
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "(Note套图)Note Set",
                            TooltipText = "统一指定note套图, 含note和打击光效\n"
                                          + "Set a note set for all notes and hit effects",
                            Current = selectedNoteSet,
                            Items = availableNoteSets,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "(note高)Note Height",
                            TooltipText = "统一修改非圆形note的高度\n"
                                          + "Fixed Height for square notes",
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight),
                            KeyboardStep = 1.0f,
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

            // columnWidth.ValueChanged += onStageChanged;
            // specialFactor.ValueChanged += onStageChanged;
            // hitPosition.ValueChanged += onStageChanged;
            globalTextureName.ValueChanged += onTextureNameChanged;
            selectedNoteSet.ValueChanged += onNoteSetChanged;
            dynamicTracking.ValueChanged += tracking =>
            {
                if (tracking.NewValue)
                {
                    columnWidth!.ValueChanged += onSettingsValueChanged;
                    specialFactor!.ValueChanged += onSettingsValueChanged;
                    hitPosition!.ValueChanged += onSettingsValueChanged;
                }
                else
                {
                    columnWidth!.ValueChanged -= onSettingsValueChanged;
                    specialFactor!.ValueChanged -= onSettingsValueChanged;
                    hitPosition!.ValueChanged -= onSettingsValueChanged;
                }

                ezSkinConfig.SetValue(EzSkinSetting.DynamicTracking, tracking.NewValue);
            };
        }

        #region 追踪处理

        // private void onStageChanged(ValueChangedEvent<double> e)
        // {
        //     Invalidate();
        // }

        private void onTextureNameChanged(ValueChangedEvent<EzSelectorNameSet> textureName)
        {
            updateAllSkinComponentsTextureNames(textureName.NewValue);

            ezSkinConfig.SetValue(EzSkinSetting.GlobalTextureName, (int)textureName.NewValue);
        }

        private void onNoteSetChanged(ValueChangedEvent<string> e)
        {
            ezSkinConfig.SetValue(EzSkinSetting.NoteSetName, e.NewValue);

            if (dynamicTracking.Value)
            {
                ScheduleRefresh();
            }
        }

        private ScheduledDelegate? scheduledRefresh;

        private void onSettingsValueChanged(ValueChangedEvent<double> _) => ScheduleRefresh();

        public void ScheduleRefresh()
        {
            scheduledRefresh?.Cancel();
            scheduledRefresh = Scheduler.AddDelayed(RefreshSkin, 50);
        }

        public void RefreshSkin()
        {
            isAbsolutePosition = !isAbsolutePosition;
            skinManager.CurrentSkinInfo.TriggerChange();
            // 更新按钮颜色
            updateButtonColor(refreshSkinButton, isAbsolutePosition);
            // 更新按钮文字
            updateButtonText(refreshSkinButton, isAbsolutePosition);
        }

        private void updateButtonColor(SettingsButton button, bool isAbsolute)
        {
            Color4 color = isAbsolute ? new Color4(0.2f, 0.4f, 0.8f, 0.3f) : new Color4(0.8f, 0.2f, 0.4f, 0.3f);

            // 找到按钮内的Box并更新颜色
            var box = button.ChildrenOfType<Box>().FirstOrDefault();

            box?.FadeColour(color, 200);
        }

        private void updateButtonText(SettingsButton button, bool isAbsolute)
        {
            string topText = isAbsolute ? "强制刷新, 并切换至 绝对位置" : "强制刷新, 并切换至 相对位置";
            string bottomText = isAbsolute ? "Refresh, Switch to Absolute" : "Refresh, Switch to Relative";

            var textContainer = button.ChildrenOfType<FillFlowContainer>().FirstOrDefault();

            if (textContainer != null)
            {
                var texts = textContainer.ChildrenOfType<OsuSpriteText>().ToArray();

                if (texts.Length >= 2)
                {
                    texts[0].Text = topText;
                    texts[1].Text = bottomText;
                }
            }
        }

        #endregion

        #region updateAllSkinComponentsTextureNames

        private void updateAllSkinComponentsTextureNames(EzSelectorNameSet textureName)
        {
            var scoreTexts = this.ChildrenOfType<EzScoreText>().ToList();
            var comboTexts = this.ChildrenOfType<EzComboText>().ToList();

            if (Parent != null)
            {
                var parentScoreTexts = Parent.ChildrenOfType<EzScoreText>().ToList();
                var parentComboTexts = Parent.ChildrenOfType<EzComboText>().ToList();

                scoreTexts.AddRange(parentScoreTexts);
                comboTexts.AddRange(parentComboTexts);
            }

            var root = this as Drawable;

            while (root.Parent != null)
            {
                root = root.Parent;
            }

            var rootScoreTexts = root.ChildrenOfType<EzScoreText>().ToList();
            var rootComboTexts = root.ChildrenOfType<EzComboText>().ToList();

            scoreTexts.AddRange(rootScoreTexts);
            comboTexts.AddRange(rootComboTexts);

            var hitResultScores = this.ChildrenOfType<EzComHitResultScore>().ToList();

            if (Parent != null)
            {
                var parentHitResultScores = Parent.ChildrenOfType<EzComHitResultScore>().ToList();
                hitResultScores.AddRange(parentHitResultScores);
            }

            var rootHitResultScores = root.ChildrenOfType<EzComHitResultScore>().ToList();
            hitResultScores.AddRange(rootHitResultScores);

            foreach (var scoreText in scoreTexts)
            {
                scoreText.FontName.Value = textureName;
            }

            foreach (var comboText in comboTexts)
            {
                comboText.FontName.Value = textureName;
            }

            foreach (var hitResultScore in hitResultScores)
            {
                hitResultScore.NameDropdown.Value = textureName;
            }
        }

        #endregion

        private void loadAvailableNoteSets()
        {
            availableNoteSets.Clear();

            try
            {
                const string relative_path = @"EzResources\note";
                string dataFolderPath = storage.GetFullPath(relative_path);
                Debug.Assert(!string.IsNullOrEmpty(dataFolderPath));

                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    Logger.Log($"EzSkinSettingsTab create Note Path: {dataFolderPath}");
                }

                // 获取所有子文件夹作为note套图选项
                string[] directories = Directory.GetDirectories(dataFolderPath);

                foreach (string dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    availableNoteSets.Add(dirName);
                }

                Logger.Log($"EzSkinSettingsTab Find {dataFolderPath} to {availableNoteSets.Count} Note Sets", LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "EzSkinSettingsTab Load NoteSets Error");
            }
        }
    }

    public partial class EzGlobalTextureNameSelector : EzSelectorEnumList;

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

    #endregion
}
