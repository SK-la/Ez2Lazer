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
using osu.Game.Configuration;
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
        public BindableNumber<double>? NonSquareNoteHeight;
        public Bindable<double>? VirtualHitPosition;
        private Bindable<double>? columnWidth;
        private Bindable<double>? specialFactor;
        private readonly Bindable<bool> dynamicTracking = new Bindable<bool>();

        private readonly Bindable<EzSelectorNameSet> globalTextureName = new Bindable<EzSelectorNameSet>((EzSelectorNameSet)4);

        private readonly Bindable<string> selectedNoteSet = new Bindable<string>();

        private readonly List<string> availableNoteSets = new List<string>();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

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
            columnWidth = config.GetBindable<double>(OsuSetting.ColumnWidth);
            specialFactor = config.GetBindable<double>(OsuSetting.SpecialFactor);
            VirtualHitPosition = config.GetBindable<double>(OsuSetting.VirtualHitPosition);

            var configBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight);

            NonSquareNoteHeight = new BindableDouble(configBindable.Value)
            {
                MinValue = 1,
                MaxValue = 100,
                Precision = 1f,
            };

            NonSquareNoteHeight.ValueChanged += e => configBindable.Value = e.NewValue;
            configBindable.ValueChanged += e => NonSquareNoteHeight.Value = e.NewValue;
            // NonSquareNoteHeight.ValueChanged += onSettingsValueChanged;

            globalTextureName.Value = (EzSelectorNameSet)ezSkinConfig.GetBindable<int>(EzSkinSetting.GlobalTextureName).Value;
            globalTextureName.ValueChanged += onTextureNameChanged;

            dynamicTracking.BindTo(ezSkinConfig.GetBindable<bool>(EzSkinSetting.DynamicTracking));
            dynamicTracking.ValueChanged += tracking =>
            {
                if (tracking.NewValue)
                {
                    columnWidth!.ValueChanged += onSettingsValueChanged;
                    specialFactor!.ValueChanged += onSettingsValueChanged;
                    VirtualHitPosition!.ValueChanged += onSettingsValueChanged;
                }
                else
                {
                    columnWidth!.ValueChanged -= onSettingsValueChanged;
                    specialFactor!.ValueChanged -= onSettingsValueChanged;
                    VirtualHitPosition!.ValueChanged -= onSettingsValueChanged;
                }

                ezSkinConfig.SetValue(EzSkinSetting.DynamicTracking, tracking.NewValue);
            };

            loadAvailableNoteSets();

            // 从配置中加载上次选择的note套图，如果有的话
            string configuredNoteSet = ezSkinConfig.Get<string>(EzSkinSetting.NoteSetName);
            if (!string.IsNullOrEmpty(configuredNoteSet) && availableNoteSets.Contains(configuredNoteSet))
                selectedNoteSet.Value = configuredNoteSet;
            else if (availableNoteSets.Count > 0)
                selectedNoteSet.Value = availableNoteSets[1];

            selectedNoteSet.ValueChanged += onNoteSetChanged;

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
                        new EzGlobalTextureNameSelector
                        {
                            LabelText = "(全局纹理名称)Global Texture Name",
                            Current = globalTextureName,
                            TooltipText = "统一修改当前皮肤中所有组件的纹理名称"
                        },
                        new SettingsDropdown<string>
                        {
                            LabelText = "(Note套图)Note Set",
                            Current = selectedNoteSet,
                            Items = availableNoteSets,
                            TooltipText = "选择不同的Note套图，影响音符和打击光效果"
                        },
                        new SettingsSlider<double>
                        {
                            LabelText = "(note高)Note Height",
                            Current = NonSquareNoteHeight,
                            KeyboardStep = 1.0f,
                        },
                        new SettingsButton
                        {
                            Action = RefreshSkin,
                        }.WithTwoLineText("(刷新&保存皮肤)", "Refresh & Save Skin")
                    }
                }
            };
        }

        #region 追踪处理

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
            skinManager.CurrentSkinInfo.TriggerChange();
        }

        #endregion

        #region MyRegion

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
                    Logger.Log($"EzSkinSettings create Note Path: {dataFolderPath}");
                }

                // 获取所有子文件夹作为note套图选项
                string[] directories = Directory.GetDirectories(dataFolderPath);

                foreach (string dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    availableNoteSets.Add(dirName);
                }

                Logger.Log($"EzSkinSettings Find {dataFolderPath} to {availableNoteSets.Count} Note Sets", LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "EzSkinSettings Load NoteSets Error");
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
