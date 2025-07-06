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
        private readonly Bindable<bool> globalHitPosition = new BindableBool();
        private readonly Bindable<EzSelectorNameSet> globalTextureName = new Bindable<EzSelectorNameSet>((EzSelectorNameSet)4);

        private readonly Bindable<string> selectedNoteSet = new Bindable<string>();
        private readonly List<string> availableNoteSets = new List<string>();

        private readonly Bindable<string> selectedStageSet = new Bindable<string>();
        private readonly List<string> availableStageSets = new List<string>();

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
            globalHitPosition.Value = ezSkinConfig.Get<bool>(EzSkinSetting.GlobalHitPosition);

            globalTextureName.Value = (EzSelectorNameSet)ezSkinConfig.GetBindable<int>(EzSkinSetting.GlobalTextureName).Value;

            loadFolderSets("note");
            string configuredNoteSet = ezSkinConfig.Get<string>(EzSkinSetting.NoteSetName);
            if (!string.IsNullOrEmpty(configuredNoteSet) && availableNoteSets.Contains(configuredNoteSet))
                selectedNoteSet.Value = configuredNoteSet;
            else if (availableNoteSets.Count > 0)
                selectedNoteSet.Value = availableNoteSets[1];

            loadFolderSets("Stage");
            string configStageSet = ezSkinConfig.Get<string>(EzSkinSetting.StageName);
            if (!string.IsNullOrEmpty(configStageSet) && availableStageSets.Contains(configStageSet))
                selectedStageSet.Value = configStageSet;
            else if (availableStageSets.Count > 0)
                selectedStageSet.Value = availableStageSets[1];

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
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth),
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Special Factor (特殊轨道倍率)",
                            TooltipText = "特殊列关联S1类型, 可自定义"
                                          + "\nSpecial columns are associated with S1 type, customizable",
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor),
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
                            LabelText = "Note Height (方形note 厚度)",
                            TooltipText = "统一修改非圆形note的高度\n"
                                          + "Fixed Height for square notes",
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight),
                            KeyboardStep = 1.0f,
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "Note Track Line",
                            TooltipText = "note两侧辅助轨道线的高度\n"
                                          + "note side auxiliary track line height",
                            Current = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteTrackLineHeight),
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

            selectedNoteSet.BindValueChanged(e => ezSkinConfig.SetValue(EzSkinSetting.NoteSetName, e.NewValue));
            selectedStageSet.BindValueChanged(e => ezSkinConfig.SetValue(EzSkinSetting.StageName, e.NewValue));
            globalTextureName.BindValueChanged(OnTextureNameChanged);
        }

        #region 追踪处理

        private void OnTextureNameChanged(ValueChangedEvent<EzSelectorNameSet> textureName)
        {
            updateAllSkinComponentsTextureNames(textureName.NewValue);

            ezSkinConfig.SetValue(EzSkinSetting.GlobalTextureName, (int)textureName.NewValue);
        }

        private ScheduledDelegate? scheduledRefresh;

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

        #region 刷新所有EzComponent的纹理名称

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

        private void loadFolderSets(string type)
        {
            List<string> targetList = type.Equals("note", StringComparison.OrdinalIgnoreCase) ? availableNoteSets : availableStageSets;
            targetList.Clear();

            try
            {
                string relativePath = $@"EzResources\{type}";
                string dataFolderPath = storage.GetFullPath(relativePath);
                Debug.Assert(!string.IsNullOrEmpty(dataFolderPath));

                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    Logger.Log($"EzSkinTab create {type} Path: {dataFolderPath}");
                }

                string[] directories = Directory.GetDirectories(dataFolderPath);

                foreach (string dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    targetList.Add(dirName);
                }

                Logger.Log($"EzSkinTab Find {dataFolderPath} to {targetList.Count} {type} Sets", LoggingTarget.Runtime, LogLevel.Debug);
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
